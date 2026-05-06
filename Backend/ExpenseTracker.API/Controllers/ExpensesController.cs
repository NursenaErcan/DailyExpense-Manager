using System.Security.Claims;
using ExpenseTracker.API.DTOs;
using ExpenseTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _service;
    private readonly IWebHostEnvironment _env;

    public ExpensesController(IExpenseService service, IWebHostEnvironment env)
    {
        _service = service;
        _env = env;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("sub")
                          ?? throw new UnauthorizedAccessException();

    // ===================== GET =====================

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ExpenseFilterDto filter)
        => Ok(await _service.GetExpensesAsync(UserId, filter));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var expense = await _service.GetByIdAsync(UserId, id);
        return expense is null ? NotFound() : Ok(expense);
    }

    // ===================== CREATE WITH RECEIPT =====================

    [HttpPost]
    public async Task<IActionResult> Create([FromForm] CreateExpenseDto dto, IFormFile? receipt)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Save file if exists
        if (receipt != null)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "receipts");

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = Guid.NewGuid() + Path.GetExtension(receipt.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await receipt.CopyToAsync(stream);

            dto.ReceiptPath = "/receipts/" + fileName;
        }

        var expense = await _service.CreateAsync(UserId, dto);
        return CreatedAtAction(nameof(GetById), new { id = expense.Id }, expense);
    }

    // ===================== UPLOAD RECEIPT SEPARATELY =====================

    [HttpPost("{id:int}/receipt")]
    public async Task<IActionResult> UploadReceipt(int id, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        var uploadsFolder = Path.Combine(_env.WebRootPath, "receipts");

        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
        var filePath = Path.Combine(uploadsFolder, fileName);

        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var updated = await _service.AttachReceiptAsync(UserId, id, "/receipts/" + fileName);

        return updated ? Ok(new { message = "Receipt uploaded" }) : NotFound();
    }

    // ===================== UPDATE =====================

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateExpenseDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var expense = await _service.UpdateAsync(UserId, id, dto);
        return expense is null ? NotFound() : Ok(expense);
    }

    // ===================== DELETE =====================

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(UserId, id);
        return deleted ? NoContent() : NotFound();
    }

    // ===================== SUMMARY =====================

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
        => Ok(await _service.GetSummaryAsync(UserId));
}