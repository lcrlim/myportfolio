using GrpcService.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// Load configuration
var configuration = builder.Configuration;

// Add gRPC services
builder.Services.AddGrpc(options =>
{
    options.MaxReceiveMessageSize = null; // No limit on message size
    options.MaxSendMessageSize = null;
    options.EnableDetailedErrors = true; // Detailed errors for debugging
});

// Add gRPC health checks
builder.Services.AddGrpcHealthChecks()
    .AddCheck("GreeterService", () => HealthCheckResult.Healthy());

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddConfiguration(configuration.GetSection("Logging"));
});

// Configure Kestrel
builder.WebHost.ConfigureKestrel((context, options) =>
{
    var kestrelConfig = context.Configuration.GetSection("Kestrel");
    options.Configure(kestrelConfig);

    // Load TLS certificate
    var certConfig = kestrelConfig.GetSection("Certificate");
    var certPath = certConfig["Path"];
    var certPassword = certConfig["Password"];
    if (!string.IsNullOrEmpty(certPath) && File.Exists(certPath))
    {
        options.ConfigureHttpsDefaults(https =>
        {
            https.ServerCertificate = new X509Certificate2(certPath, certPassword);
        });
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseRouting();
//app.UseOpenTelemetryPrometheusScrapingEndpoint(); // Prometheus metrics endpoint
app.MapGrpcService<GreeterService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.");
app.MapGrpcHealthChecksService();

// Graceful shutdown
app.Lifetime.ApplicationStopping.Register(() =>
{
    app.Logger.LogInformation("Application is shutting down...");
});

await app.RunAsync();
