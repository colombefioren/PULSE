using Pulse.Application.Accounts;
using Pulse.Application.Common;
using Pulse.Application.Organizations;
using Pulse.Domain.Organizations;

namespace Pulse.UnitTests.Accounts;

public class ValidatorTests
{
    private static RegisterRequest Valid() => new("Colombe", "Acme", "colombe@acme.dev", "a-long-enough-password");

    [Fact]
    public void Accepts_a_complete_registration() =>
        FluentActions.Invoking(() => new RegisterValidator().Check(Valid())).Should().NotThrow();

    [Fact]
    public void Reports_every_bad_field_at_once_keyed_by_the_json_property_name()
    {
        var request = new RegisterRequest("", "", "not-an-email", "short");

        var thrown = FluentActions.Invoking(() => new RegisterValidator().Check(request)).Should().Throw<AppException>().Which;

        thrown.Status.Should().Be(400);
        thrown.Code.Should().Be("validation_failed");
        thrown.Errors!.Keys.Should().BeEquivalentTo("name", "teamName", "email", "password");
        thrown.Errors["password"].Should().ContainSingle().Which.Should().Be("Use at least 10 characters");
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Enforces_the_password_length_limits(int length, bool valid) =>
        new RegisterValidator().Validate(Valid() with { Password = new string('x', length) }).IsValid.Should().Be(valid);

    [Fact]
    public void Normalizes_email_case_and_spaces() =>
        Validation.NormalizeEmail("  Colombe@Acme.DEV ").Should().Be("colombe@acme.dev");

    [Fact]
    public void Rejects_an_invalid_role_for_an_invite() =>
        new CreateInviteValidator().Validate(new CreateInviteRequest("ana@acme.dev", (Role)9)).IsValid.Should().BeFalse();

    [Fact]
    public void Rejects_an_overlong_team_name() =>
        new CreateOrganizationValidator().Validate(new CreateOrganizationRequest(new string('a', 81))).IsValid.Should().BeFalse();
}
