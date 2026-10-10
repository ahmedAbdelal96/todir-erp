namespace Shared.Primitives.Identifiers;

/// <summary>Creates time-ordered distributed identifiers without exposing database row sequences.</summary>
public static class Uuid7
{
    public static Guid NewGuid() => Guid.CreateVersion7();
}
