using Calubridi.Api.Data;
using Calubridi.Api.Models;
using Calubridi.Api.DTOs.ProductFabrics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Calubridi.Api.Controllers;

[ApiController]
[Route("api/products/{productId:int}/fabrics")]
public class ProductFabricsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProductFabricsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/products/7/fabrics
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductFabricResponseDto>>> GetProductFabrics(
    int productId)
    {
    var productExists = await _context.Products
        .AnyAsync(p => p.Id == productId);

    if (!productExists)
    {
        return NotFound(new
        {
            message = "El producto indicado no existe."
        });
    }

    var fabrics = await _context.ProductFabrics
        .AsNoTracking()
        .Where(pf => pf.ProductId == productId)
        .OrderByDescending(pf => pf.IsDefault)
        .ThenBy(pf => pf.Fabric!.Name)
        .Select(pf => new ProductFabricResponseDto
        {
            Id = pf.Id,
            ProductId = pf.ProductId,
            FabricId = pf.FabricId,
            FabricName = pf.Fabric!.Name,
            Price = pf.Price,
            IsDefault = pf.IsDefault,
            IsActive = pf.IsActive
        })
        .ToListAsync();

    return Ok(fabrics);
    }   

    // POST: api/products/7/fabrics
    [HttpPost]
    public async Task<IActionResult> AddFabricToProduct(
    int productId,
    CreateProductFabricDto request)
    {
    var productExists = await _context.Products
        .AnyAsync(p => p.Id == productId);

    if (!productExists)
    {
        return NotFound(new
        {
            message = "El producto indicado no existe."
        });
    }

    var fabricExists = await _context.Fabrics
        .AnyAsync(f => f.Id == request.FabricId);

    if (!fabricExists)
    {
        return BadRequest(new
        {
            message = "La tela indicada no existe."
        });
    }

    var alreadyExists = await _context.ProductFabrics
        .AnyAsync(pf =>
            pf.ProductId == productId &&
            pf.FabricId == request.FabricId);

    if (alreadyExists)
    {
        return Conflict(new
        {
            message = "La tela ya está asociada a este producto."
        });
    }

    if (request.IsDefault)
    {
        var currentDefaults = await _context.ProductFabrics
            .Where(pf =>
                pf.ProductId == productId &&
                pf.IsDefault)
            .ToListAsync();

        foreach (var item in currentDefaults)
        {
            item.IsDefault = false;
        }
    }

    var productFabric = new ProductFabric
    {
        ProductId = productId,
        FabricId = request.FabricId,
        Price = request.Price,
        IsDefault = request.IsDefault,
        IsActive = request.IsActive
    };

    _context.ProductFabrics.Add(productFabric);
    await _context.SaveChangesAsync();

    var fabric = await _context.Fabrics
        .AsNoTracking()
        .FirstAsync(f => f.Id == productFabric.FabricId);

    var response = new ProductFabricResponseDto
    {
        Id = productFabric.Id,
        ProductId = productFabric.ProductId,
        FabricId = productFabric.FabricId,
        FabricName = fabric.Name,
        Price = productFabric.Price,
        IsDefault = productFabric.IsDefault,
        IsActive = productFabric.IsActive
    };

    return Created(
        $"/api/products/{productId}/fabrics/{productFabric.Id}",
        response
    );
    }

    // PUT: api/products/7/fabrics/1
    [HttpPut("{productFabricId:int}")]
    public async Task<IActionResult> UpdateProductFabric(
    int productId,
    int productFabricId,
    UpdateProductFabricDto request)
    {
    var productFabric = await _context.ProductFabrics
        .FirstOrDefaultAsync(pf =>
            pf.Id == productFabricId &&
            pf.ProductId == productId);

    if (productFabric is null)
    {
        return NotFound(new
        {
            message = "La tela asociada al producto no existe."
        });
    }

    if (request.IsDefault)
    {
        var currentDefaults = await _context.ProductFabrics
            .Where(pf =>
                pf.ProductId == productId &&
                pf.Id != productFabricId &&
                pf.IsDefault)
            .ToListAsync();

        foreach (var item in currentDefaults)
        {
            item.IsDefault = false;
        }
    }

    productFabric.Price = request.Price;
    productFabric.IsDefault = request.IsDefault;
    productFabric.IsActive = request.IsActive;

    await _context.SaveChangesAsync();

    var fabric = await _context.Fabrics
        .AsNoTracking()
        .FirstAsync(f => f.Id == productFabric.FabricId);

    var response = new ProductFabricResponseDto
    {
        Id = productFabric.Id,
        ProductId = productFabric.ProductId,
        FabricId = productFabric.FabricId,
        FabricName = fabric.Name,
        Price = productFabric.Price,
        IsDefault = productFabric.IsDefault,
        IsActive = productFabric.IsActive
    };

    return Ok(response);
    }

    // DELETE: api/products/7/fabrics/1
    [HttpDelete("{productFabricId:int}")]
    public async Task<IActionResult> DeleteProductFabric(
        int productId,
        int productFabricId)
    {
        var productFabric = await _context.ProductFabrics
            .FirstOrDefaultAsync(pf =>
                pf.Id == productFabricId &&
                pf.ProductId == productId);

        if (productFabric is null)
        {
            return NotFound(new
            {
                message = "La tela asociada al producto no existe."
            });
        }

        var wasDefault = productFabric.IsDefault;

        _context.ProductFabrics.Remove(productFabric);
        await _context.SaveChangesAsync();

        if (wasDefault)
        {
            var nextFabric = await _context.ProductFabrics
                .Where(pf =>
                    pf.ProductId == productId &&
                    pf.IsActive)
                .OrderBy(pf => pf.Id)
                .FirstOrDefaultAsync();

            if (nextFabric is not null)
            {
                nextFabric.IsDefault = true;
                await _context.SaveChangesAsync();
            }
        }

        return NoContent();
    }
}