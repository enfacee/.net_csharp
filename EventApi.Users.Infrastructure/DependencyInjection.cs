using EventApi.Users.Application.Abstractions;
using EventApi.Users.Infrastructure.Persistence;
using EventApi.Users.Infrastructure.Persistence.Repositories;
using EventApi.Users.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventApi.Users.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddUsersInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("DefaultConnection connection string is not configured.");

        services.AddDbContext<UsersDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, Sha256PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        return services;
    }

    public static void MigrateUsersDatabase(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();

        if (db.Database.IsRelational())
            db.Database.Migrate();
    }
}
