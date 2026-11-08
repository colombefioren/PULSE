using System.Net;
using Pulse.IntegrationTests.Support;

namespace Pulse.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public class SubscriberTests(PulseApi api)
{
    private async Task<(TestUser Owner, StatusPageInfo Page)> Page(string prefix)
    {
        var owner = await api.SignUp(prefix);
        var page = await owner.CreateStatusPage();
        await owner.AddComponent(page, "Website", await owner.CreateMonitor());
        return (owner, page);
    }

    private static string Subscribe(StatusPageInfo page) => $"/api/public/status/{page.Slug}/subscribe";

    private static string Location(HttpResponseMessage response) => response.Headers.Location!.ToString();

    private Task<string?> UnsubscribeToken(string email) =>
        api.Scalar<string>("select unsubscribe_token from subscribers where email = $1", email.ToLowerInvariant());

    [Fact]
    public async Task Subscribing_sends_a_confirmation_and_only_the_link_makes_it_count()
    {
        var (owner, page) = await Page("sub-flow");
        var visitor = api.NewClientWithoutRedirects();
        var email = TestUsers.NewEmail("reader");

        var response = await visitor.PostWithoutToken(Subscribe(page), new { email });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var message = api.Emails.LastTo(email);
        message.Subject.Should().Contain(page.Title);
        message.Text.Should().Contain("/api/public/subscriptions/confirm/");
        (await owner.GetStatusPage(page.Id)).SubscriberCount.Should().Be(0, "nothing counts before the link is opened");

        var confirm = await visitor.Get(api.Emails.LinkIn(email).AbsolutePath);

        confirm.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Location(confirm).Should().Be($"https://pulse.test/status/{page.Slug}?subscription=confirmed");
        (await owner.GetStatusPage(page.Id)).SubscriberCount.Should().Be(1);
    }

    [Fact]
    public async Task A_confirmation_link_works_once()
    {
        var (_, page) = await Page("sub-once");
        var visitor = api.NewClientWithoutRedirects();
        var email = TestUsers.NewEmail("reader");
        await visitor.PostWithoutToken(Subscribe(page), new { email });
        var link = api.Emails.LinkIn(email).AbsolutePath;

        await visitor.Get(link);
        var again = await visitor.Get(link);

        Location(again).Should().Be("https://pulse.test/subscription/invalid");
    }

    [Fact]
    public async Task An_unknown_confirmation_link_goes_to_the_invalid_page()
    {
        var response = await api.NewClientWithoutRedirects().Get("/api/public/subscriptions/confirm/not-a-real-token");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Location(response).Should().Be("https://pulse.test/subscription/invalid");
    }

    [Fact]
    public async Task Asking_again_before_confirming_replaces_the_link()
    {
        var (owner, page) = await Page("sub-renew");
        var visitor = api.NewClientWithoutRedirects();
        var email = TestUsers.NewEmail("reader");
        await visitor.PostWithoutToken(Subscribe(page), new { email });
        var oldLink = api.Emails.LinkIn(email).AbsolutePath;
        await visitor.PostWithoutToken(Subscribe(page), new { email });
        var newLink = api.Emails.LinkIn(email).AbsolutePath;

        newLink.Should().NotBe(oldLink);
        Location(await visitor.Get(oldLink)).Should().EndWith("/subscription/invalid");
        Location(await visitor.Get(newLink)).Should().EndWith("?subscription=confirmed");
        (await owner.GetStatusPage(page.Id)).SubscriberCount.Should().Be(1);
    }

    [Fact]
    public async Task Subscribing_twice_never_makes_two_subscribers_or_reveals_who_is_subscribed()
    {
        var (owner, page) = await Page("sub-dupe");
        var visitor = api.NewClientWithoutRedirects();
        var email = TestUsers.NewEmail("Reader");
        await visitor.PostWithoutToken(Subscribe(page), new { email });
        await visitor.Get(api.Emails.LinkIn(email).AbsolutePath);
        var sentBefore = api.Emails.To(email).Count;

        var again = await visitor.PostWithoutToken(Subscribe(page), new { email = email.ToUpperInvariant() });

        again.StatusCode.Should().Be(HttpStatusCode.Accepted, "it looks the same as a new request");
        api.Emails.To(email).Should().HaveCount(sentBefore, "a confirmed address is not mailed again");
        (await owner.GetStatusPage(page.Id)).SubscriberCount.Should().Be(1);
    }

    [Fact]
    public async Task The_same_address_can_follow_two_pages()
    {
        var (firstOwner, first) = await Page("sub-two-a");
        var (secondOwner, second) = await Page("sub-two-b");
        var visitor = api.NewClientWithoutRedirects();
        var email = TestUsers.NewEmail("reader");

        await visitor.PostWithoutToken(Subscribe(first), new { email });
        await visitor.Get(api.Emails.LinkIn(email).AbsolutePath);
        await visitor.PostWithoutToken(Subscribe(second), new { email });
        await visitor.Get(api.Emails.LinkIn(email).AbsolutePath);

        (await firstOwner.GetStatusPage(first.Id)).SubscriberCount.Should().Be(1);
        (await secondOwner.GetStatusPage(second.Id)).SubscriberCount.Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("a@b")]
    public async Task Turns_away_things_that_are_not_email_addresses(string email)
    {
        var (_, page) = await Page("sub-invalid");
        var visitor = api.NewClient();

        var response = await visitor.PostWithoutToken(Subscribe(page), new { email });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await visitor.ReadProblem(response)).Errors.Should().ContainKey("email");
    }

    [Fact]
    public async Task Subscribing_to_a_page_that_does_not_exist_is_a_404()
    {
        var response = await api.NewClient().PostWithoutToken("/api/public/status/no-such-page-here/subscribe", new { email = TestUsers.NewEmail("reader") });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_unsubscribe_link_removes_the_address_and_works_once()
    {
        var (owner, page) = await Page("sub-unsub");
        var visitor = api.NewClientWithoutRedirects();
        var email = TestUsers.NewEmail("reader");
        await visitor.PostWithoutToken(Subscribe(page), new { email });
        await visitor.Get(api.Emails.LinkIn(email).AbsolutePath);
        var token = (await UnsubscribeToken(email))!;

        var removed = await visitor.Get($"/api/public/unsubscribe/{token}");
        var again = await visitor.Get($"/api/public/unsubscribe/{token}");

        Location(removed).Should().Be($"https://pulse.test/status/{page.Slug}?subscription=removed");
        Location(again).Should().Be("https://pulse.test/subscription/invalid");
        (await owner.GetStatusPage(page.Id)).SubscriberCount.Should().Be(0);
        (await UnsubscribeToken(email)).Should().BeNull();
    }

    [Fact]
    public async Task Mail_apps_can_unsubscribe_with_one_click()
    {
        var (owner, page) = await Page("sub-oneclick");
        var visitor = api.NewClient();
        var email = TestUsers.NewEmail("reader");
        await visitor.PostWithoutToken(Subscribe(page), new { email });
        await visitor.Get(api.Emails.LinkIn(email).AbsolutePath);
        var token = (await UnsubscribeToken(email))!;

        var first = await visitor.PostWithoutToken($"/api/public/unsubscribe/{token}");
        var second = await visitor.PostWithoutToken($"/api/public/unsubscribe/{token}");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK, "doing it twice is harmless");
        (await owner.GetStatusPage(page.Id)).SubscriberCount.Should().Be(0);
    }

    [Fact]
    public async Task Deleting_a_page_removes_its_subscribers()
    {
        var (owner, page) = await Page("sub-delete");
        var visitor = api.NewClientWithoutRedirects();
        var email = TestUsers.NewEmail("reader");
        await visitor.PostWithoutToken(Subscribe(page), new { email });
        await visitor.Get(api.Emails.LinkIn(email).AbsolutePath);

        await owner.Client.Delete($"/api/status-pages/{page.Id}");

        (await UnsubscribeToken(email)).Should().BeNull();
    }
}
