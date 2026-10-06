namespace Todir.SharedKernel.References;

/// <summary>Represents an actor at a boundary without coupling to an identity implementation.</summary>
public readonly record struct ActorRef
{
    public ActorRef(string actorType, Guid id)
    {
        if (string.IsNullOrWhiteSpace(actorType))
        {
            throw new ArgumentException("An actor type is required.", nameof(actorType));
        }

        if (id == Guid.Empty)
        {
            throw new ArgumentException("An actor identifier is required.", nameof(id));
        }

        ActorType = actorType;
        Id = id;
    }

    public string ActorType { get; }

    public Guid Id { get; }
}
