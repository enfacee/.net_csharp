using EventApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EventApi.Tests;

public sealed class EventApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly InMemoryDatabaseRoot _databaseRoot = new();
    private readonly string _databaseName = Guid.NewGuid().ToString();

    public EventApiWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Host=localhost;Database=eventapi-tests");
        Environment.SetEnvironmentVariable("Jwt__Secret", "test-secret-with-at-least-32-bytes");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "EventApi.Tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "EventApi.Tests");
        Environment.SetEnvironmentVariable("Jwt__LifetimeMinutes", "60");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=eventapi-tests",
                ["Jwt:Secret"] = "test-secret-with-at-least-32-bytes",
                ["Jwt:Issuer"] = "EventApi.Tests",
                ["Jwt:Audience"] = "EventApi.Tests",
                ["Jwt:LifetimeMinutes"] = "60"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            var dbContextOptionsConfigurations = services
                .Where(descriptor =>
                    descriptor.ServiceType.IsGenericType &&
                    descriptor.ServiceType.GetGenericTypeDefinition().Name == "IDbContextOptionsConfiguration`1" &&
                    descriptor.ServiceType.GenericTypeArguments[0] == typeof(AppDbContext))
                .ToArray();

            foreach (var descriptor in dbContextOptionsConfigurations)
            {
                services.Remove(descriptor);
            }

            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<IHostedService>();

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName, _databaseRoot));
        });
    }
}
