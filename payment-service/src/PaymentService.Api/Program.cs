using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PaymentService.Api.Idempotency;
using PaymentService.Api.Security;
using PaymentService.Infrastructure;
using PaymentService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPaymentInfrastructure(builder.Configuration);

builder.Services.Configure<InternalAccessOptions>(builder.Configuration.GetSection(InternalAccessOptions.SectionName));

// Scoped because it resolves the scoped idempotency store, which shares the request's DbContext.
builder.Services.AddScoped<IdempotencyFilter>();

builder.Services.AddControllers();

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GentleStore PaymentService API",
        Version = "v1",
        Description =
            "Internal payment API. Gateway integrations plug in behind IPaymentGateway; " +
            "no concrete provider is implemented yet."
    });

    c.TagActionsBy(api =>
    {
        var segments = (api.RelativePath ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
        var group = segments.Length >= 2 && string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase)
            ? segments[1]
            : segments.FirstOrDefault() ?? "General";
        return [group.Length == 0 ? "General" : char.ToUpperInvariant(group[0]) + group[1..]];
    });

    var xml = Path.Combine(AppContext.BaseDirectory, $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
});

var app = builder.Build();

// Gateways call the webhook endpoint through the same reverse proxy as everything else, and both
// signature debugging and rate limiting need the real client IP and scheme.
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

// Before the endpoints, so an unauthorized call never reaches a controller. No-op unless
// InternalAccess:Keys is configured; /health and /api/webhooks are always exempt.
app.UseMiddleware<InternalAccessMiddleware>();

app.MapControllers();

// The payment database is this service's alone, so it migrates itself rather than waiting on
// GentleStore's deployment.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
