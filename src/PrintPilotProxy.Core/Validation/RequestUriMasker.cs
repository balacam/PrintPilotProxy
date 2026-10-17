namespace PrintPilotProxy.Core.Validation;

/// <summary>
/// Removes the parts of a request URI that commonly carry secrets (credentials, query values, fragments)
/// before it is written to logs, the recent-requests list or the clipboard (PROXY-011).
/// </summary>
public static class RequestUriMasker
{
    public static string Mask(Uri? uri)
    {
        if (uri is null)
        {
            return "Unknown";
        }

        if (!uri.IsAbsoluteUri)
        {
            var text = uri.OriginalString;
            var cut = text.IndexOfAny(new[] { '?', '#' });
            return cut >= 0 ? text.Substring(0, cut) + (text[cut] == '?' ? "?[redacted]" : string.Empty) : text;
        }

        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Fragment = string.Empty,
            Query = string.Empty
        };

        var masked = builder.Uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped);
        return string.IsNullOrEmpty(uri.Query) ? masked : masked + "?[redacted]";
    }
}
