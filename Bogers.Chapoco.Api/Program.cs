using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bogers.Chapoco.Api;
using Bogers.Chapoco.Api.Pococha;
using Bogers.Chapoco.Api.Pushover;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddHttpClient()
    .AddSingleton<PocochaHeaderStore>()
    .AddScoped<PocochaClient>()
    .AddScoped<PushoverClient>()
    .AddScoped<AppAuthenticationService>()
    .AddHostedService<PocochaAuthenticationService>()
    .AddHostedService<PocochaLiveMonitorService>()
    ;

builder.Logging.AddSimpleConsole(opts => {
    opts.SingleLine = true;
    opts.ColorBehavior = LoggerColorBehavior.Enabled;
    opts.TimestampFormat = "[yyyy-MM-dd hh:mm:ss]";
});

// add filewatcher
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.AddOptions<PushoverConfiguration>()
    .BindConfiguration("Pushover");
builder.Services.AddOptions<PocochaConfiguration>()
    .BindConfiguration("Pococha");
builder.Services.AddOptions<AuthenticationConfiguration>()
    .BindConfiguration("Authentication");

var app = builder.Build();

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    var authDisabled = context.RequestServices.GetRequiredService<IOptions<AuthenticationConfiguration>>().Value.Disabled;
    
    // logging in, continue with request
    if (
        authDisabled ||
        context.Request.Path.Equals("/login")
    )
    {
        await next();
        return;
    }
    
    // redirect to login when unauthenticated, should also log
    var authService = context.RequestServices.GetRequiredService<AppAuthenticationService>();
    if (
        !context.Request.Cookies.TryGetValue("auth", out var authToken) ||
        !authService.IsValid(authToken)
    )
    {
        context.Response.Redirect($"/login?returnUrl={context.Request.GetEncodedPathAndQuery()}");
        return;
    }
    
    // continue to resource
    await next();
});

app.MapMethods(
    "/login",
    [HttpMethod.Get.Method, HttpMethod.Post.Method],
    async (HttpContext context, [FromServices] ILogger<WebApplication> logger) =>
    {
        // trying to perform login
        if (context.Request.Method.Equals(HttpMethod.Post.Method))
        {
            var password = context.Request.Form["password"].ToString();
            var authService = context.RequestServices.GetRequiredService<AppAuthenticationService>();

            if (authService.IsValid(password))
            {
                logger.LogInformation("Successful login detected from ip: {Ip}", context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
                
                context.Response.Cookies.Append("auth", authService.Token, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTime.MaxValue
                });
                
                context.Response.Redirect(returnUrl() ?? "/following");
                return;
            }
            
            logger.LogWarning("Failed login detected from ip: {Ip}", context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync($$"""
        <!DOCTYPE HTML>
        <html>
            <head>
                <title>Login</title>
            </head>
            <body>
                <form method="post" action="/login?returnUrl={{esc(returnUrl())}}">
                    <div>
                        <label for="password">Password</label>
                        <input id="password" type="password" name="password" required />
                    </div>
                    
                    <div>
                        <input type="submit" value="login" />
                    </div>
                </form>
            </body>
        </html>
        """);

        string? returnUrl()
        {
            if ( 
                context.Request.Query.TryGetValue("returnUrl", out var returnUrl) && 
                !String.IsNullOrEmpty(returnUrl) 
            )
            {
                return returnUrl;
            }

            return null;
        } 
        string esc(string? input) => HtmlEncoder.Default.Encode(input ?? "");
    }
);

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

app.MapGet("/{who:regex(^(following|all)$)}", async (
        HttpContext context,
        [FromRoute] string who,
        [FromServices] PocochaClient pococha
    ) =>
    {
        var lives = who == "following" ?
            await pococha.GetFollowingCurrentlyLive() :
            await pococha.GetCurrentlyLive();

        context.Response.ContentType = "text/html; charset=utf-8";
        return $$"""
         <!DOCTYPE HTML>
         <html>
             <head>
                 <title>Following</title>
             </head>
             <body>
                {{ String.Join(" ", lives.LiveResources.Select(buildUi)) }}
             </body>
         </html>
         """;
        
        string buildUi(LiveResource live)
        {
            return $$"""
                <article class="live">
                    <a href="/view/{{live.Live.Id}}">
                        <img 
                            width="150" 
                            height"150" 
                            class="thumbnail" 
                            src="{{esc(live.Live.ThumbnailImageUrl)}}">
                        </img>
                    </a>
                    
                    <a href="/view/{{live.Live.Id}}">
                        <div class="details">
                            <img
                                width="25"
                                height="25"
                                class="profile-image"
                                src="{{live.User.ProfileImageUrl}}"
                            >
                            </img>
                            
                            <h2 class="title">{{esc(live.Live.Title)}}</h2>
                        </div>
                    </a>
                </article>       
            """;
        }
        
        string esc(string input) => HtmlEncoder.Default.Encode(input);
    }
);

app.MapGet("/view/{liveId}", async (
    HttpContext context, 
    [FromRoute] int liveId,
    [FromServices] PocochaClient pococha
) =>
{
    var live = await pococha.GetLive(liveId);
    
    context.Response.ContentType = "text/html; charset=utf-8";
    return $$"""
    <!DOCTYPE HTML>
    <html>
        <head>
            <title>Viewing: {{esc(live.User.Name)}}</title>
            <script>window.HELP_IMPROVE_VIDEOJS = false;</script>
            
            <link href="https://unpkg.com/video.js@8.3.0/dist/video-js.css" rel="stylesheet">
            <script src="https://unpkg.com/video.js@8.3.0/dist/video.js"></script>
        </head>
        <body>
        
            <video
                controls
                autoplay="autoplay"
                preload="auto"
                class="video-js"
                data-setup="{}"
            >
                <source src="{{esc(live.LiveEdge.Ivs.PlaybackUrl)}}" type="application/vnd.apple.mpegurl"></source>
            </video>
            
            <form id="comment-form">
                <label for="comment">Comment</label>
                <input id="comment" name="Comment" />
            </form>
            
            <script>
                (function() {
                    console.log('Initializing comments!');
                    document.getElementById('comment-form').addEventListener('submit', e => {
                        e.preventDefault();
                        
                        const comment = document.getElementById('comment').value;
                        fetch('/api/live/{{liveId}}/comment', {
                            method: 'POST',
                            body: JSON.stringify({
                                body: comment
                            }),
                            headers: {
                                'content-type': 'application/json'
                            }
                        }).then(res => res.text()).then(txt => console.log('Comment result: ' + txt));
                    });
                })();
            </script>
        </body>
    </html>
    """;

    string esc(string input) => HtmlEncoder.Default.Encode(input);
});

app.MapPost("/api/live/{liveId}/comment", async (
    [FromRoute] int liveId,
    [FromBody] CommentModel comment,
    [FromServices] PocochaClient pococha
) => await pococha.Proxy("POST", $"/v2/lives/{liveId}/comments", comment));

app.Run();

record CommentModel(string Body);

class AppAuthenticationService
{
    private static readonly Guid _unique = Guid.NewGuid();
    private readonly AuthenticationConfiguration _configuration;

    public string Token
    {
        get
        {
            // require re-authenticate per restart, service is meant for private use, don't have to go all out
            var bytes = Encoding.UTF8.GetBytes($"{_configuration.Password}:{_unique}");
            return Encoding.UTF8.GetString(SHA256.HashData(bytes));
        }
    }
    
    public AppAuthenticationService(IOptions<AuthenticationConfiguration> configuration) => _configuration = configuration.Value;

    /// <summary>
    /// Validate the given token or password, assume password is only used for login
    /// </summary>
    /// <param name="tokenOrPassword">Token</param>
    /// <returns>True when token is valid</returns>
    public bool IsValid(string tokenOrPassword) => tokenOrPassword == Token || tokenOrPassword == _configuration.Password;
}
