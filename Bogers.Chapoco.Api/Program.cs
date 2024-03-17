using System.Text.Json;
using System.Text.Json.Nodes;
using Bogers.Chapoco.Api.Pococha;
using Bogers.Chapoco.Api.Pushover;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddHttpClient()
    .AddSingleton<PocochaHeaderStore>()
    .AddScoped<PocochaClient>()
    .AddScoped<PushoverClient>()
    .AddHostedService<PocochaHeaderStoreUpdater>()
    .AddHostedService<PocochaAuthenticationStateMonitor>()
    .AddHostedService<PocochaLiveMonitor>()
    ;

builder.Logging
    .AddConsole();


builder.Services.AddOptions<PushoverConfiguration>()
    .BindConfiguration("Pushover");
builder.Services.AddOptions<PocochaConfiguration>()
    .BindConfiguration("Pococha");

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/api/debug/notification", async (
    [FromServices] PushoverClient pushover,
    [FromQuery] string message
) =>
{
    await pushover.SendMessage(PushoverMessage.Text(message));
    return new { status = "ok" };
});

app.MapMethods(
    "/api/pococha/forward/{**path}",
    [HttpMethod.Get.Method, HttpMethod.Post.Method, HttpMethod.Delete.Method],
    async (
        HttpContext context, 
        [FromServices] PocochaClient pococha,
        [FromRoute] string path
    ) =>
    {
        return await pococha.Proxy(
            context.Request.Method,
            $"/{path}{context.Request.QueryString}",
            
            // should just forwarded stream as-is
            context.Request.HasJsonContentType() ?
                JsonSerializer.DeserializeAsync<JsonNode>(context.Request.Body) :
                null
        );
    }
);

app.Run();