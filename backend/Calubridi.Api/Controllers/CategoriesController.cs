using Calubridi.Api.Data;
using Calubridi.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Calubridi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CategoriesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/categories
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Category>>> GetCategories()
    {
        var categories = await _context.Categories
            .OrderBy(c => c.Name)
            .ToListAsync();

        return Ok(categories);
    }

    // GET: api/categories/1
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Category>> GetCategory(int id)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category is null)
        {
            return NotFound();
        }

        return Ok(category);
    }

    // POST: api/categories
    [HttpPost]
    public async Task<ActionResult<Category>> CreateCategory(Category category)
    {
        category.Id = 0;
        category.CreatedAt = DateTime.UtcNow;

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetCategory),
            new { id = category.Id },
            category);
    }

    // PUT: api/categories/1
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateCategory(
        int id,
        Category updatedCategory)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category is null)
        {
            return NotFound();
        }

        category.Name = updatedCategory.Name;
        category.IsActive = updatedCategory.IsActive;

        await _context.SaveChangesAsync();

        return Ok(category);
    }

    // DELETE: api/categories/1
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
    var category = await _context.Categories
        .FirstOrDefaultAsync(c => c.Id == id);

    if (category is null)
    {
        return NotFound();
    }

    if (category.Name == "Otros")
    {
        return Conflict(new
        {
            message = "La categoría 'Otros' es la categoría por defecto y no puede eliminarse."
        });
    }

    var hasProducts = await _context.Products
        .AnyAsync(p => p.CategoryId == id);

    if (hasProducts)
    {
        return Conflict(new
        {
            message = "No se puede eliminar la categoría porque tiene productos asociados."
        });
    }

    _context.Categories.Remove(category);
    await _context.SaveChangesAsync();

    return NoContent();
    }
}