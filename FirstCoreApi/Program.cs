var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IEventService, EventService>();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/health", () => "I am ok!");

app.Run();
