using GentleStore.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GentleStore.Api.Controllers;

[ApiController]
[Authorize(Policy = Policies.StoreMember)]
public abstract class BackofficeControllerBase : ControllerBase
{
    protected IActionResult Forbidden() => StatusCode(StatusCodes.Status403Forbidden);
}
