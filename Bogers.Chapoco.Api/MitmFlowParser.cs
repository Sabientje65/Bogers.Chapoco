using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Bogers.Chapoco.Api;
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
    /// <exception cref="ApplicationException">Thrown when parsing failed (interrupted)</exception>
    public async Task<JsonNode> ParseToHar(string path)
    {
        // todo: add ability to add a flowfilter
        // normalize path
        path = PathHelper.Normalize(path);
        
        if (!File.Exists(path)) throw new FileNotFoundException("Could not find file", path);

        var harBuilder = new StringBuilder();
        var errBuilder = new StringBuilder();
        
        using var gracefulCls = new CancellationTokenSource();
        var gracefulToken = gracefulCls.Token;
        
        // give mitmdump a decent timeframe to produce output, after that we'll be waiting in intervals of 150ms
        // if no output is written anymore, we gracefully terminate
        // allowing us to capture the har output written to stdout by mitmdump
        gracefulCls.CancelAfter(TimeSpan.FromSeconds(5));

        var cmd = Cli.Wrap("mitmdump")
            .WithArguments(["-nr", path, "--set", "hardump=-"])
            .WithStandardOutputPipe(PipeTarget.ToDelegate(buffer =>
            {
                if (gracefulCls.IsCancellationRequested)
                {
                    var isFirstWrite = buffer.Length == 0;
                    
                    // should reduce errors: ignore everything until first `{`
                    // buffer may still contain some output from write prior to us sending signint
                    if (isFirstWrite) {
                        if (buffer.IndexOf('{') == -1) return;
                        buffer = buffer.Substring(buffer.IndexOf('{'));
                    }
                    
                    harBuilder.Append(buffer);
                    return;
                }

                // small delay for next regular output, no more output = assumed done
                gracefulCls.CancelAfter(TimeSpan.FromMilliseconds(500));   
            }))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(errBuilder))
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
            var err = errBuilder.ToString();
            if (!String.IsNullOrEmpty(err)) throw new ApplicationException($"mitmdump failed: {errBuilder}");
            
            var har = harBuilder.ToString();
            try
            {
                if (!String.IsNullOrEmpty(har)) return JsonNode.Parse(har)!;
            }
            catch (Exception)
            {
                throw new ApplicationException($"mitmdump failed, unable to parse json: {har}");
            }
            
            throw new ApplicationException("mitm dump failed", e); // bubble when something else occurred
        }
    }
    
}
