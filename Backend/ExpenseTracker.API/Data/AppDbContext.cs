using ExpenseTracker.API.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.API.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<BudgetAlert> BudgetAlerts => Set<BudgetAlert>();
    public DbSet<Budget> Budgets => Set<Budget>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Expense>(e =>
        {
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.Date);
            e.HasIndex(x => new { x.UserId, x.Date });

            e.HasOne(x => x.User)
             .WithMany(u => u.Expenses)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BudgetAlert>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Month, x.Year }).IsUnique(false);
            e.HasOne(x => x.User)
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Budget>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Month, x.Year });
            e.HasIndex(x => new { x.UserId, x.Category, x.Month, x.Year }).IsUnique(true);
            e.HasOne(x => x.User)
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
