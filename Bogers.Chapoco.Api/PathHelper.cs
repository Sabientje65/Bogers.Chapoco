using System.Text.RegularExpressions;

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

    /// <summary>
    /// Strip all extensions from the given path, foo.bar.baz -> foo
    /// </summary>
    /// <param name="path">Path to strip extensions from</param>
    /// <returns>Path without extensions</returns>
    public static string StripExtensions(string path) => path.Split('.')[0];
}