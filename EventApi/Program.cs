using System.Text;
using System.Text.Json.Serialization;
using EventApi.Application;
using EventApi.Application.Security;
using EventApi.Infrastructure;
using EventApi.Presentation.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT token."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document, null),
            []
        }
    });
});
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var app = builder.Build();

app.Services.MigrateInfrastructureDatabase();

app.UseHttpsRedirection();
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();

internal static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = GetJwtOptions(configuration);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret))
                };
            });

        return services;
    }

    private static JwtOptions GetJwtOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection(JwtOptions.SectionName);
        var jwtOptions = new JwtOptions
        {
            Secret = section[nameof(JwtOptions.Secret)] ?? string.Empty,
            Issuer = section[nameof(JwtOptions.Issuer)] ?? string.Empty,
            Audience = section[nameof(JwtOptions.Audience)] ?? string.Empty
        };

        if (int.TryParse(section[nameof(JwtOptions.LifetimeMinutes)], out var lifetimeMinutes))
            jwtOptions.LifetimeMinutes = lifetimeMinutes;

        ValidateJwtOptions(jwtOptions);

        return jwtOptions;
    }

    private static void ValidateJwtOptions(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Secret))
            throw new InvalidOperationException("Jwt:Secret is not configured.");

        if (Encoding.UTF8.GetByteCount(options.Secret) < 32)
            throw new InvalidOperationException("Jwt:Secret must be at least 32 bytes long.");

        if (string.IsNullOrWhiteSpace(options.Issuer))
            throw new InvalidOperationException("Jwt:Issuer is not configured.");

        if (string.IsNullOrWhiteSpace(options.Audience))
            throw new InvalidOperationException("Jwt:Audience is not configured.");
    }
}
