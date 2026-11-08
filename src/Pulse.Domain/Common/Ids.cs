namespace Pulse.Domain.Common;

/// <summary>Creates time-ordered UUID v7 identifiers, so primary keys index well in PostgreSQL.</summary>
public static class Ids
{
    public static Guid New() => Guid.CreateVersion7();

    public static Guid NewAt(DateTimeOffset timestamp) => Guid.CreateVersion7(timestamp);
}
