using System.Text.Encodings.Web;
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
    .AddHostedService<PocochaAuthenticationService>()
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

app.MapGet("/api/pococha/following", async ([FromServices] PocochaClient pococha) => await pococha.Proxy("GET", "v5/lives/followings"));

app.MapMethods(
    "/api/pococha/proxy/{**path}",
    [HttpMethod.Get.Method, HttpMethod.Post.Method, HttpMethod.Put.Method, HttpMethod.Delete.Method],
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

// app.MapGet("/stream", context =>
// {
//  //https://3b654b205ff4.us-west-2.playback.live-video.net/api/video/v1/us-west-2.748725782618.channel.fVShT7hNus1s.m3u8   
// })

app.MapGet("/view", (HttpContext context, [FromQuery] string url) =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    return $"""
    <!DOCTYPE HTML>
    <html>
        <head>
            <title>Viewing: {esc(url)}</title>
        </head>
        <body>
            <video width="500" height="500" controls>
                <source src="{esc(url)}" />
            </video>
        </body>
    </html
    """;

    string esc(string input) => HtmlEncoder.Default.Encode(input);
});

app.Run();