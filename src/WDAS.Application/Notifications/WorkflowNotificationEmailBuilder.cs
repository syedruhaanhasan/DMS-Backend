namespace WDAS.Application.Notifications;

public static class WorkflowNotificationEmailBuilder
{
    public static (string PlainBody, string HtmlBody) Build(
        string appBaseUrl,
        string subject,
        string body,
        Guid? documentId)
    {
        var docUrl = documentId.HasValue
            ? $"{appBaseUrl.TrimEnd('/')}/documents/{documentId.Value}"
            : appBaseUrl.TrimEnd('/');

        var plain = $"{body}\n\nOpen in WDAS: {docUrl}";
        var html = $"""
            <html><body style="font-family:Segoe UI,Arial,sans-serif;line-height:1.5;color:#111">
            <h2 style="margin:0 0 12px">{System.Net.WebUtility.HtmlEncode(subject)}</h2>
            <p>{System.Net.WebUtility.HtmlEncode(body)}</p>
            <p><a href="{docUrl}" style="display:inline-block;padding:10px 16px;background:#2563eb;color:#fff;text-decoration:none;border-radius:6px">Open document</a></p>
            <p style="color:#666;font-size:12px">WDAS workflow notification</p>
            </body></html>
            """;

        return (plain, html);
    }
}
