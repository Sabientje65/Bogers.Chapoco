using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Medallion.Shell;

var builder = WebApplication.CreateBuilder(args);


//mitmdump --no-server --quiet --rfile flows_local --set hardump=- 
// mitmdump -qnr flows_local --set hardump=-
// --script <-- what info do we get?

var flowParser = new FlowParser();
var har = await flowParser.ParseToHAR("C:\\Data\\flows_local");

var file = "C:\\Data\\flows_local";

var output = new StringBuilder();

var cmd = Command.Run("mitmdump", "-nqr", "C:\\Data\\flows_local", "--set", "hardump=-")
    .RedirectTo(new StringWriter(output))
    // .RedirectStandardErrorTo(Console.Out)
    ;

// give our process 1 second to process
await Task.Delay(1000);

// check if we can detect process idle instead of waiting for 1 second
await cmd.TrySignalAsync(CommandSignal.ControlC);

await Task.Delay(500);
// await cmd.TrySignalAsync(CommandSignal.ControlC);

await Task.Delay(1500);

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


class FlowParser
{

    public async Task<string> ParseToHAR(string path)
    {
        var har = new StringBuilder();
        var err = new StringBuilder();

        var cmd = Command.Run("mitmdump", "-nqr", path, "--set", "hardump=-")
            .RedirectTo(new StringWriter(har))
            .RedirectStandardErrorTo(new StringWriter(err));

        // give our process 1 second to process
        await Task.Delay(1000);

        // check if we can detect process idle instead of waiting for 1 second
        await cmd.TrySignalAsync(CommandSignal.ControlC);

        return har.ToString();
    }
    
}
