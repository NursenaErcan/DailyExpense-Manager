using ExpenseTracker.API.Data;
using ExpenseTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.API.Services;

public interface IBudgetAlertService
{
    Task<bool> AlertExistsAsync(string userId, int month, int year);
    Task CreateAlertAsync(BudgetAlert alert);
    Task<List<BudgetAlert>> GetAlertsAsync();
}

public class BudgetAlertService : IBudgetAlertService
{
    private readonly AppDbContext _db;

    public BudgetAlertService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> AlertExistsAsync(string userId, int month, int year)
    {
        return await _db.Set<BudgetAlert>()
            .AnyAsync(a => a.UserId == userId && a.Month == month && a.Year == year);
    }

    public async Task CreateAlertAsync(BudgetAlert alert)
    {
        _db.Set<BudgetAlert>().Add(alert);
        await _db.SaveChangesAsync();
    }

    public async Task<List<BudgetAlert>> GetAlertsAsync()
    {
        return await _db.Set<BudgetAlert>()
            .Include(a => a.User)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }
}
