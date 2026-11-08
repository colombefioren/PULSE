namespace Pulse.Domain.Organizations;

public sealed class Organization
{
    public const int NameMaxLength = 80;

    private Organization()
    {
    }

    public Organization(string name, string slug, DateTimeOffset createdAt)
    {
        Id = Common.Ids.New();
        Name = name;
        Slug = slug;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>URL-safe, unique across organizations. Used in public status page addresses later on.</summary>
    public string Slug { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }
}
