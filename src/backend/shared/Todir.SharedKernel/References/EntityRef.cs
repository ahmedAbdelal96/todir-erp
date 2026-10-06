namespace Todir.SharedKernel.References;

/// <summary>Identifies an entity across a public boundary without exposing its implementation.</summary>
public readonly record struct EntityRef
{
    public EntityRef(string entityType, Guid id)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new ArgumentException("An entity type is required.", nameof(entityType));
        }

        if (id == Guid.Empty)
        {
            throw new ArgumentException("An entity identifier is required.", nameof(id));
        }

        EntityType = entityType;
        Id = id;
    }

    public string EntityType { get; }

    public Guid Id { get; }
}
