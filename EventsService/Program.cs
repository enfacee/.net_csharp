using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IEventService, EventService>();
builder.Services.AddSingleton<IBookingService, BookingService>();
builder.Services.AddHostedService<BookingProcessingBackgroundService>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var app = builder.Build();

app.UseHttpsRedirection();
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.MapControllers();

if (app.Environment.IsDevelopment())
{    
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();
