using GentleStore.Api.Auth;
using GentleStore.Api.Contracts;
using GentleStore.Domain.Entities;
using GentleStore.Domain.Enums;
using GentleStore.Infrastructure.Auth;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = Policies.SuperAdmin)]
public class AdminUsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminUsersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var users = await _db.Users.Include(u => u.Store)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new AdminUserDto(u.Id, u.Email, u.FullName, u.Role.ToString(), u.StoreId, u.Store!.Name, u.IsActive, u.CreatedAt))
            .ToListAsync();
        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest req, [FromServices] IPasswordHasher hasher)
    {
        if (string.IsNullOrWhiteSpace(req.Email)) return BadRequest(new { error = "Email is required." });
        if (string.IsNullOrWhiteSpace(req.FullName)) return BadRequest(new { error = "Full name is required." });
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
            return BadRequest(new { error = "Password must be at least 6 characters." });
        if (!Enum.TryParse<UserRole>(req.Role, true, out var role))
            return BadRequest(new { error = "Invalid role." });

        var email = req.Email.Trim().ToLower();
        if (await _db.Users.AnyAsync(u => u.Email.ToLower() == email))
            return Conflict(new { error = "A user with this email already exists." });

        Guid? storeId = null;
        if (role is UserRole.StoreOwner or UserRole.StoreStaff)
        {
            if (req.StoreId is null || !await _db.Stores.AnyAsync(s => s.Id == req.StoreId))
                return BadRequest(new { error = "A valid store is required for store roles." });
            storeId = req.StoreId;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = req.FullName.Trim(),
            PasswordHash = hasher.Hash(req.Password),
            Role = role,
            StoreId = storeId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Created($"/api/admin/users/{user.Id}",
            new AdminUserDto(user.Id, user.Email, user.FullName, user.Role.ToString(), user.StoreId, null, user.IsActive, user.CreatedAt));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest req, [FromServices] IPasswordHasher hasher, [FromServices] ICurrentUser current)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.FullName)) return BadRequest(new { error = "Full name is required." });
        if (!Enum.TryParse<UserRole>(req.Role, true, out var role))
            return BadRequest(new { error = "Invalid role." });

        if (user.Id == current.UserId && (!req.IsActive || role != UserRole.SuperAdmin))
            return BadRequest(new { error = "You cannot change your own role or deactivate yourself." });

        Guid? storeId = null;
        if (role is UserRole.StoreOwner or UserRole.StoreStaff)
        {
            if (req.StoreId is null || !await _db.Stores.AnyAsync(s => s.Id == req.StoreId))
                return BadRequest(new { error = "A valid store is required for store roles." });
            storeId = req.StoreId;
        }

        user.FullName = req.FullName.Trim();
        user.Role = role;
        user.StoreId = storeId;
        user.IsActive = req.IsActive;
        if (!string.IsNullOrWhiteSpace(req.Password))
        {
            if (req.Password.Length < 6) return BadRequest(new { error = "Password must be at least 6 characters." });
            user.PasswordHash = hasher.Hash(req.Password);
        }

        await _db.SaveChangesAsync();
        return Ok(new AdminUserDto(user.Id, user.Email, user.FullName, user.Role.ToString(), user.StoreId, null, user.IsActive, user.CreatedAt));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromServices] ICurrentUser current)
    {
        if (id == current.UserId) return BadRequest(new { error = "You cannot delete your own account." });
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
