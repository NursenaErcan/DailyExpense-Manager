using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExpenseTracker.API.Models;

public class BudgetAlert
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public AppUser? User { get; set; }

    [Required]
    [MaxLength(150)]
    public string UserEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string UserName { get; set; } = string.Empty;

    public int Month { get; set; }
    public int Year { get; set; }
    public decimal Total { get; set; }
    public decimal Limit { get; set; }

    [Required]
    [MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
