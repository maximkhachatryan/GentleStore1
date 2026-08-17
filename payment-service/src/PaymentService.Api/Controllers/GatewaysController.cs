using Microsoft.AspNetCore.Mvc;
using PaymentService.Api.Contracts;
using PaymentService.Application.Abstractions.Gateways;

namespace PaymentService.Api.Controllers;

/// <summary>
/// What this build can do. Lets a console render a merchant's provider choices, and a caller check
/// whether manual capture or partial refunds are available before offering them.
/// </summary>
[ApiController]
[Route("api/gateways")]
[Produces("application/json")]
public class GatewaysController : ControllerBase
{
    private readonly IPaymentGatewayRegistry _registry;

    public GatewaysController(IPaymentGatewayRegistry registry) => _registry = registry;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GatewayDto>), StatusCodes.Status200OK)]
    public IActionResult List() =>
        Ok(_registry.All.OrderBy(g => g.Key).Select(g => g.ToDto()).ToList());
}
