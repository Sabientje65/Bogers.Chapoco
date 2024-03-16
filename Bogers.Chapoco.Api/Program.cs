using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

var builder = WebApplication.CreateBuilder(args);


//mitmdump --no-server --quiet --rfile flows_local --set hardump=- 
// --script <-- what info do we get?

var file = "D:\\Data\\flows";

var p = new MitmParser().ParseFlowsAsHar();


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


class MitmParser
{
    public MitmParser()
    {
        
    }

    public string ParseFlowsAsHar()
    {
        using var p = new Process
        {
            StartInfo = new ProcessStartInfo("C:\\Program Files\\mitmproxy\\bin\\mitmdump.exe")
            {
                Arguments = "--no-server --quiet --rfile D:/Data/flows_local --set hardump=-",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true
            }
        };
        
        // PosixSignalRegistration.Create(PosixSignal.SIGINT, ctx => ctx.can)

        
        var har = new StringBuilder();
        p.OutputDataReceived += (_, evt) => har.Append(evt.Data);
        
        p.Start();
        // p.WaitForInputIdle();
        p.GracefullyTerminate();
        // p.StandardInput.Close();
        p.WaitForExit();

        return har.ToString();
    }
    
}

public static class ProcessExtensions
{
    public static void GracefullyTerminate(this Process process)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Windows.AttachConsole((uint)process.Id);
            Windows.GenerateConsoleCtrlEvent(0, 0);
            return;
        }

        Unix.kill(process.Id, 15);
    }

    private static class Windows
    {
        // https://github.com/devlooped/dotnet-stop/blob/main/src/Program.cs
        [DllImport("kernel32.dll")]
        public static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AttachConsole(uint dwProcessId);
    }

    private static class Unix
    {
        // https://github.com/dotnet/runtime/issues/59746#issuecomment-930132533
        [DllImport("libc")]
        public static extern int kill(int pid, int sig);
    }
}
