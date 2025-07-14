using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using MyOpenId;
using Serilog;
using Serilog.Events;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

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
//builder.Services.AddSwaggerGen(options =>
//{
//    // Basic 인증 정의 추가
//    options.AddSecurityDefinition("Basic", new OpenApiSecurityScheme
//    {
//        Name = "Authorization",
//        Type = SecuritySchemeType.Http,
//        Scheme = "Basic",
//        In = ParameterLocation.Header,
//        Description = "Enter 'Basic' followed by a space and your Base64-encoded username:password (e.g., Basic dXNlcm5hbWU6cGFzc3dvcmQ=)"
//    });

//    // API 엔드포인트에 보안 요구 사항 추가
//    options.AddSecurityRequirement(new OpenApiSecurityRequirement
//    {
//        {
//            new OpenApiSecurityScheme
//            {
//                Reference = new OpenApiReference
//                {
//                    Type = ReferenceType.SecurityScheme,
//                    Id = "Basic"
//                }
//            },
//            new string[] { }
//        }
//    });
//});

builder.Services.AddSwaggerGen(options =>
{
    // Bearer 인증 정의
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' followed by a space and the JWT token."
    });

    // Basic 인증 정의
    options.AddSecurityDefinition("Basic", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Basic",
        In = ParameterLocation.Header,
        Description = "Enter 'Basic' followed by a space and Base64-encoded username:password (e.g., Basic dXNlcm5hbWU6cGFzc3dvcmQ=)"
    });

    // API 엔드포인트에 보안 요구 사항 추가
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Basic"
                }
            },
            new string[] { }
        }
    });
});

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

string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (connectionString == null)
{
    throw new Exception("connection string is null");
}

builder.Services.AddMyOpenId(() => connectionString);

builder.Services.AddDbContext<MyOpenIdDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")
     ?? throw new InvalidOperationException("Connection string 'database' not found.")));

var app = builder.Build();

app.UseSerilogRequestLogging();

// 환경 변수로 Swagger 활성화 제어
var enableSwagger = app.Configuration.GetValue<bool>("ENABLE_SWAGGER") ||
                   app.Environment.IsDevelopment();

if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Web Service API V1");
        c.RoutePrefix = "swagger";
    });

    Log.Information("Enable swagger");
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();


// Initialize database and tables
try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<MyOpenIdDbContext>();
        //await dbContext.Database.EnsureCreatedAsync();
        //Log.Information("my open id database ensure success");
        await dbContext.Database.MigrateAsync();
        Log.Information("MyOpenId database migrate success");
    }
}
catch (Exception e)
{
    Log.Error($"MyOpenId database migrate failed - {e.Message}");
}

app.Run();
