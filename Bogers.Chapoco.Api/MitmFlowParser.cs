using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using CliWrap;

class MitmFlowParser
{
    
    /// <summary>
    /// Parse the given flow file to http archive format
    ///
    /// <see cref="http://www.softwareishard.com/blog/har-12-spec/#pages"/>
    /// </summary>
    /// <param name="path">Path to file to parse</param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException">Thrown when flow file could not be found</exception>
    /// <exception cref="OperationCanceledException">Thrown when parsing failed (interrupted)</exception>
    public async Task<JsonNode> ParseToHar(string path)
    {
        // todo: add ability to add a flowfilter
        
        if (!File.Exists(path)) throw new FileNotFoundException("Could not find file", path);

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
            .WithStandardOutputPipe(PipeTarget.ToDelegate(_ =>
            {
                if (gracefulCls.IsCancellationRequested)
                {
                    har.Append(_);
                    return;
                }

                // small delay for next regular output, no more output = assumed done
                gracefulCls.CancelAfter(150);   
            }))
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
            if (e.CancellationToken == gracefulToken) return JsonNode.Parse(har.ToString())!;
            throw; // bubble when something else occurred
        }
    }
    
}