using C4IEntities.Data;
using C4IEntities.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace C4IEntities.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NoFlyZonesController : ControllerBase
{
    private readonly C4IDbContext _context;

    public NoFlyZonesController(C4IDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NoFlyZone>>> GetNoFlyZones()
    {
        return await _context.NoFlyZones.ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<NoFlyZone>> CreateNoFlyZone(NoFlyZone noFlyZone)
    {
        if (noFlyZone.Id == Guid.Empty)
        {
            noFlyZone.Id = Guid.NewGuid();
        }

        // Ensure geometry has correct SRID (4326 for GPS)
        if (noFlyZone.Geometry != null && noFlyZone.Geometry.SRID != 4326)
        {
            noFlyZone.Geometry.SRID = 4326;
        }

        _context.NoFlyZones.Add(noFlyZone);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetNoFlyZones), new { id = noFlyZone.Id }, noFlyZone);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateNoFlyZone(Guid id, NoFlyZone noFlyZone)
    {
        if (id != noFlyZone.Id)
        {
            return BadRequest();
        }

        if (noFlyZone.Geometry != null && noFlyZone.Geometry.SRID != 4326)
        {
            noFlyZone.Geometry.SRID = 4326;
        }

        _context.Entry(noFlyZone).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.NoFlyZones.Any(e => e.Id == id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNoFlyZone(Guid id)
    {
        var noFlyZone = await _context.NoFlyZones.FindAsync(id);
        if (noFlyZone == null)
        {
            return NotFound();
        }

        _context.NoFlyZones.Remove(noFlyZone);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
