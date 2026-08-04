using System.Security.Claims;
using GentleStore.Domain.Enums;

namespace GentleStore.Api.Auth;

public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? StoreId { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }
}

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public Guid? StoreId =>
        Guid.TryParse(Principal?.FindFirstValue(AppClaimTypes.StoreId), out var id) ? id : null;

    public UserRole? Role =>
        Enum.TryParse<UserRole>(Principal?.FindFirstValue(ClaimTypes.Role), out var r) ? r : null;
}
