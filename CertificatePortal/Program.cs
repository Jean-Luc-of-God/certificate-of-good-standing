using Serilog;
using CertificatePortal.Repositories;
using CertificatePortal.Models;
using CertificatePortal.Services;
using CertificatePortal.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Configuration.AddEnvironmentVariables();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Global Antiforgery
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("SQL Server", () =>
    {
        try
        {
            var config = builder.Configuration;
            var connString = config.GetConnectionString("DefaultConnection");
            using var conn = new Microsoft.Data.SqlClient.SqlConnection(connString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.ExecuteScalar();
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database is unreachable", ex);
        }
    });

// Register AppSettings
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));

// Register Data Access Layer
builder.Services.AddScoped<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<ICertificateRepository, CertificateRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

// Register Services
builder.Services.AddScoped<ICertificateService, CertificateService>();

var app = builder.Build();

// 3. STARTUP DB TEST
if (!builder.Configuration.GetValue<bool>("UseMockData"))
{
    try 
    {
        using (var scope = app.Services.CreateScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using (var conn = await factory.CreateConnectionAsync())
            {
                // Note: We use Dapper's ExecuteScalar or similar
                using (var cmd = ((Microsoft.Data.SqlClient.SqlConnection)conn).CreateCommand())
                {
                    cmd.CommandText = "SELECT 1";
                    var result = cmd.ExecuteScalar();
                    app.Logger.LogInformation("DB connection startup test OK: {result}", result);
                }
            }
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex, "DB connection startup test FAILED.");
    }
}
else
{
    app.Logger.LogInformation("Skipping DB startup test: Running in Mock Mode.");
}

// Error Handling & Security
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// 404 Handling
app.UseStatusCodePagesWithReExecute("/Home/NotFound");

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

try
{
    Log.Information("Starting web host");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
