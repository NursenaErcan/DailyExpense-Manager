using ExpenseTracker.API.Data;
using ExpenseTracker.API.DTOs;
using ExpenseTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.API.Services;

public interface IBudgetService
{
    Task<List<BudgetDto>> GetBudgetsAsync(string userId, int month, int year);
    Task<BudgetDto?> GetByIdAsync(string userId, int id);
    Task<BudgetDto> CreateAsync(string userId, CreateBudgetDto dto);
    Task<BudgetDto?> UpdateAsync(string userId, int id, UpdateBudgetDto dto);
    Task<bool> DeleteAsync(string userId, int id);
}

public class BudgetService : IBudgetService
{
    private readonly AppDbContext _db;

    public BudgetService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<BudgetDto>> GetBudgetsAsync(string userId, int month, int year)
    {
        var budgets = await _db.Budgets
            .Where(b => b.UserId == userId && b.Month == month && b.Year == year)
            .ToListAsync();

        var result = new List<BudgetDto>();

        foreach (var budget in budgets)
        {
            var spent = await SumExpenseAmountsAsync(_db.Expenses
                .Where(e => e.UserId == userId 
                    && e.Category == budget.Category 
                    && e.Date.Month == month 
                    && e.Date.Year == year));

            result.Add(new BudgetDto
            {
                Id = budget.Id,
                Category = budget.Category,
                Limit = budget.Limit,
                Spent = spent,
                Month = budget.Month,
                Year = budget.Year,
                CreatedAt = budget.CreatedAt,
                UpdatedAt = budget.UpdatedAt
            });
        }

        return result.OrderBy(x => x.Category).ToList();
    }

    public async Task<BudgetDto?> GetByIdAsync(string userId, int id)
    {
        var budget = await _db.Budgets
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (budget is null)
            return null;

        var spent = await SumExpenseAmountsAsync(_db.Expenses
            .Where(e => e.UserId == userId 
                && e.Category == budget.Category 
                && e.Date.Month == budget.Month 
                && e.Date.Year == budget.Year));

        return new BudgetDto
        {
            Id = budget.Id,
            Category = budget.Category,
            Limit = budget.Limit,
            Spent = spent,
            Month = budget.Month,
            Year = budget.Year,
            CreatedAt = budget.CreatedAt,
            UpdatedAt = budget.UpdatedAt
        };
    }

    public async Task<BudgetDto> CreateAsync(string userId, CreateBudgetDto dto)
    {
        // If month/year not provided, use current month/year
        var now = DateTime.UtcNow;
        var month = dto.Month > 0 ? dto.Month : now.Month;
        var year = dto.Year > 0 ? dto.Year : now.Year;

        // Check if budget already exists for this category and month
        var existing = await _db.Budgets
            .FirstOrDefaultAsync(b => b.UserId == userId 
                && b.Category == dto.Category 
                && b.Month == month 
                && b.Year == year);

        Budget budget;
        if (existing != null)
        {
            existing.Limit = dto.Limit;
            existing.UpdatedAt = DateTime.UtcNow;
            _db.Budgets.Update(existing);
            budget = existing;
        }
        else
        {
            budget = new Budget
            {
                UserId = userId,
                Category = dto.Category,
                Limit = dto.Limit,
                Month = month,
                Year = year,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Budgets.Add(budget);
        }

        await _db.SaveChangesAsync();

        // Calculate spent amount
        var spent = await SumExpenseAmountsAsync(_db.Expenses
            .Where(e => e.UserId == userId 
                && e.Category == budget.Category 
                && e.Date.Month == month 
                && e.Date.Year == year));

        return new BudgetDto
        {
            Id = budget.Id,
            Category = budget.Category,
            Limit = budget.Limit,
            Spent = spent,
            Month = budget.Month,
            Year = budget.Year,
            CreatedAt = budget.CreatedAt,
            UpdatedAt = budget.UpdatedAt
        };
    }

    private static async Task<decimal> SumExpenseAmountsAsync(IQueryable<Expense> query)
    {
        var amounts = await query.Select(e => e.Amount).ToListAsync();
        return amounts.Sum();
    }

    public async Task<BudgetDto?> UpdateAsync(string userId, int id, UpdateBudgetDto dto)
    {
        var budget = await _db.Budgets
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (budget is null)
            return null;

        budget.Limit = dto.Limit;
        budget.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return await GetByIdAsync(userId, id);
    }

    public async Task<bool> DeleteAsync(string userId, int id)
    {
        var budget = await _db.Budgets
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (budget is null)
            return false;

        _db.Budgets.Remove(budget);
        await _db.SaveChangesAsync();

        return true;
    }
}
