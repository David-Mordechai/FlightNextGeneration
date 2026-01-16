using C4IEntities.Models;
using C4IEntities.Services;
using Microsoft.AspNetCore.Mvc;

namespace C4IEntities.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RouteController(PathFindingService pathFindingService) : ControllerBase
{
    [HttpPost("calculate")]
    public async Task<ActionResult<RouteResponse>> CalculatePath([FromBody] RouteRequest request)
    {
        try
        {
            var result = await pathFindingService.CalculateOptimalPath(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest($"Failed to calculate path: {ex.Message}");
        }
    }
}
