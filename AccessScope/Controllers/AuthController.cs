using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccessScope.Data;
using AccessScope.Models;
using AccessScope.Services;

namespace AccessScope.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuthService _authService;

    public AuthController(AppDbContext db, AuthService authService)
    {
        _db = db;
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return Conflict("Email already registered.");

        var role = request.Role == "Admin" ? "Admin" : "User";

        var user = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Created(string.Empty, new { user.Id, user.Email, user.Role });
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await _authService.ValidateCredentialsAsync(request.Email, request.Password);
        if (user is null) return Unauthorized("Invalid email or password.");

        return Ok(new LoginResponse(_authService.IssueAccessToken(user)));
    }
}
