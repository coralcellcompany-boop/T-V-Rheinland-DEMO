using Microsoft.AspNetCore.Mvc;
using NSwag;
using NSwag.Generation.Processors.Security;
using Serilog;
using TuvInspection.Api.Middleware;
using TuvInspection.Infrastructure.DependencyInjection;
using TuvInspection.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

// ---- Logging (Serilog) ----
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14));

// ---- Services ----
builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(o =>
{
    o.SuppressMapClientErrors = false;
});

builder.Services.AddProblemDetails();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IdentitySeeder>();

// CORS for the Angular dev server
const string SpaCors = "Spa";
builder.Services.AddCors(o => o.AddPolicy(SpaCors, p =>
    p.WithOrigins("http://localhost:4200", "http://localhost:4201")
     .AllowAnyHeader()
     .AllowAnyMethod()
     .WithExposedHeaders("X-Active-Client")));

// OpenAPI + Swagger UI (NSwag)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(cfg =>
{
    cfg.Title = "TUV Inspection API";
    cfg.Version = "v1";
    cfg.AddSecurity("Bearer", Enumerable.Empty<string>(), new OpenApiSecurityScheme
    {
        Type = OpenApiSecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste a JWT here (Bearer token from /api/auth/login)."
    });
    cfg.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("Bearer"));
});

var app = builder.Build();

// ---- Pipeline ----
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger UI exposed in all environments — this is an internal API for known users.
app.UseOpenApi();
app.UseSwaggerUi(s => s.Path = "/swagger");

app.UseCors(SpaCors);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ---- First-run seeding ----
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
    await seeder.SeedAsync(CancellationToken.None);
}

app.Run();

// Required for WebApplicationFactory in integration tests.
public partial class Program { }
