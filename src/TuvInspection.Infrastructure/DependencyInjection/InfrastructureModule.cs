using System.Reflection;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Common.Documents;
using TuvInspection.Application.Common.Email;
using TuvInspection.Application.Common.Outbox;
using TuvInspection.Application.Common.Persistence;
using TuvInspection.Application.Common.Time;
using TuvInspection.Infrastructure.Assessments;
using TuvInspection.Infrastructure.Auditing;
using TuvInspection.Infrastructure.Cqrs;
using TuvInspection.Infrastructure.Email;
using TuvInspection.Infrastructure.Certificates;
using TuvInspection.Infrastructure.Equipment;
using TuvInspection.Infrastructure.JobManagement;
using TuvInspection.Infrastructure.Reports;
using TuvInspection.Infrastructure.Stickers;
using TuvInspection.Infrastructure.Identity;
using TuvInspection.Infrastructure.Outbox;
using TuvInspection.Infrastructure.Persistence;
using TuvInspection.Infrastructure.Storage;
using TuvInspection.Infrastructure.Tenancy;
using TuvInspection.Infrastructure.Time;

namespace TuvInspection.Infrastructure.DependencyInjection;

public static class InfrastructureModule
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        // Options
        services.Configure<JwtOptions>(config.GetSection("Jwt"));
        services.Configure<SmtpOptions>(config.GetSection("Smtp"));
        services.Configure<LocalDocumentStoreOptions>(config.GetSection("Documents"));

        // Tenancy + clock
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpTenantContext>();
        services.AddSingleton<IClock, SystemClock>();

        // Persistence
        var appConn = config.GetConnectionString("Application")
            ?? throw new InvalidOperationException("ConnectionStrings:Application is required");
        var auditConn = config.GetConnectionString("Audit") ?? appConn;

        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((sp, opt) =>
            opt.UseSqlServer(appConn, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
               .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));
        services.AddDbContext<AuditDbContext>((sp, opt) =>
            opt.UseSqlServer(auditConn, sql => sql.MigrationsAssembly(typeof(AuditDbContext).Assembly.FullName)));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        // Identity
        services.AddIdentityCore<ApplicationUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequireDigit = true;
                o.Password.RequireUppercase = true;
                o.Password.RequireLowercase = true;
                o.Password.RequireNonAlphanumeric = true;
                o.Password.RequiredLength = 12;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // JWT
        var jwtSection = config.GetSection("Jwt");
        var signingKey = jwtSection.GetValue<string>("SigningKey")
            ?? throw new InvalidOperationException("Jwt:SigningKey is required");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwtSection.GetValue<string>("Issuer"),
                    ValidAudience = jwtSection.GetValue<string>("Audience"),
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });
        services.AddAuthorization();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        // CQRS dispatcher + Scrutor scan for handlers in Application & Infrastructure
        services.AddScoped<IDispatcher, Dispatcher>();
        services.Scan(scan => scan
            .FromAssembliesOf(typeof(IDispatcher), typeof(InfrastructureModule))
            .AddClasses(c => c.AssignableTo(typeof(ICommandHandler<,>)))
                .AsImplementedInterfaces().WithScopedLifetime()
            .AddClasses(c => c.AssignableTo(typeof(IQueryHandler<,>)))
                .AsImplementedInterfaces().WithScopedLifetime());

        // FluentValidation: scan Application assembly for validators.
        services.AddValidatorsFromAssembly(
            Assembly.GetAssembly(typeof(IDispatcher)),
            ServiceLifetime.Scoped,
            includeInternalTypes: true);

        // Side-effect ports
        services.AddScoped<IOutbox, EfOutbox>();
        services.AddSingleton<IDocumentStore, LocalDocumentStore>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();

        // Outbox processor + handlers
        services.AddHostedService<OutboxProcessor>();
        services.AddScoped<IOutboxMessageHandler<ClientSentCertificateEmail>, ClientSentEmailHandler>();

        // Excel import service
        services.AddScoped<EquipmentImportService>();

        // Certificate number generator + PDF renderer
        services.AddScoped<CertificateNoGenerator>();
        services.AddSingleton<CertificatePdfRenderer>();

        // Stickers
        services.AddScoped<StickerNumberGenerator>();
        services.AddSingleton<QrCodeService>();
        services.AddSingleton<StickerPdfRenderer>();

        // Operator Assessment
        services.AddScoped<AssessmentNoGenerator>();
        services.AddScoped<CompetencyCardNoGenerator>();
        services.AddSingleton<CompetencyCardPdfRenderer>();

        // Job Management
        services.AddScoped<JobRequestNoGenerator>();
        services.AddScoped<JobOrderNoGenerator>();
        services.AddScoped<DwrNoGenerator>();
        services.AddScoped<SurveyNoGenerator>();

        // Reports + Aramco weekly export
        services.AddScoped<AramcoWeeklyExporter>();

        return services;
    }
}
