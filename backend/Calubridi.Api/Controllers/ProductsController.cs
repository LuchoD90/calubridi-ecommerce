using Calubridi.Api.Data;
using Calubridi.Api.DTOs.Products;
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

    // GET: api/products
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductResponseDto>>> GetProducts()
    {
        var products = await _context.Products
            .AsNoTracking()
            .Select(p => new ProductResponseDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                EstimatedProductionDays = p.EstimatedProductionDays,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : string.Empty
            })
            .ToListAsync();

        return Ok(products);
    }

    // GET: api/products/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductResponseDto>> GetProductById(int id)
    {
        var product = await _context.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductResponseDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                EstimatedProductionDays = p.EstimatedProductionDays,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : string.Empty
            })
            .FirstOrDefaultAsync();

        if (product is null)
        {
            return NotFound();
        }

        return Ok(product);
    }

    // POST: api/products
    [HttpPost]
    public async Task<ActionResult<ProductResponseDto>> CreateProduct(
        CreateProductDto dto)
    {
        Category? category;

        if (dto.CategoryId.HasValue)
        {
            category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == dto.CategoryId.Value);

            if (category is null)
            {
                return BadRequest(new
                {
                    message = "La categoría indicada no existe."
                });
            }
        }
        else
        {
            category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Name == "Otros");

            if (category is null)
            {
                category = new Category
                {
                    Name = "Otros",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
            }
        }

        var product = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            EstimatedProductionDays = dto.EstimatedProductionDays,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CategoryId = category.Id
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var response = new ProductResponseDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            EstimatedProductionDays = product.EstimatedProductionDays,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
            CategoryId = category.Id,
            CategoryName = category.Name
        };

        return CreatedAtAction(
            nameof(GetProductById),
            new { id = product.Id },
            response
        );
    }

    // PUT: api/products/5
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProductResponseDto>> UpdateProduct(
        int id,
        UpdateProductDto dto)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null)
        {
            return NotFound();
        }

        Category? category;

        if (dto.CategoryId.HasValue)
        {
            category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == dto.CategoryId.Value);

            if (category is null)
            {
                return BadRequest(new
                {
                    message = "La categoría indicada no existe."
                });
            }
        }
        else
    {
    category = await _context.Categories
        .FirstOrDefaultAsync(c => c.Id == product.CategoryId);

    if (category is null)
    {
        return BadRequest(new
        {
            message = "La categoría actual del producto no existe."
        });
    }
    }

        product.Name = dto.Name;
        product.Description = dto.Description;
        product.Price = dto.Price;
        product.EstimatedProductionDays = dto.EstimatedProductionDays;
        product.IsActive = dto.IsActive;
        product.CategoryId = category.Id;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var response = new ProductResponseDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            EstimatedProductionDays = product.EstimatedProductionDays,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
            CategoryId = category.Id,
            CategoryName = category.Name
        };

        return Ok(response);
    }

    // DELETE: api/products/5
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