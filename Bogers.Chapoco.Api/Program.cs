using System.Diagnostics;
using System.Text;
using CliWrap;

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
var har = await flowParser.ParseToHAR("D:\\Data\\flows_local");

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


class MitmFlowParser
{

    public async Task<string> ParseToHAR(string path)
    {
        var har = new StringBuilder();
        var err = new StringBuilder();
        
        using var gracefulCls = new CancellationTokenSource();
        var gracefulToken = gracefulCls.Token;
        
        // give mitmdump a default of 2500ms to produce output, after that we'll be waiting in intervals of 150ms
        // if no output is written anymore, we gracefully terminate
        // allowing us to capture the har output written to stdout by mitmdump
        gracefulCls.CancelAfter(2500);

        var cmd = Cli.Wrap("mitmdump")
            .WithArguments(["-nr", path, "--set", "hardump=-"])
            .WithStandardOutputPipe(PipeTarget.Merge(
                
                PipeTarget.ToDelegate(_ =>
                {
                    if (gracefulCls.IsCancellationRequested)
                    {
                        har.Append(_);
                        return;
                    }

                    // small delay for next regular output, no more output = assumed done
                    gracefulCls.CancelAfter(150);   
                })
            ))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(err))
            .WithValidation(CommandResultValidation.None);

        try
        {
            // never exits before our gracefulCancellationToken, so will basically always crash
            await cmd.ExecuteAsync(
                forcefulCancellationToken: CancellationToken.None,
                gracefulCancellationToken: gracefulToken
            );

            throw new UnreachableException("Expected OperationCanceledException after graceful token cancellation");
        }
        catch (OperationCanceledException e)
        {
            if (e.CancellationToken == gracefulToken) return har.ToString();
            throw; // bubble when something else occurred
        }
    }
}
