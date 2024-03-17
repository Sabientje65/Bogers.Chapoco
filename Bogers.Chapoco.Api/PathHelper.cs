namespace Bogers.Chapoco.Api;

public class PathHelper
{
    /// <summary>
    /// Normalize the given path by ensuring the path separator for the current OS is being used
    /// </summary>
    /// <param name="path">Path to normalize</param>
    /// <returns>Normalized path</returns>
    public static string Normalize(string path) => path
        .Replace('/', Path.DirectorySeparatorChar)
        .Replace('\\', Path.DirectorySeparatorChar);
}