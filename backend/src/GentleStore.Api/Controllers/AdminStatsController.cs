using GentleStore.Api.Auth;
using GentleStore.Api.Contracts;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

[ApiController]
[Route("api/admin/stats")]
[Authorize(Policy = Policies.SuperAdmin)]
public class AdminStatsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminStatsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var stats = new AdminStatsDto(
            await _db.Stores.CountAsync(),
            await _db.Stores.CountAsync(s => s.IsActive),
            await _db.Products.CountAsync(),
            await _db.Users.CountAsync());
        return Ok(stats);
    }
}
