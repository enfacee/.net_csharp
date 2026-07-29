using EventApi.Application.Abstractions;
using EventApi.Application.Security;
using EventApi.Infrastructure.BackgroundServices;
using EventApi.Infrastructure.Persistence;
using EventApi.Infrastructure.Persistence.Repositories;
using EventApi.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("DefaultConnection connection string is not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.Configure<JwtOptions>(options =>
        {
            var section = configuration.GetSection(JwtOptions.SectionName);
            options.Secret = section[nameof(JwtOptions.Secret)] ?? string.Empty;
            options.Issuer = section[nameof(JwtOptions.Issuer)] ?? string.Empty;
            options.Audience = section[nameof(JwtOptions.Audience)] ?? string.Empty;

            if (int.TryParse(section[nameof(JwtOptions.LifetimeMinutes)], out var lifetimeMinutes))
                options.LifetimeMinutes = lifetimeMinutes;
        });
        services.AddSingleton<Sha256PasswordHasher>();
        services.AddScoped<JwtTokenGenerator>();
        services.AddScoped<IUserService, UserService>();
        services.AddHostedService<BookingProcessingBackgroundService>();

        return services;
    }

    public static void MigrateInfrastructureDatabase(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Database.IsRelational())
            db.Database.Migrate();
    }
}

