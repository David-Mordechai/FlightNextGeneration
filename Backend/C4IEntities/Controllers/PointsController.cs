using C4IEntities.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Point = C4IEntities.Models.Point;
using NetTopologySuite.Geometries;
using C4IEntities.Models;

namespace C4IEntities.Controllers;

public record PointDto(string Name, double Lat, double Lng, PointType Type);

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
    public async Task<ActionResult<Point>> CreatePoint(PointDto dto)
    {
        var point = new Point
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Type = dto.Type,
            Location = new NetTopologySuite.Geometries.Point(dto.Lng, dto.Lat) { SRID = 4326 }
        };

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
    public async Task<IActionResult> UpdatePoint(Guid id, PointDto dto)
    {
        var point = await context.Points.FindAsync(id);
        if (point == null) return NotFound();

        point.Name = dto.Name;
        point.Type = dto.Type;
        point.Location = new NetTopologySuite.Geometries.Point(dto.Lng, dto.Lat) { SRID = 4326 };

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