using Microsoft.AspNetCore.Mvc;
using PaymentService.Api.Contracts;
using PaymentService.Application.Merchants;
using PaymentService.Domain.Enums;

namespace PaymentService.Api.Controllers;

/// <summary>
/// Which gateways a merchant is enrolled with.
/// <para>
/// Note that no endpoint here accepts a secret. Accounts carry a <c>credentialRef</c> that the
/// credential provider resolves out of configuration or a secret manager, so an API key never
/// travels through a request body, a request log or a database backup.
/// </para>
/// </summary>
[ApiController]
[Route("api/merchants/{merchantId:guid}/gateway-accounts")]
[Produces("application/json")]
public class MerchantGatewayAccountsController : ControllerBase
{
    private readonly IMerchantGatewayAccountService _accounts;

    public MerchantGatewayAccountsController(IMerchantGatewayAccountService accounts) => _accounts = accounts;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GatewayAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid merchantId)
    {
        var accounts = await _accounts.ListAsync(merchantId, HttpContext.RequestAborted);
        return Ok(accounts.Select(a => a.ToDto()).ToList());
    }

    /// <summary>
    /// Creates or replaces the account for this (merchant, gateway, mode). The credential
    /// reference is resolved before anything is saved, so a typo fails here rather than at a
    /// customer's checkout.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(GatewayAccountDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Upsert(Guid merchantId, UpsertGatewayAccountRequest request)
    {
        if (!Enum.TryParse<GatewayMode>(request.Mode, ignoreCase: true, out var mode))
            return BadRequest(new { code = "invalid_mode", detail = "Mode must be 'Test' or 'Live'." });

        var command = new UpsertGatewayAccountCommand(
            request.GatewayKey,
            mode,
            request.DisplayName,
            request.CredentialRef,
            request.WebhookSecretRef,
            request.IsEnabled,
            request.Priority,
            request.SupportedCurrencies);

        var result = await _accounts.UpsertAsync(merchantId, command, HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Account!.ToDto()) : Problem(result);
    }

    /// <summary>
    /// Stops the account taking new payments. Not a delete — existing payments still resolve it,
    /// because turning a gateway off is not the same as giving up the ability to refund what it
    /// already took.
    /// </summary>
    [HttpDelete("{accountId:guid}")]
    [ProducesResponseType(typeof(GatewayAccountDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Disable(Guid merchantId, Guid accountId)
    {
        var result = await _accounts.DisableAsync(merchantId, accountId, HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Account!.ToDto()) : Problem(result);
    }

    private IActionResult Problem(GatewayAccountResult result)
    {
        var (status, code) = result.Problem switch
        {
            GatewayAccountProblem.UnknownGateway => (StatusCodes.Status400BadRequest, "gateway_unknown"),
            GatewayAccountProblem.InvalidCurrency => (StatusCodes.Status400BadRequest, "invalid_currency"),
            GatewayAccountProblem.CredentialsNotResolvable => (StatusCodes.Status422UnprocessableEntity, "credentials_not_resolvable"),
            GatewayAccountProblem.NotFound => (StatusCodes.Status404NotFound, "account_not_found"),
            _ => (StatusCodes.Status500InternalServerError, "unexpected")
        };

        return StatusCode(status, new { code, detail = result.Detail });
    }
}
