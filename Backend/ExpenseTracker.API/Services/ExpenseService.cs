using ExpenseTracker.API.Data;
using ExpenseTracker.API.DTOs;
using ExpenseTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.API.Services;

public interface IExpenseService
{
    Task<PagedResult<ExpenseDto>> GetExpensesAsync(string userId, ExpenseFilterDto filter);
    Task<ExpenseDto?> GetByIdAsync(string userId, int id);
    Task<ExpenseDto> CreateAsync(string userId, CreateExpenseDto dto);
    Task<ExpenseDto?> UpdateAsync(string userId, int id, UpdateExpenseDto dto);
    Task<bool> DeleteAsync(string userId, int id);
    Task<SummaryDto> GetSummaryAsync(string userId);
    Task<bool> AttachReceiptAsync(string userId, int expenseId, string path);
}

public class ExpenseService : IExpenseService
{
    private readonly AppDbContext _db;
    private readonly IBudgetAlertService _alertService;
    private readonly decimal _monthlyBudgetLimit;

    public ExpenseService(AppDbContext db, IBudgetAlertService alertService, IConfiguration config)
    {
        _db = db;
        _alertService = alertService;
        _monthlyBudgetLimit = config.GetSection("Budget").GetValue<decimal>("MonthlyLimit", 5000m);
    }

    public async Task<PagedResult<ExpenseDto>> GetExpensesAsync(string userId, ExpenseFilterDto filter)
    {
        var q = _db.Expenses.Where(e => e.UserId == userId);

        if (!string.IsNullOrEmpty(filter.Category))
            q = q.Where(e => e.Category == filter.Category);

        if (filter.Period is not null)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            q = filter.Period switch
            {
                "today" => q.Where(e => e.Date == today),
                "week"  => q.Where(e => e.Date >= today.AddDays(-(int)DateTime.UtcNow.DayOfWeek)),
                "month" => q.Where(e => e.Date.Year == today.Year && e.Date.Month == today.Month),
                _ => q
            };
        }
        else
        {
            if (filter.From.HasValue) q = q.Where(e => e.Date >= filter.From.Value);
            if (filter.To.HasValue)   q = q.Where(e => e.Date <= filter.To.Value);
        }

        var total = await q.CountAsync();

        var items = await q
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(e => ToDto(e))
            .ToListAsync();

        return new PagedResult<ExpenseDto>
        {
            Items = items,
            TotalCount = total,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<ExpenseDto?> GetByIdAsync(string userId, int id)
    {
        var e = await _db.Expenses
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        return e is null ? null : ToDto(e);
    }

    public async Task<ExpenseDto> CreateAsync(string userId, CreateExpenseDto dto)
    {
        var expense = new Expense
        {
            UserId      = userId,
            Description = dto.Description,
            Amount      = dto.Amount,
            Category    = dto.Category,
            Date        = dto.Date,
            Note        = dto.Note,
            ReceiptPath = dto.ReceiptPath ?? string.Empty,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();

        await NotifyBudgetExceededAsync(userId, expense.Date);

        return ToDto(expense);
    }

    public async Task<ExpenseDto?> UpdateAsync(string userId, int id, UpdateExpenseDto dto)
    {
        var expense = await _db.Expenses
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (expense is null) return null;

        expense.Description = dto.Description;
        expense.Amount      = dto.Amount;
        expense.Category    = dto.Category;
        expense.Date        = dto.Date;
        expense.Note        = dto.Note;
        expense.UpdatedAt   = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await NotifyBudgetExceededAsync(userId, expense.Date);

        return ToDto(expense);
    }

    public async Task<bool> AttachReceiptAsync(string userId, int expenseId, string path)
    {
        var expense = await _db.Expenses
            .FirstOrDefaultAsync(e => e.Id == expenseId && e.UserId == userId);

        if (expense is null) return false;

        expense.ReceiptPath = path;
        expense.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(string userId, int id)
    {
        var expense = await _db.Expenses
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (expense is null) return false;

        _db.Expenses.Remove(expense);
        await _db.SaveChangesAsync();

        return true;
    }

    private async Task NotifyBudgetExceededAsync(string userId, DateOnly date)
    {
        var month = date.Month;
        var year = date.Year;

        var total = await SumExpenseAmountsAsync(_db.Expenses
            .Where(e => e.UserId == userId && e.Date.Month == month && e.Date.Year == year));

        if (total <= _monthlyBudgetLimit)
            return;

        if (await _alertService.AlertExistsAsync(userId, month, year))
            return;

        var user = await _db.Users.FindAsync(userId);
        if (user is null)
            return;

        var userName = user.FullName ?? string.Empty;
        var userEmail = user.Email ?? string.Empty;

        var alert = new BudgetAlert
        {
            UserId = userId,
            UserEmail = userEmail,
            UserName = userName,
            Month = month,
            Year = year,
            Total = total,
            Limit = _monthlyBudgetLimit,
            Message = $"{userName} exceeded the monthly budget limit of {_monthlyBudgetLimit:C}. Current total for {new DateTime(year, month, 1):MMMM yyyy} is {total:C}.",
            CreatedAt = DateTime.UtcNow
        };

        await _alertService.CreateAlertAsync(alert);
    }

    public async Task<SummaryDto> GetSummaryAsync(string userId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekStart = today.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var all = await _db.Expenses
            .Where(e => e.UserId == userId)
            .ToListAsync();

        var last7Start = today.AddDays(-6);
        var month6Start = new DateOnly(today.Year, today.Month, 1).AddMonths(-5);

        var last7Days = all
            .Where(e => e.Date >= last7Start)
            .GroupBy(e => e.Date)
            .Select(g => new DailySummaryDto
            {
                Date = g.Key,
                Total = g.Sum(x => x.Amount)
            })
            .OrderBy(x => x.Date)
            .ToList();

        var last6Months = all
            .Where(e => e.Date >= month6Start)
            .GroupBy(e => new { e.Date.Year, e.Date.Month })
            .Select(g => new MonthlySummaryDto
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                MonthName = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM"),
                Total = g.Sum(x => x.Amount)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToList();

        var byCategory = all
            .GroupBy(e => e.Category)
            .Select(g => new CategorySummaryDto
            {
                Category = g.Key,
                Total = g.Sum(x => x.Amount),
                Count = g.Count()
            })
            .OrderByDescending(x => x.Total)
            .ToList();

        return new SummaryDto
        {
            TodayTotal  = all.Where(e => e.Date == today).Sum(e => e.Amount),
            WeekTotal   = all.Where(e => e.Date >= weekStart).Sum(e => e.Amount),
            MonthTotal  = all.Where(e => e.Date >= monthStart).Sum(e => e.Amount),
            ByCategory  = byCategory,
            Last7Days   = last7Days,
            Last6Months = last6Months
        };
    }

    private static async Task<decimal> SumExpenseAmountsAsync(IQueryable<Expense> query)
    {
        var amounts = await query.Select(e => e.Amount).ToListAsync();
        return amounts.Sum();
    }

    private static ExpenseDto ToDto(Expense e) => new()
    {
        Id          = e.Id,
        Description = e.Description,
        Amount      = e.Amount,
        Category    = e.Category,
        Date        = e.Date,
        Note        = e.Note,
        ReceiptPath = e.ReceiptPath,
        CreatedAt   = e.CreatedAt,
        UpdatedAt   = e.UpdatedAt
    };
}