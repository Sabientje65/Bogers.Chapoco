using Bogers.Chapoco.Api;

var builder = WebApplication.CreateBuilder(args);


//mitmdump --no-server --quiet --rfile flows_local --set hardump=- 
// mitmdump -qnr flows_local --set hardump=-
// --script <-- what info do we get?

// var cls = new CancellationTokenSource();
// var token = cls.Token;
// cls.CancelAfter(10_000);
// cls.CancelAfter(500);
// cls.CancelAfter(10_000);
// await Task.Delay(1000);
// Console.WriteLine(token.IsCancellationRequested);
// await Task.Delay(10_000);
//
// return;

var flowParser = new MitmFlowParser();
var headerStore = new PocochaHeaderStore();
var har = await flowParser.ParseToHar("D:\\Data\\flows_live");
var pocochaRequests = DebugHelper.FindPocochaRequests(har);


headerStore.UpdateFromHar(har);

return;

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