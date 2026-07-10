namespace WDAS.Application.Abstractions;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body, bool isHtml = false, CancellationToken cancellationToken = default);
}
