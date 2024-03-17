using Bogers.Chapoco.Api.Pococha;
using Bogers.Chapoco.Api.Pushover;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddHttpClient()
    .AddSingleton<PocochaHeaderStore>()
    .AddScoped<PocochaClient>()
    .AddScoped<PushoverClient>();
    // .AddHostedService<PocochaAuthenticationStateMonitor>()
    // .AddHostedService<PocochaHeaderStoreUpdater>()
    // .AddHostedService<PocochaLiveMonitor>();


builder.Services.AddOptions<PushoverConfiguration>()
    .BindConfiguration("Pushover");
builder.Services.AddOptions<PocochaConfiguration>()
    .BindConfiguration("Pococha");

// builder.Configuration.AddJsonFile("appsettings.json");

// var p = new MitmParser().ParseFlowsAsHar();


// Parse();
// return;



// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
// builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
    // app.UseSwagger();
    // app.UseSwaggerUI();
// }

app.UseHttpsRedirection();

// var summaries = new[]
// {
//     "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
// };

app.MapGet("/debug/notification", async (
    [FromServices] PushoverClient pushover,
    [FromQuery] string message
) =>
{
    await pushover.SendMessage(PushoverMessage.Text(message));
    return new { status = "ok" };
});

app.MapGet("/weatherforecast", () =>
    {
        return "ok";
        // var forecast = Enumerable.Range(1, 5).Select(index =>
        //         new WeatherForecast
        //         (
        //             DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
        //             Random.Shared.Next(-20, 55),
        //             summaries[Random.Shared.Next(summaries.Length)]
        //         ))
        //     .ToArray();
        // return forecast;
    })
    .WithName("GetWeatherForecast")
    .WithOpenApi();

app.Run();