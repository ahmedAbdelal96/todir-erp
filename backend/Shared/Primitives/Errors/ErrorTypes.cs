namespace Shared.Primitives.Errors;

public sealed record ValidationError : DomainError
{
    public ValidationError(string code, string message, IReadOnlyDictionary<string, string[]>? fieldErrors = null)
        : base(code, message)
    {
        FieldErrors = fieldErrors ?? new Dictionary<string, string[]>();
    }

    public IReadOnlyDictionary<string, string[]> FieldErrors { get; }
}

public sealed record ConflictError : DomainError
{
    public ConflictError(string code, string message)
        : base(code, message)
    {
    }
}

public sealed record ForbiddenError : DomainError
{
    public ForbiddenError(string code, string message)
        : base(code, message)
    {
    }
}

public sealed record NotFoundError : DomainError
{
    public NotFoundError(string code, string message)
        : base(code, message)
    {
    }
}

public sealed record ConcurrencyError : DomainError
{
    public ConcurrencyError(string code, string message)
        : base(code, message)
    {
    }
}
