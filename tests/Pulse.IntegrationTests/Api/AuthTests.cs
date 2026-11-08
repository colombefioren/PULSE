using System.Net;
using Pulse.IntegrationTests.Support;

namespace Pulse.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public class AuthTests(PulseApi api)
{
    private static object Registration(string email, string password = TestUsers.Password) =>
        new { name = "Colombe", teamName = "Acme Platform", email, password };

    [Fact]
    public async Task Signing_up_creates_the_user_an_organization_and_an_owner_membership()
    {
        var user = await api.SignUp("signup", "Acme Platform");

        var me = await user.Client.Me();

        me.EmailConfirmed.Should().BeTrue();
        me.Organizations.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Name = "Acme Platform", Role = "Owner" },
            options => options.ExcludingMissingMembers());
        me.Organizations[0].Slug.Should().StartWith("acme-platform");
    }

    [Fact]
    public async Task Signing_in_is_refused_until_the_email_is_confirmed()
    {
        var client = api.NewClient();
        var email = TestUsers.NewEmail("unverified");
        (await client.Post("/api/auth/register", Registration(email))).StatusCode.Should().Be(HttpStatusCode.Accepted);

        var response = await client.Login(email, TestUsers.Password);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.ReadProblem(response)).Code.Should().Be("email_not_verified");
        (await client.Get("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Resending_the_confirmation_email_sends_a_working_link()
    {
        var client = api.NewClient();
        var email = TestUsers.NewEmail("resend");
        await client.Post("/api/auth/register", Registration(email));
        var before = api.Emails.To(email).Count;

        (await client.Post("/api/auth/resend-verification", new { email })).StatusCode.Should().Be(HttpStatusCode.Accepted);

        api.Emails.To(email).Count.Should().Be(before + 1);
        var (userId, token) = ApiClient.ParseVerificationLink(api.Emails.LinkIn(email));
        (await client.Post("/api/auth/verify-email", new { userId, token })).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_confirmation_link_with_a_wrong_token_is_rejected()
    {
        var client = api.NewClient();
        var email = TestUsers.NewEmail("badtoken");
        await client.Post("/api/auth/register", Registration(email));
        var (userId, _) = ApiClient.ParseVerificationLink(api.Emails.LinkIn(email));

        var response = await client.Post("/api/auth/verify-email", new { userId, token = "not-a-real-token" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.ReadProblem(response)).Code.Should().Be("invalid_token");
    }

    [Fact]
    public async Task Registering_an_existing_address_looks_the_same_but_creates_nothing_and_emails_the_owner()
    {
        var existing = await api.SignUp("existing");
        var stranger = api.NewClient();

        var response = await stranger.Post("/api/auth/register", Registration(existing.Email, "another-password-1"));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        api.Emails.LastTo(existing.Email).Subject.Should().Contain("Someone tried to sign up");
        (await stranger.Login(existing.Email, "another-password-1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await existing.Client.Login(existing.Email, existing.Password)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Registration_reports_every_invalid_field_at_once()
    {
        var client = api.NewClient();

        var response = await client.Post("/api/auth/register", new { name = "", teamName = "", email = "nope", password = "short" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await client.ReadProblem(response);
        problem.Code.Should().Be("validation_failed");
        problem.Errors!.Keys.Should().BeEquivalentTo("name", "teamName", "email", "password");
    }

    [Theory]
    [InlineData("right-email", "wrong-password-1")]
    [InlineData("unknown-email", TestUsers.Password)]
    public async Task Wrong_credentials_get_the_same_answer_whether_or_not_the_account_exists(string which, string password)
    {
        var known = await api.SignUp("creds");
        var email = which == "right-email" ? known.Email : TestUsers.NewEmail("ghost");

        var response = await api.NewClient().Login(email, password);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await api.NewClient().ReadProblem(response)).Code.Should().Be("invalid_credentials");
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_for_a_while_even_for_the_right_one()
    {
        var user = await api.SignUp("lockout");
        var attacker = api.NewClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await attacker.Login(user.Email, "wrong-password-1");
        }

        var response = await attacker.Login(user.Email, user.Password);

        response.StatusCode.Should().Be((HttpStatusCode)423);
        (await attacker.ReadProblem(response)).Code.Should().Be("locked_out");
    }

    [Fact]
    public async Task Reading_the_profile_needs_a_session_and_logging_out_ends_it()
    {
        var user = await api.SignUp("session");
        (await api.NewClient().Get("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await user.Client.Post("/api/auth/logout")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await user.Client.Get("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_session_cookie_is_http_only()
    {
        var email = TestUsers.NewEmail("cookie");
        var client = api.NewClient();
        await client.Post("/api/auth/register", Registration(email));
        var (userId, token) = ApiClient.ParseVerificationLink(api.Emails.LinkIn(email));
        await client.Post("/api/auth/verify-email", new { userId, token });

        var response = await client.Login(email, TestUsers.Password);

        response.Headers.GetValues("Set-Cookie").Should().Contain(c => c.StartsWith("pulse.auth=") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_password_reset_link_works_once_and_the_old_password_stops_working()
    {
        var user = await api.SignUp("reset");
        var client = api.NewClient();

        (await client.Post("/api/auth/forgot-password", new { email = user.Email })).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var (userId, token) = ApiClient.ParseVerificationLink(api.Emails.LinkIn(user.Email));

        (await client.Post("/api/auth/reset-password", new { userId, token, password = "a-brand-new-password" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.Login(user.Email, user.Password)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.Login(user.Email, "a-brand-new-password")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reuse = await client.Post("/api/auth/reset-password", new { userId, token, password = "yet-another-password" });
        reuse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.ReadProblem(reuse)).Code.Should().Be("invalid_token");
    }

    [Fact]
    public async Task Asking_for_a_reset_for_an_unknown_address_answers_the_same_and_sends_nothing()
    {
        var email = TestUsers.NewEmail("nobody");

        var response = await api.NewClient().Post("/api/auth/forgot-password", new { email });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        api.Emails.To(email).Should().BeEmpty();
    }

    [Fact]
    public async Task A_change_without_the_security_token_is_refused()
    {
        var client = api.NewClient();

        var response = await client.PostWithoutToken("/api/auth/login", new { email = "a@b.co", password = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.ReadProblem(response)).Code.Should().Be("csrf_invalid");
    }

    [Fact]
    public async Task Too_many_attempts_from_one_address_get_429_with_retry_after()
    {
        using var limited = api.WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:AuthPermitsPerMinute", "3"));
        var client = new ApiClient(limited.CreateClient());

        HttpResponseMessage last = null!;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            last = await client.Login(TestUsers.NewEmail("flood"), "wrong-password-1");
        }

        last.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        last.Headers.RetryAfter.Should().NotBeNull();
        (await client.ReadProblem(last)).Code.Should().Be("rate_limited");
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var response = await api.NewClient().Get("/api/auth/me");

        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle().Which.Should().Contain("default-src 'none'");
    }
}
