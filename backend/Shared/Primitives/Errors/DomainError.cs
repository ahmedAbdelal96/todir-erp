namespace Shared.Primitives.Errors;

/// <summary>Base error with a stable machine code safe for contracts and API mapping.</summary>
public abstract record DomainError
{
    protected DomainError(string code, string message)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A stable error code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("An error message is required.", nameof(message));
        }

        Code = code;
        Message = message;
    }

    public string Code { get; }

    public string Message { get; }
}
