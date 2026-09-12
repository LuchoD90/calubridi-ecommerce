using Calubridi.Api.Data;
using Calubridi.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Calubridi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ColorsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ColorsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Color>>> GetColors()
    {
        var colors = await _context.Colors
            .OrderBy(c => c.Name)
            .ToListAsync();

        return Ok(colors);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Color>> GetColor(int id)
    {
        var color = await _context.Colors
            .FirstOrDefaultAsync(c => c.Id == id);

        if (color is null)
        {
            return NotFound(new
            {
                message = "El color indicado no existe."
            });
        }

        return Ok(color);
    }

    [HttpPost]
    public async Task<ActionResult<Color>> CreateColor(Color color)
    {
        color.Id = 0;
        color.CreatedAt = DateTime.UtcNow;

        _context.Colors.Add(color);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetColor),
            new { id = color.Id },
            color
        );
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateColor(
        int id,
        Color updatedColor)
    {
        var color = await _context.Colors
            .FirstOrDefaultAsync(c => c.Id == id);

        if (color is null)
        {
            return NotFound(new
            {
                message = "El color indicado no existe."
            });
        }

        color.Name = updatedColor.Name;
        color.HexCode = updatedColor.HexCode;
        color.IsActive = updatedColor.IsActive;

        await _context.SaveChangesAsync();

        return Ok(color);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteColor(int id)
    {
        var color = await _context.Colors
            .FirstOrDefaultAsync(c => c.Id == id);

        if (color is null)
        {
            return NotFound(new
            {
                message = "El color indicado no existe."
            });
        }

        var isUsed = await _context.ProductFabricColors
            .AnyAsync(pfc => pfc.ColorId == id);

        if (isUsed)
        {
            return Conflict(new
            {
                message = "No se puede eliminar el color porque está asociado a uno o más productos."
            });
        }

        _context.Colors.Remove(color);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}