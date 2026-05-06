using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.API.DTOs;

// ── Auth ──────────────────────────────────────────────────────────────────────

public class RegisterDto
{
    [Required] public string FullName { get; set; } = string.Empty;
    [Required][EmailAddress] public string Email { get; set; } = string.Empty;
    [Required][MinLength(6)] public string Password { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
}

public class LoginDto
{
    [Required][EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsAdmin { get; set; }
}

// ── Expense ───────────────────────────────────────────────────────────────────

public class CreateExpenseDto
{
    [Required][MaxLength(200)] public string Description { get; set; } = string.Empty;
    [Required][Range(0.01, double.MaxValue)] public decimal Amount { get; set; }
    [Required][MaxLength(50)] public string Category { get; set; } = string.Empty;
    [Required] public DateOnly Date { get; set; }
    [MaxLength(500)] public string? Note { get; set; }

    public string? ReceiptPath { get; set; }
}

public class UpdateExpenseDto
{
    [Required][MaxLength(200)] public string Description { get; set; } = string.Empty;
    [Required][Range(0.01, double.MaxValue)] public decimal Amount { get; set; }
    [Required][MaxLength(50)] public string Category { get; set; } = string.Empty;
    [Required] public DateOnly Date { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
}

public class ExpenseDto
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string? ReceiptPath { get; set; }
}

public class ExpenseFilterDto
{
    public string? Category { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Period { get; set; } // today | week | month
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

// ── Summary ───────────────────────────────────────────────────────────────────

public class SummaryDto
{
    public decimal TodayTotal { get; set; }
    public decimal WeekTotal { get; set; }
    public decimal MonthTotal { get; set; }
    public List<CategorySummaryDto> ByCategory { get; set; } = new();
    public List<DailySummaryDto> Last7Days { get; set; } = new();
    public List<MonthlySummaryDto> Last6Months { get; set; } = new();
}

public class CategorySummaryDto
{
    public string Category { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int Count { get; set; }
}

public class DailySummaryDto
{
    public DateOnly Date { get; set; }
    public decimal Total { get; set; }
}

public class MonthlySummaryDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

public class BudgetAlertDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal Total { get; set; }
    public decimal Limit { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// ── Budget ────────────────────────────────────────────────────────────────────

public class CreateBudgetDto
{
    [Required][MaxLength(50)] public string Category { get; set; } = string.Empty;
    [Required][Range(0.01, double.MaxValue)] public decimal Limit { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
}

public class UpdateBudgetDto
{
    [Required][Range(0.01, double.MaxValue)] public decimal Limit { get; set; }
}

public class BudgetDto
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Limit { get; set; }
    public decimal Spent { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal RemainingAmount => Limit - Spent;
    public decimal PercentageUsed => Limit > 0 ? (Spent / Limit) * 100 : 0;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
