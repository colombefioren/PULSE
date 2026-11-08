using Pulse.Domain.Common;

namespace Pulse.UnitTests.Common;

public class IdsTests
{
    [Fact]
    public void New_returns_a_version_7_uuid()
    {
        Ids.New().Version.Should().Be(7);
    }

    [Fact]
    public void Ids_created_later_sort_after_earlier_ones()
    {
        var earlier = Ids.NewAt(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var later = Ids.NewAt(DateTimeOffset.Parse("2026-01-01T00:00:01Z"));

        earlier.CompareTo(later).Should().BeNegative();
    }

    [Fact]
    public void New_never_returns_an_empty_id()
    {
        Ids.New().Should().NotBe(Guid.Empty);
    }
}
