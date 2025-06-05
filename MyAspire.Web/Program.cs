using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Serilog;
using Serilog.Events;
using MyOpenId;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServiceDefaults();

// Serilog 설정
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(path: "logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

// Serilog을 ASP.NET Core 로깅에 통합
builder.Host.UseSerilog();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Rate Limiter 서비스 등록
builder.Services.AddRateLimiter(limiterOptions =>
{
    limiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Fixed Window Limiter 정책 추가
    limiterOptions.AddFixedWindowLimiter(policyName: "fixed_100_1sec", options =>
    {
        options.PermitLimit = 1; // 1초당 100회 제한
        options.Window = TimeSpan.FromSeconds(1);
        options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        options.QueueLimit = 0; // 큐잉 비활성화 (초과 요청은 즉시 거부)
    });
});

builder.Services.AddMyOpenId(() => "connection string");

var app = builder.Build();

app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

app.Run();
