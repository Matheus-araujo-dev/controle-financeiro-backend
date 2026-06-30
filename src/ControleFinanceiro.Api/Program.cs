using ControleFinanceiro.Api.Configuration;
using ControleFinanceiro.Api.Extensions;
using ControleFinanceiro.Api.Middleware;
using ControleFinanceiro.Api.Swagger;
using ControleFinanceiro.Application;
using ControleFinanceiro.Application.Common.FeatureFlags;
using ControleFinanceiro.Infrastructure;
using ControleFinanceiro.Infrastructure.Persistence;
using ControleFinanceiro.SharedKernel.Logging;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

if (args.Length > 0)
{
    builder.Configuration.AddCommandLine(args);
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (connectionString?.Contains("${DB_PASSWORD}") == true)
{
    var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD")
        ?? throw new InvalidOperationException("A variável de ambiente DB_PASSWORD deve estar configurada para expandir ${DB_PASSWORD} na connection string.");
    builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString.Replace("${DB_PASSWORD}", dbPassword);
}

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ControleFinanceiro.Api")
    .Enrich.With<SensitiveDataEnricher>()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        new CompactJsonFormatter(),
        path: "logs/app-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        fileSizeLimitBytes: 10 * 1024 * 1024,
        rollOnFileSizeLimit: true)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<WhatsappWebhookOptions>(builder.Configuration.GetSection(WhatsappWebhookOptions.SectionName));
builder.Services.AddFeatureFlags(builder.Configuration);
builder.Services.AddObservability(builder.Configuration);
builder.Services.AddMemoryCache();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiFoundation(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    var rateLimitPolicies = RateLimitPolicies.Policies;

    options.AddPolicy(RateLimitPolicies.StrictPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(GetPartitionKey(context), _ => rateLimitPolicies[RateLimitPolicies.StrictPolicy]));

    options.AddPolicy(RateLimitPolicies.StandardPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(GetPartitionKey(context), _ => rateLimitPolicies[RateLimitPolicies.StandardPolicy]));

    options.AddPolicy(RateLimitPolicies.RelaxedPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(GetPartitionKey(context), _ => rateLimitPolicies[RateLimitPolicies.RelaxedPolicy]));

    options.AddPolicy(RateLimitPolicies.AiPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(GetPartitionKey(context), _ => rateLimitPolicies[RateLimitPolicies.AiPolicy]));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

static string GetPartitionKey(HttpContext httpContext)
{
    if (httpContext.User.Identity?.IsAuthenticated == true)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? httpContext.User.FindFirstValue("sub")
                     ?? httpContext.User.FindFirstValue("userId");
        var familiaId = httpContext.User.FindFirstValue("familiaId");

        if (!string.IsNullOrEmpty(userId))
        {
            // Partition per tenant+user to isolate rate limits across families.
            return string.IsNullOrEmpty(familiaId)
                ? $"user:{userId}"
                : $"familia:{familiaId}:user:{userId}";
        }
    }

    return httpContext.Connection.RemoteIpAddress?.ToString()
           ?? httpContext.Request.Headers.Host.ToString();
}
var healthChecks = builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["db", "postgres"])
    .AddCheck("self", () => HealthCheckResult.Healthy("API is running"), tags: ["self", "live"])
    .AddCheck("logging", () => HealthCheckResult.Healthy("Logging is available"), tags: ["logging", "infra"]);

healthChecks.AddCheck<DistributedCacheHealthCheck>("cache", tags: ["cache", "infra"]);
builder.Services.AddHostedService<ControleFinanceiro.Api.BackgroundServices.RecorrenciaMensalWorker>();
builder.Services.AddHostedService<ControleFinanceiro.Api.BackgroundServices.AtualizacaoStatusContasWorker>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Controle Financeiro API",
        Version = "v1",
        Description = "Bootstrap inicial do backend do controle financeiro."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Name = "Authorization",
        Description = "Use um token Bearer quando a API estiver configurada em modo JwtBearer/Auth0."
    });

    if (builder.Environment.IsDevelopment())
    {
        options.AddSecurityDefinition("DebugUser", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = builder.Configuration[$"{AuthOptions.SectionName}:{nameof(AuthOptions.DevelopmentUserHeader)}"]
                ?? new AuthOptions().DevelopmentUserHeader,
            Description = "Use o header de desenvolvimento para autenticar localmente quando a API estiver em modo Development."
        });
    }

    options.OperationFilter<AuthorizeOperationFilter>();
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(correlationId))
    {
        correlationId = Guid.NewGuid().ToString();
    }

    context.Items["CorrelationId"] = correlationId;
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        return Task.CompletedTask;
    });

    await next();
});

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("CorrelationId", httpContext.Items["CorrelationId"]?.ToString() ?? string.Empty);

        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub");
            var familiaId = httpContext.User.FindFirstValue("familiaId");

            if (userId is not null) diagnosticContext.Set("UserId", userId);
            if (familiaId is not null) diagnosticContext.Set("TenantId", familiaId);
        }
    };
});

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors(CorsOptions.PolicyName);

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    // CSP permite Google Sign-In e comunicação com Anthropic (usados pelo frontend e backend)
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' https://accounts.google.com/gsi/client; " +
        "frame-src https://accounts.google.com; " +
        "connect-src 'self' https://apis.google.com; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https://lh3.googleusercontent.com;");
    await next();
});

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
    app.UseHsts();
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        await next();
    });
}

app.UseAuthentication();
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseRateLimiter();
}
app.UseAuthorization();
app.UseMiddleware<TenantLoggingMiddleware>();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("db")
});
app.MapHealthChecks("/health/infra", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("infra")
});
app.MapControllers();

app.Run();

public partial class Program;
