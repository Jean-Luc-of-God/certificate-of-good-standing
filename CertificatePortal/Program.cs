using Serilog;
using CertificatePortal.Repositories;
using CertificatePortal.Models;
using CertificatePortal.Services;
using CertificatePortal.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PuppeteerSharp;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Configuration.AddEnvironmentVariables();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Health Checks
builder.Services.AddHealthChecks().AddCheck("Self", () => HealthCheckResult.Healthy());

builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
builder.Services.AddScoped<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<ICertificateRepository, CertificateRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<ICertificateService, CertificateService>();

var app = builder.Build();

// 3. STARTUP PREPARATION
var useMock = app.Configuration.GetValue<string>("UseMockData")?.ToLower() == "true";
if (useMock)
{
    Log.Information("Running in MOCK MODE - Skipping DB tests.");
}

// Pre-download Chromium for PDF generation so first user doesn't wait
try {
    Log.Information("Initializing PDF Engine...");
    await new BrowserFetcher().DownloadAsync();
    Log.Information("PDF Engine Ready.");
} catch (Exception ex) {
    Log.Error(ex, "Failed to initialize PDF engine.");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/NotFound");
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllerRoute(name: "default", pattern: "{controller=Certificate}/{action=Index}/{id?}");

try
{
    Log.Information("Starting AUCA Portal on port {Port}", Environment.GetEnvironmentVariable("PORT") ?? "8080");
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
