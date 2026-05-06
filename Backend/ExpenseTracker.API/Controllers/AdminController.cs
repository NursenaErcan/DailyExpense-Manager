using ExpenseTracker.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    // ================= MANAGE USERS =================

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _db.Users
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.Email
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _db.Users.FindAsync(id);

        if (user == null)
            return NotFound(new { message = "User not found" });

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        return Ok(new { message = "User deleted successfully" });
    }

    // ================= SEND NOTIFICATION =================

    [HttpPost("send-notification")]
    public IActionResult SendNotification([FromBody] AdminNotificationDto dto)
    {
        // For now this just simulates sending notification.
        // Later you can connect email/SMS system here.

        return Ok(new
        {
            message = "Notification sent successfully",
            title = dto.Title,
            body = dto.Body
        });
    }

    // ================= BACKUP DATA =================

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts()
    {
        var alerts = await _db.BudgetAlerts
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                a.UserId,
                a.UserName,
                a.UserEmail,
                a.Month,
                a.Year,
                a.Total,
                a.Limit,
                a.Message,
                a.CreatedAt
            })
            .ToListAsync();

        return Ok(alerts);
    }

    [HttpGet("backup")]
    public async Task<IActionResult> BackupData()
    {
        var users = await _db.Users
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.Email
            })
            .ToListAsync();

        var expenses = await _db.Expenses.ToListAsync();

        var backup = new
        {
            CreatedAt = DateTime.UtcNow,
            Users = users,
            Expenses = expenses
        };

        return Ok(backup);
    }
}

public class AdminNotificationDto
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}