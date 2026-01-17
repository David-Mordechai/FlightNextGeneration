using Bff.Service.Hubs;
using Bff.Service.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Bff.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController(IHubContext<FlightHub> hubContext) : ControllerBase
{
    [HttpPost("entity-update")]
    public async Task<IActionResult> PostEntityUpdate([FromBody] EntityUpdateDto update)
    {
        await hubContext.Clients.All.SendAsync("EntityUpdateReceived", update);
        return Ok();
    }
}
