using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using GoldenCornOrder.Data;

var builder = WebApplication.CreateBuilder(args);

// Cloud environment PORT binding support
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// Configure Database (PostgreSQL on Cloud vs SQLite on Local)
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    string npgsqlConnStr;
    try
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':');
        var user = userInfo[0];
        var password = userInfo.Length > 1 ? userInfo[1] : "";
        var host = uri.Host;
        var portNum = uri.Port > 0 ? uri.Port : 5432;
        var dbName = uri.AbsolutePath.TrimStart('/');
        npgsqlConnStr = $"Host={host};Port={portNum};Database={dbName};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true";
    }
    catch
    {
        npgsqlConnStr = databaseUrl;
    }

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(npgsqlConnStr));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=goldencorn.db";
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(connectionString));
}

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// Ensure Database is created and seeded on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        DbInitializer.Initialize(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing and seeding the database.");
    }
}

app.UseCors("AllowAll");

// Serve static files from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.MapControllers();

// Fallback for Admin page and Customer page
app.MapGet("/admin", () => Results.Redirect("/admin.html"));
app.MapFallbackToFile("index.html");

app.Run();
