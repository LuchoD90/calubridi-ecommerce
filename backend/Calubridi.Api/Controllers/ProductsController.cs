using Calubridi.Api.Data;
using Calubridi.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Calubridi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProductsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts()
    {
        var products = await _context.Products
            .AsNoTracking()
            .ToListAsync();

        return Ok(products);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetProductById(int id)
    {
    var product = await _context.Products
        .AsNoTracking()
        .FirstOrDefaultAsync(p => p.Id == id);

    if (product is null)
    {
        return NotFound();
    }

    return Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct(Product product)
    {
    product.Id = 0;
    product.CreatedAt = DateTime.UtcNow;
    product.UpdatedAt = null;
    product.IsActive = true;

    _context.Products.Add(product);

    await _context.SaveChangesAsync();

    return CreatedAtAction(
        nameof(GetProductById),
        new { id = product.Id },
        product
    );
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateProduct(int id, Product updatedProduct)
    {
    var product = await _context.Products
        .FirstOrDefaultAsync(p => p.Id == id);

    if (product is null)
    {
        return NotFound();
    }

    product.Name = updatedProduct.Name;
    product.Description = updatedProduct.Description;
    product.Price = updatedProduct.Price;
    product.EstimatedProductionDays = updatedProduct.EstimatedProductionDays;
    product.IsActive = updatedProduct.IsActive;
    product.UpdatedAt = DateTime.UtcNow;

    await _context.SaveChangesAsync();

    return Ok(product);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
    var product = await _context.Products
        .FirstOrDefaultAsync(p => p.Id == id);

    if (product is null)
    {
        return NotFound();
    }

    _context.Products.Remove(product);

    await _context.SaveChangesAsync();

    return NoContent();
    }
}