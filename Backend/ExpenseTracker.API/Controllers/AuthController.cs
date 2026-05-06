using ExpenseTracker.API.DTOs;
using ExpenseTracker.API.Models;
using ExpenseTracker.API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IJwtService _jwt;

    public AuthController(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager, IJwtService jwt)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _jwt = jwt;
    }

    /// <summary>Register a new user account.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (await _userManager.FindByEmailAsync(dto.Email) is not null)
            return BadRequest(new { message = "Email already in use." });

        var user = new AppUser
        {
            FullName = dto.FullName,
            Email    = dto.Email,
            UserName = dto.Email
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        if (dto.IsAdmin)
        {
            if (!await _roleManager.RoleExistsAsync("Admin"))
                await _roleManager.CreateAsync(new IdentityRole("Admin"));

            await _userManager.AddToRoleAsync(user, "Admin");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var (token, expiresAt) = _jwt.GenerateToken(user, roles);

        return Ok(new AuthResponseDto
        {
            Token     = token,
            Email     = user.Email!,
            FullName  = user.FullName,
            ExpiresAt = expiresAt,
            IsAdmin   = roles.Contains("Admin")
        });
    }

    /// <summary>Login and receive a JWT token.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, dto.Password))
            return Unauthorized(new { message = "Invalid email or password." });

        var roles = await _userManager.GetRolesAsync(user);
        var (token, expiresAt) = _jwt.GenerateToken(user, roles);
        return Ok(new AuthResponseDto
        {
            Token     = token,
            Email     = user.Email!,
            FullName  = user.FullName,
            ExpiresAt = expiresAt,
            IsAdmin   = roles.Contains("Admin")
        });
    }
}
