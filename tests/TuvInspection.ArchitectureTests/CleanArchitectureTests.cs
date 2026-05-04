using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NetArchTest.Rules;
using TuvInspection.Domain.Common;
using TuvInspection.Infrastructure.Persistence;
using Xunit;

namespace TuvInspection.ArchitectureTests;

public class CleanArchitectureTests
{
    private const string Domain = "TuvInspection.Domain";
    private const string Application = "TuvInspection.Application";
    private const string Infrastructure = "TuvInspection.Infrastructure";
    private const string Api = "TuvInspection.Api";
    private const string Contracts = "TuvInspection.Contracts";

    private static readonly Assembly DomainAsm = typeof(Entity<>).Assembly;
    private static readonly Assembly ApplicationAsm = typeof(Application.Common.Cqrs.IDispatcher).Assembly;
    private static readonly Assembly InfrastructureAsm = typeof(AppDbContext).Assembly;

    [Fact]
    public void Domain_should_not_depend_on_infrastructure_or_api()
    {
        var result = Types.InAssembly(DomainAsm)
            .Should()
            .NotHaveDependencyOnAny(Infrastructure, Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Domain leaks: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Domain_should_not_depend_on_aspnetcore_or_efcore()
    {
        var result = Types.InAssembly(DomainAsm)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Domain has framework deps: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Application_should_not_depend_on_infrastructure_or_api()
    {
        var result = Types.InAssembly(ApplicationAsm)
            .Should()
            .NotHaveDependencyOnAny(Infrastructure, Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Application leaks: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Application_should_not_depend_on_efcore()
    {
        var result = Types.InAssembly(ApplicationAsm)
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Application leaks EF: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Contracts_should_be_pure_DTOs_with_no_app_dependencies()
    {
        var contractsAsm = typeof(Contracts.Auth.LoginRequest).Assembly;
        var result = Types.InAssembly(contractsAsm)
            .Should()
            .NotHaveDependencyOnAny(Application, Infrastructure, Api, Domain)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Contracts leaks: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    /// <summary>
    /// Every entity that implements <see cref="ITenantScoped"/> MUST have a global query filter
    /// configured in <see cref="AppDbContext"/>. This catches the data-leak vector where a new
    /// tenant-scoped aggregate is added but the developer forgets to wire the filter.
    /// </summary>
    [Fact]
    public void Every_tenant_scoped_aggregate_has_a_query_filter()
    {
        var tenantScoped = DomainAsm.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ITenantScoped).IsAssignableFrom(t))
            .ToList();

        tenantScoped.Should().NotBeEmpty(
            "the test only has value if there is at least one tenant-scoped aggregate");

        // Build a real DbContext model and check each tenant-scoped entity has a query filter.
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("arch-tests")
            .Options;
        using var ctx = new AppDbContext(options, new NoopTenantContext());

        var unfiltered = new List<string>();
        foreach (var type in tenantScoped)
        {
            var et = ctx.Model.FindEntityType(type);
            et.Should().NotBeNull($"{type.Name} should be mapped in AppDbContext");
            var hasFilter = et!.GetDeclaredQueryFilters().Any();
            if (!hasFilter) unfiltered.Add(type.Name);
        }

        unfiltered.Should().BeEmpty(
            $"these tenant-scoped types are missing a global query filter: {string.Join(", ", unfiltered)}");
    }

    private sealed class NoopTenantContext : Application.Common.ITenantContext
    {
        public bool IsAnonymous => true;
        public string? UserId => null;
        public string? UserName => null;
        public string? PrimaryRole => null;
        public IReadOnlySet<string> Roles { get; } = new HashSet<string>();
        public IReadOnlySet<Guid> AssignedClientIds { get; } = new HashSet<Guid>();
        public Guid? ActiveClientId => null;
        public string? IpAddress => null;
        public bool IsInRole(string role) => false;
    }
}
