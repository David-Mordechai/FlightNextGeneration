using C4IEntities.Data;
using C4IEntities.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace C4IEntities.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NoFlyZonesController(C4IDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NoFlyZone>>> GetNoFlyZones()
    {
        return await context.NoFlyZones.ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<NoFlyZone>> CreateNoFlyZone(NoFlyZone noFlyZone)
    {
        if (noFlyZone.Id == Guid.Empty)
        {
            noFlyZone.Id = Guid.NewGuid();
        }

        // Ensure geometry has correct SRID (4326 for GPS) and is 2D
        noFlyZone.Geometry.SRID = 4326;
        Make2D(noFlyZone.Geometry);

        context.NoFlyZones.Add(noFlyZone);
        await context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetNoFlyZones), new { id = noFlyZone.Id }, noFlyZone);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateNoFlyZone(Guid id, NoFlyZone noFlyZone)
    {
        if (id != noFlyZone.Id)
        {
            return BadRequest();
        }

        noFlyZone.Geometry.SRID = 4326;
        Make2D(noFlyZone.Geometry);

        context.Entry(noFlyZone).State = EntityState.Modified;

        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!context.NoFlyZones.Any(e => e.Id == id))
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

    private void Make2D(NetTopologySuite.Geometries.Geometry geometry)
    {
        if (geometry == null) return;
        foreach (var coordinate in geometry.Coordinates)
        {
            try
            {
                coordinate.Z = NetTopologySuite.Geometries.Coordinate.NullOrdinate;
            }
            catch (InvalidOperationException)
            {
                // Coordinate does not support Z, which means it is already 2D.
            }
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNoFlyZone(Guid id)
    {
        var noFlyZone = await context.NoFlyZones.FindAsync(id);
        if (noFlyZone == null)
        {
            return NotFound();
        }

        context.NoFlyZones.Remove(noFlyZone);
        await context.SaveChangesAsync();

        return NoContent();
    }
}
