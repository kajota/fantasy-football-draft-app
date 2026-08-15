using System.Net;
using System.Text;

namespace FantasyDraftAssistant.Providers.Yahoo;

public static class YahooRedirectListener
{
    public static async Task<string?> WaitForCodeAsync(
        string redirectUri,
        string expectedState,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
            return null;

        var prefix = $"{uri.Scheme}://{uri.Authority}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            var contextTask = listener.GetContextAsync();
            var completed = await Task.WhenAny(contextTask, Task.Delay(Timeout.Infinite, timeoutCts.Token));
            if (completed != contextTask)
                return null;

            var context = await contextTask;
            var query = context.Request.QueryString;
            var code = query["code"];
            var state = query["state"];
            var html = !string.IsNullOrWhiteSpace(code) && (string.IsNullOrWhiteSpace(expectedState) || state == expectedState)
                ? SuccessHtml()
                : FailHtml(query["error_description"] ?? query["error"] ?? "No authorization code was returned.");
            var bytes = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, cancellationToken);
            context.Response.OutputStream.Close();
            return string.Equals(state, expectedState, StringComparison.Ordinal) ? code : null;
        }
        catch (Exception) when (timeoutCts.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            if (listener.IsListening)
                listener.Stop();
        }
    }

    private static string SuccessHtml() =>
        """
        <!doctype html>
        <html><body style="font-family:sans-serif;background:#0B1220;color:#E8EEF7;padding:2rem">
        <h1>Yahoo connected</h1>
        <p>You can close this tab and return to Fantasy Draft Assistant.</p>
        </body></html>
        """;

    private static string FailHtml(string message) =>
        $"""
        <!doctype html>
        <html><body style="font-family:sans-serif;background:#0B1220;color:#E8EEF7;padding:2rem">
        <h1>Yahoo sign-in did not finish</h1>
        <p>{WebUtility.HtmlEncode(message)}</p>
        </body></html>
        """;
}
