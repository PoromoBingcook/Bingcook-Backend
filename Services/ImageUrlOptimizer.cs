namespace BingCook.Api.Services;

public static class ImageUrlOptimizer
{
    private const int DefaultQuality = 75;

    public static string? ForWidth(string? imageUrl, int width)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || width <= 0)
        {
            return imageUrl;
        }

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
            || !uri.Host.Equals("images.unsplash.com", StringComparison.OrdinalIgnoreCase))
        {
            return imageUrl;
        }

        var query = ParseQuery(uri.Query);
        query["auto"] = "format";
        query["fit"] = "crop";
        query["w"] = width.ToString(System.Globalization.CultureInfo.InvariantCulture);
        query["q"] = DefaultQuality.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var builder = new UriBuilder(uri)
        {
            Query = string.Join("&", query.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"))
        };

        return builder.Uri.AbsoluteUri;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in query.TrimStart('?').Split(
                     '&',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            var key = separator < 0 ? part : part[..separator];
            var value = separator < 0 ? string.Empty : part[(separator + 1)..];
            values[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value);
        }

        return values;
    }
}
