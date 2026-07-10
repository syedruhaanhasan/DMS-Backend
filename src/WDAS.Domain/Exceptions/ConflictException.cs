namespace WDAS.Domain.Exceptions;

public class ConflictException : DomainException
{
    public string Code { get; }
    public object? Details { get; }

    public ConflictException(string code, string message, object? details = null) : base(message)
    {
        Code = code;
        Details = details;
    }
}
