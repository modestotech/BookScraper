using System.Text.RegularExpressions;

namespace BookScraper.Console;
internal static class UrlUtils
{
    internal static bool UrlIsValid(string url)
    {
        var uriCreationResult = Uri.TryCreate(url, UriKind.Absolute, out var uriResult);
        var protocolIsValid = uriResult!.Scheme == Uri.UriSchemeHttp || uriResult!.Scheme == Uri.UriSchemeHttps;
        return uriCreationResult && protocolIsValid;
    }

    internal static string CleanUrl(string urlToClean)
    {
        var builder = new UriBuilder(urlToClean)
        {
            Port = -1,
            Query = null,
        };

        if (builder.Path.EndsWith('/'))
        {
            builder.Path = builder.Path.TrimEnd('/');
        }

        if (builder.Path.Contains("//"))
        {
            builder.Path = builder.Path.Replace("//", "/");
        }

        return builder.ToString();
    }

    internal static Uri GetUri(string url)
    {
        var builder = new UriBuilder(url);
        return builder.Uri;
    }

    internal static string GetPath(string baseDirectory, string filePath)
    {
        /*


        string osSpecificFilePath;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            osSpecificFilePath = filePath;
        }
        else
        {
            osSpecificFilePath = filePath.Replace(@"/", @"\");
        }

        */

        var hasFileExtension = Regex.IsMatch(filePath, @"\.\w{1,4}\b", RegexOptions.IgnoreCase);

        var path = Path.Combine(
            baseDirectory,
            hasFileExtension ? filePath.TrimStart('/') : "index.html");

        return Path.GetFullPath(path);
    }
}
