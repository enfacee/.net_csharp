var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IEventService, EventService>();
builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
