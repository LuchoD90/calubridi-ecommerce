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

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config)
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

        if (config.GetValue<bool>("Store:SeedDemo") && !await db.Products.AnyAsync())
        {
            var printed = new Fabric { Name = "Gabardina estampada" };
            var plain = new Fabric { Name = "Gabardina lisa" };
            var natural = new Color { Name = "Natural", HexCode = "#B78D72" };
            var chocolate = new Color { Name = "Chocolate", HexCode = "#593C34" };
            var bag = new Product
            {
                Name = "Carterita estampada", Description = "Carterita de tela con correa. Diseño de muestra.",
                Price = 22000m, EstimatedProductionDays = 7, Category = new Category { Name = "Carteras y bolsos" },
                CoverUrl = "/assets/carterita.webp",
                Fabrics = [
                    new ProductFabric { Fabric = printed, Price = 22000m, IsDefault = true,
                        Colors = [new ProductFabricColor { Color = natural, IsDefault = true }] },
                    new ProductFabric { Fabric = plain, Price = 20500m,
                        Colors = [new ProductFabricColor { Color = chocolate, IsDefault = true }] }
                ]
            };
            var wallet = new Product
            {
                Name = "Monedero con broches", Description = "Monedero compacto con dos broches. Diseño de muestra.",
                Price = 9500m, EstimatedProductionDays = 5, Category = new Category { Name = "Accesorios" },
                CoverUrl = "/assets/monedero.webp"
            };
            db.Products.AddRange(bag, wallet);
            await db.SaveChangesAsync();
        }
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
    }
}
