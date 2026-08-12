using System.Text.Json.Serialization;
using EventApi.Users.Application;
using EventApi.Users.Infrastructure;
using EventApi.Users.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddSwaggerGen();
builder.Services.AddUsersApplicationServices(builder.Configuration);
builder.Services.AddUsersInfrastructureServices(builder.Configuration);
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var app = builder.Build();

app.Services.MigrateUsersDatabase();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();
