using Calubridi.Api.Data;
using Calubridi.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Calubridi.Api.Controllers;

[ApiController]
[Route("api/products/{productId:int}/videos")]
public class ProductVideosController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public ProductVideosController(
        ApplicationDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    // =========================================================
    // GET - Obtener videos del producto
    // =========================================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductVideo>>> GetVideos(
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

        var videos = await _context.ProductVideos
            .AsNoTracking()
            .Where(v => v.ProductId == productId)
            .OrderBy(v => v.DisplayOrder)
            .ThenBy(v => v.Id)
            .ToListAsync();

        return Ok(videos);
    }

    // =========================================================
    // POST - Subir video
    // =========================================================

    [HttpPost]
    public async Task<ActionResult<ProductVideo>> UploadVideo(
        int productId,
        IFormFile file,
        int displayOrder = 0)
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

        if (file is null || file.Length == 0)
        {
            return BadRequest(new
            {
                message = "Debe seleccionar un archivo."
            });
        }

        var videoExtensions = new[]
        {
            ".mp4",
            ".webm"
        };

        var extension = Path
            .GetExtension(file.FileName)
            .ToLowerInvariant();

        if (!videoExtensions.Contains(extension))
        {
            return BadRequest(new
            {
                message = "Formato no permitido. Se aceptan MP4 y WEBM."
            });
        }

        const long maxVideoSize = 100 * 1024 * 1024;

        if (file.Length > maxVideoSize)
        {
            return BadRequest(new
            {
                message = "El video no puede superar los 100 MB."
            });
        }

        var uploadsFolder = Path.Combine(
            _environment.ContentRootPath,
            "wwwroot",
            "uploads",
            "products",
            "videos"
        );

        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid()}{extension}";

        var filePath = Path.Combine(
            uploadsFolder,
            fileName
        );

        await using (var stream = new FileStream(
            filePath,
            FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var relativeUrl =
            $"/uploads/products/videos/{fileName}";

        var productVideo = new ProductVideo
        {
            ProductId = productId,
            Url = relativeUrl,
            DisplayOrder = displayOrder,
            CreatedAt = DateTime.UtcNow
        };

        _context.ProductVideos.Add(productVideo);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            throw;
        }

        return Created(
            $"/api/products/{productId}/videos/{productVideo.Id}",
            productVideo
        );
    }

    // =========================================================
    // DELETE - Eliminar video
    // =========================================================

    [HttpDelete("{videoId:int}")]
    public async Task<IActionResult> DeleteVideo(
        int productId,
        int videoId)
    {
        var video = await _context.ProductVideos
            .FirstOrDefaultAsync(v =>
                v.Id == videoId &&
                v.ProductId == productId);

        if (video is null)
        {
            return NotFound(new
            {
                message = "El video indicado no existe para este producto."
            });
        }

        var relativePath = video.Url
            .TrimStart('/')
            .Replace('/', Path.DirectorySeparatorChar);

        var filePath = Path.Combine(
            _environment.ContentRootPath,
            "wwwroot",
            relativePath
        );

        _context.ProductVideos.Remove(video);

        await _context.SaveChangesAsync();

        var remainingVideos = await _context.ProductVideos
            .Where(v => v.ProductId == productId)
            .OrderBy(v => v.DisplayOrder)
            .ThenBy(v => v.Id)
            .ToListAsync();

        for (int i = 0; i < remainingVideos.Count; i++)
        {
            remainingVideos[i].DisplayOrder = i + 1;
        }

        await _context.SaveChangesAsync();

        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }

        return NoContent();
    }
}