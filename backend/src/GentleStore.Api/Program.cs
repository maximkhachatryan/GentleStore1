using System.Text;
using GentleStore.Api.Auth;
using GentleStore.Domain.Enums;
using GentleStore.Infrastructure;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton<ITokenService, TokenService>();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
          ?? throw new InvalidOperationException("Jwt settings are missing.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.SuperAdmin, p => p.RequireRole(nameof(UserRole.SuperAdmin)))
    .AddPolicy(Policies.StoreMember, p => p.RequireRole(nameof(UserRole.StoreOwner), nameof(UserRole.StoreStaff), nameof(UserRole.SuperAdmin)))
    .AddPolicy(Policies.StoreOwner, p => p.RequireRole(nameof(UserRole.StoreOwner)));

builder.Services.AddCors(options => options.AddPolicy("frontend", policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>())
          .AllowAnyHeader()
          .AllowAnyMethod()));

builder.Services.AddControllers();

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "GentleStore API", Version = "v1" });
    c.TagActionsBy(api =>
    {
        var segments = (api.RelativePath ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
        var group = segments.Length >= 2 && string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase)
            ? segments[1]
            : segments.FirstOrDefault() ?? "General";
        return new[] { group.Length == 0 ? "General" : char.ToUpperInvariant(group[0]) + group[1..] };
    });
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { scheme, Array.Empty<string>() } });
});

var app = builder.Build();

// Trust the reverse proxy (nginx) for scheme + client IP.
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownIPNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Always apply migrations + ensure the super admin; demo data is opt-in via Seed:DemoData.
using (var scope = app.Services.CreateScope())
{
    var seed = builder.Configuration.GetSection("Seed");
    await DbInitializer.InitializeAsync(
        scope.ServiceProvider,
        seed["SuperAdminEmail"] ?? "admin@gentlestore.local",
        seed["SuperAdminPassword"] ?? "Admin123!",
        seed["DemoStoreOwnerPassword"] ?? "Owner123!",
        seedDemoData: builder.Configuration.GetValue<bool>("Seed:DemoData"));
}

app.Run();
