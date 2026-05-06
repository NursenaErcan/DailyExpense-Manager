using Microsoft.AspNetCore.Identity;

namespace ExpenseTracker.API.Models;

public class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
