using System.Security.Claims;
using ExpenseTracker.API.DTOs;
using ExpenseTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BudgetsController : ControllerBase
{
    private readonly IBudgetService _service;

    public BudgetsController(IBudgetService service)
    {
        _service = service;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("sub")
                          ?? throw new UnauthorizedAccessException();

    // ===================== GET =====================

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int month = 0, [FromQuery] int year = 0)
    {
        var now = DateTime.UtcNow;
        var m = month > 0 ? month : now.Month;
        var y = year > 0 ? year : now.Year;

        var budgets = await _service.GetBudgetsAsync(UserId, m, y);
        return Ok(budgets);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var budget = await _service.GetByIdAsync(UserId, id);
        return budget is null ? NotFound() : Ok(budget);
    }

    // ===================== CREATE =====================

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBudgetDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var budget = await _service.CreateAsync(UserId, dto);
        return CreatedAtAction(nameof(GetById), new { id = budget.Id }, budget);
    }

    // ===================== UPDATE =====================

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBudgetDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var budget = await _service.UpdateAsync(UserId, id, dto);
        return budget is null ? NotFound() : Ok(budget);
    }

    // ===================== DELETE =====================

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(UserId, id);
        return deleted ? NoContent() : NotFound();
    }
}
