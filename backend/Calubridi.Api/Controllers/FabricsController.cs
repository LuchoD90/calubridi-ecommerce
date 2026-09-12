using Calubridi.Api.Data;
using Calubridi.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Calubridi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FabricsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public FabricsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Fabric>>> GetFabrics()
    {
        var fabrics = await _context.Fabrics
            .OrderBy(f => f.Name)
            .ToListAsync();

        return Ok(fabrics);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Fabric>> GetFabric(int id)
    {
        var fabric = await _context.Fabrics
            .FirstOrDefaultAsync(f => f.Id == id);

        if (fabric is null)
        {
            return NotFound(new
            {
                message = "La tela indicada no existe."
            });
        }

        return Ok(fabric);
    }

    [HttpPost]
    public async Task<ActionResult<Fabric>> CreateFabric(Fabric fabric)
    {
        fabric.Id = 0;
        fabric.CreatedAt = DateTime.UtcNow;

        _context.Fabrics.Add(fabric);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetFabric),
            new { id = fabric.Id },
            fabric
        );
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateFabric(
        int id,
        Fabric updatedFabric)
    {
        var fabric = await _context.Fabrics
            .FirstOrDefaultAsync(f => f.Id == id);

        if (fabric is null)
        {
            return NotFound(new
            {
                message = "La tela indicada no existe."
            });
        }

        fabric.Name = updatedFabric.Name;
        fabric.IsActive = updatedFabric.IsActive;

        await _context.SaveChangesAsync();

        return Ok(fabric);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteFabric(int id)
    {
        var fabric = await _context.Fabrics
            .FirstOrDefaultAsync(f => f.Id == id);

        if (fabric is null)
        {
            return NotFound(new
            {
                message = "La tela indicada no existe."
            });
        }

        var isUsed = await _context.ProductFabrics
            .AnyAsync(pf => pf.FabricId == id);

        if (isUsed)
        {
            return Conflict(new
            {
                message = "No se puede eliminar la tela porque está asociada a uno o más productos."
            });
        }

        _context.Fabrics.Remove(fabric);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}