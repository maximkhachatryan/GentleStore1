namespace GentleStore.Api.Contracts;

public record LoginRequest(string Email, string Password);

public record UserDto(Guid Id, string Email, string FullName, string Role, Guid? StoreId, string? StoreName, string? StoreSlug);

public record LoginResponse(string Token, DateTime ExpiresAt, UserDto User);
