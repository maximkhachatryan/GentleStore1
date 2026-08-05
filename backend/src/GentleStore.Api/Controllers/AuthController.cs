using GentleStore.Api.Auth;
using GentleStore.Api.Contracts;
using GentleStore.Infrastructure.Auth;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly ICurrentUser _current;

    public AuthController(AppDbContext db, IPasswordHasher hasher, ITokenService tokens, ICurrentUser current)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _current = current;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var email = (req.Email ?? string.Empty).Trim().ToLower();
        var user = await _db.Users.Include(u => u.Store)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        if (user is null || !user.IsActive || !_hasher.Verify(req.Password ?? string.Empty, user.PasswordHash))
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");

        var (token, expiresAt) = _tokens.CreateToken(user);
        var dto = new UserDto(user.Id, user.Email, user.FullName, user.Role.ToString(), user.StoreId, user.Store?.Name, user.Store?.Slug);
        return Ok(new LoginResponse(token, expiresAt, dto));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        if (_current.UserId is not Guid userId)
            return Unauthorized();

        var user = await _db.Users.Include(u => u.Store).FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null || !user.IsActive)
            return Unauthorized();

        return Ok(new UserDto(user.Id, user.Email, user.FullName, user.Role.ToString(), user.StoreId, user.Store?.Name, user.Store?.Slug));
    }
}
