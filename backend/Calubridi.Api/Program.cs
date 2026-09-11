using Calubridi.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Configuración de Entity Framework Core con PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/api/test", () =>
{
    return Results.Ok(new
    {
        message = "Calubridi API funcionando correctamente, Hola Ale te amo"
    });
});

app.MapGet("/api/database/test", async (ApplicationDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();

    return Results.Ok(new
    {
        database = "calubridi_db",
        connected = canConnect
    });
});

app.UseStaticFiles();
app.MapControllers();
app.Run();