namespace Shared.Primitives.Identifiers;

public readonly record struct TenantId(Guid Value)
{
    public static TenantId New() => new(Uuid7.NewGuid());
}

public readonly record struct LegalEntityId(Guid Value)
{
    public static LegalEntityId New() => new(Uuid7.NewGuid());
}

public readonly record struct BranchId(Guid Value)
{
    public static BranchId New() => new(Uuid7.NewGuid());
}

public readonly record struct IdentityId(Guid Value)
{
    public static IdentityId New() => new(Uuid7.NewGuid());
}

public readonly record struct MembershipId(Guid Value)
{
    public static MembershipId New() => new(Uuid7.NewGuid());
}

public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Uuid7.NewGuid());
}

public readonly record struct ModuleId(Guid Value)
{
    public static ModuleId New() => new(Uuid7.NewGuid());
}

public readonly record struct CorrelationId(Guid Value)
{
    public static CorrelationId New() => new(Uuid7.NewGuid());
}

public readonly record struct CausationId(Guid Value)
{
    public static CausationId New() => new(Uuid7.NewGuid());
}

public readonly record struct MessageId(Guid Value)
{
    public static MessageId New() => new(Uuid7.NewGuid());
}
