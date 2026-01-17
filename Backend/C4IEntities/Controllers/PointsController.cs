using C4IEntities.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Point = C4IEntities.Models.Point;

namespace C4IEntities.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PointsController(C4IDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Point>>> GetPoints()
    {
        return await context.Points.ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<Point>> CreatePoint(Point point)
    {
        // Ensure geometry has SRID 4326
        if (point.Location != null)
        {
            point.Location.SRID = 4326;
            try
            {            
                point.Location.Coordinate.Z = NetTopologySuite.Geometries.Coordinate.NullOrdinate;
            }
            catch (InvalidOperationException)
            {
                // Already 2D
            }
        }

        context.Points.Add(point);
        await context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPoints), new { id = point.Id }, point);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePoint(Guid id)
    {
        var point = await context.Points.FindAsync(id);
        if (point == null)
        {
            return NotFound();
        }

        context.Points.Remove(point);
        await context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePoint(Guid id, Point point)
    {
        if (id != point.Id)
        {
            return BadRequest();
        }

        // Ensure geometry has SRID 4326 and is 2D
        if (point.Location != null)
        {
            point.Location.SRID = 4326;
            try
            {            
                point.Location.Coordinate.Z = NetTopologySuite.Geometries.Coordinate.NullOrdinate;
            }
            catch (InvalidOperationException)
            {
                // Already 2D
            }
        }

        context.Entry(point).State = EntityState.Modified;

        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!PointExists(id))
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

    private bool PointExists(Guid id)
    {
        return context.Points.Any(e => e.Id == id);
    }
}