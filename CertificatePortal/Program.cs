using Serilog;
using CertificatePortal.Repositories;
using CertificatePortal.Models;
using CertificatePortal.Services;
using CertificatePortal.Helpers;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// FORCE CONFIG LOAD
builder.Configuration.AddEnvironmentVariables();

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// REGISTER SERVICES
builder.Services.AddControllersWithViews();
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
builder.Services.AddScoped<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<ICertificateRepository, CertificateRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<ICertificateService, CertificateService>();

var app = builder.Build();

// NUCLEAR FIX: Always show detailed errors in this test phase
app.UseDeveloperExceptionPage(); 

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Simplified Route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Certificate}/{action=Index}/{id?}");

Log.Information(">>> AUCA PORTAL STARTING UP...");
Log.Information(">>> UseMockData: {Mock}", builder.Configuration["UseMockData"]);

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");
