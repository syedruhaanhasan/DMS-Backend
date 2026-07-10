namespace WDAS.Application.Abstractions;

public interface ISmsSender
{
    Task SendAsync(string toPhoneOrEmail, string message, CancellationToken cancellationToken = default);
}
