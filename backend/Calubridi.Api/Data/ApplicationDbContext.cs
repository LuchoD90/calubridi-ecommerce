using Calubridi.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Calubridi.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products { get; set; }
}