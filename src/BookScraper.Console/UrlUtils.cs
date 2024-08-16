namespace BookScraper.Console;
internal static class UrlUtils
{
    internal static bool UrlIsValid(string url)
    {
        var createdUri = Uri.TryCreate(url, UriKind.Absolute, out var uriResult);
        return createdUri && uriResult!.Scheme == Uri.UriSchemeHttp;
    }

    internal static string CleanUrl(string urlToClean)
    {
        var builder = new UriBuilder(urlToClean)
        {
            Port = -1,
            Query = null,
        };

        if (builder.Path.EndsWith("/"))
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
}
