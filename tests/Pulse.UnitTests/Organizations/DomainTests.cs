using Pulse.Domain.Common;
using Pulse.Domain.Organizations;

namespace Pulse.UnitTests.Organizations;

public class SlugTests
{
    [Theory]
    [InlineData("Acme Platform!", "acme-platform")]
    [InlineData("  Équipe  Données ", "equipe-donnees")]
    [InlineData("a---b", "a-b")]
    [InlineData("Pulse 2", "pulse-2")]
    public void Turns_a_team_name_into_a_url_safe_slug(string name, string expected) =>
        Slugs.From(name).Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData("!!!")]
    [InlineData("日本語")]
    public void Falls_back_when_nothing_usable_is_left(string name) =>
        Slugs.From(name).Should().Be("team");

    [Fact]
    public void Never_exceeds_the_maximum_length_or_ends_with_a_hyphen()
    {
        var slug = Slugs.From(new string('a', 47) + " bbbb");

        slug.Length.Should().BeLessThanOrEqualTo(Slugs.MaxLength);
        slug.Should().NotEndWith("-");
    }
}

public class RoleTests
{
    [Theory]
    [InlineData(Role.Owner, Role.Admin, true)]
    [InlineData(Role.Admin, Role.Admin, true)]
    [InlineData(Role.Admin, Role.Owner, false)]
    [InlineData(Role.Viewer, Role.Admin, false)]
    [InlineData(Role.Viewer, Role.Viewer, true)]
    public void Higher_roles_include_the_lower_ones(Role role, Role minimum, bool expected) =>
        role.AtLeast(minimum).Should().Be(expected);
}

public class InviteTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private static Invite NewInvite() => new(Ids.New(), "ana@acme.dev", Role.Admin, "hash", Ids.New(), Now);

    [Fact]
    public void Expires_seven_days_after_it_was_created()
    {
        var invite = NewInvite();

        invite.ExpiresAt.Should().Be(Now.AddDays(7));
        invite.IsExpired(Now.AddDays(7).AddSeconds(-1)).Should().BeFalse();
        invite.IsExpired(Now.AddDays(7)).Should().BeTrue();
    }

    [Fact]
    public void Is_no_longer_open_once_accepted_or_expired()
    {
        var accepted = NewInvite();
        accepted.MarkAccepted(Now.AddHours(1));
        var expired = NewInvite();

        NewInvite().IsOpen(Now.AddDays(1)).Should().BeTrue();
        accepted.IsOpen(Now.AddHours(2)).Should().BeFalse();
        expired.IsOpen(Now.AddDays(8)).Should().BeFalse();
    }
}
