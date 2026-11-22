using Calubridi.Api.Data;
using Calubridi.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Calubridi.Api.Store;

internal static class StoreSetup
{
    public static string Currency(IConfiguration config)
    {
        var code = (config["Store:Currency"] ?? "ARS").ToUpperInvariant();
        if (code.Length != 3 || code.Any(c => c is < 'A' or > 'Z'))
            throw new InvalidOperationException("Store:Currency debe ser un código de moneda de tres letras.");
        return code;
    }

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config, IWebHostEnvironment environment)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync("Admin")) Check(await roles.CreateAsync(new IdentityRole("Admin")));

        var email = config["BootstrapAdmin:Email"];
        var password = config["BootstrapAdmin:Password"];
        if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<StoreUser>>();
            if (await users.FindByEmailAsync(email) is null)
            {
                await using var transaction = await db.Database.BeginTransactionAsync();
                var admin = new StoreUser { UserName = email, Email = email, EmailConfirmed = true };
                Check(await users.CreateAsync(admin, password));
                Check(await users.AddToRoleAsync(admin, "Admin"));
                await transaction.CommitAsync();
            }
        }

        if (environment.IsDevelopment() && config.GetValue<bool>("Store:SeedDemo") && !await db.Products.AnyAsync())
        {
            var category = new Category { Name = "Vestidos" };
            var examples = new[] {
                ("Vestido Carmín", "Una silueta amplia con mangas cortas y cintura marcada.", 75000m, 12, "producto-prueba.jpg"),
                ("Vestido Lunares", "Lunares clásicos y falda con vuelo.", 69000m, 10, "Prueba2.jpg"),
                ("Vestido Girasol", "Diseño ilustrado con tirantes finos y lazo en la cintura.", 64000m, 14, "Prueba3.jpg")
            };
            var folder = Path.Combine(environment.WebRootPath, "uploads", "products");
            Directory.CreateDirectory(folder);
            foreach (var (name, description, price, days, file) in examples)
            {
                System.IO.File.Copy(Path.Combine(environment.ContentRootPath, "test-files", file),
                    Path.Combine(folder, "demo-" + file), overwrite: true);
                db.Products.Add(new Product {
                    Name = name, Description = description, Price = price, EstimatedProductionDays = days,
                    Category = category, CoverUrl = "/uploads/products/demo-" + file
                });
            }
            await db.SaveChangesAsync();
        }
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
    }
}
