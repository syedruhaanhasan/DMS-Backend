namespace WDAS.Application.Notifications;

public static class ExternalInvitationEmailBuilder
{
    public static (string Subject, string PlainText, string Html) Build(
        string approverName,
        string documentSubject,
        string secureLinkUrl,
        string otp,
        int otpExpiryMinutes,
        int linkExpiryHours,
        bool isResend)
    {
        var subject = isResend
            ? $"WDAS: Your approval invitation was resent — {documentSubject}"
            : $"WDAS: Document approval requested — {documentSubject}";

        var plainText = $"""
            Hello {approverName},

            You have been invited to review and approve a document in WDAS.

            Document: {documentSubject}

            1. Open your secure link (valid for {linkExpiryHours} hours):
            {secureLinkUrl}

            2. Enter this one-time verification code (valid for {otpExpiryMinutes} minutes):
            {otp}

            If you did not expect this email, you can ignore it.

            — WDAS Document Approvals
            """;

        var html = $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:Segoe UI,Arial,sans-serif;line-height:1.5;color:#0f172a;max-width:560px;margin:0 auto;padding:24px;">
              <p style="font-size:12px;color:#64748b;text-transform:uppercase;letter-spacing:0.05em;">WDAS External Approval</p>
              <h1 style="font-size:20px;margin:0 0 12px;">Hello {System.Net.WebUtility.HtmlEncode(approverName)},</h1>
              <p>You have been invited to review and approve:</p>
              <p style="font-size:16px;font-weight:600;margin:16px 0;">{System.Net.WebUtility.HtmlEncode(documentSubject)}</p>
              <p><a href="{secureLinkUrl}" style="display:inline-block;background:#2563eb;color:#fff;text-decoration:none;padding:12px 20px;border-radius:8px;font-weight:600;">Open secure approval link</a></p>
              <p style="font-size:12px;color:#64748b;word-break:break-all;">Or copy this URL:<br/>{System.Net.WebUtility.HtmlEncode(secureLinkUrl)}</p>
              <div style="margin:20px 0;padding:16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;">
                <p style="margin:0 0 8px;font-size:13px;color:#475569;">Your one-time verification code</p>
                <p style="margin:0;font-size:28px;font-weight:700;letter-spacing:0.25em;font-family:Consolas,monospace;">{otp}</p>
                <p style="margin:8px 0 0;font-size:12px;color:#64748b;">Expires in {otpExpiryMinutes} minutes</p>
              </div>
              <p style="font-size:12px;color:#64748b;">This link expires in {linkExpiryHours} hours. All actions are logged.</p>
            </body>
            </html>
            """;

        return (subject, plainText, html);
    }
}
