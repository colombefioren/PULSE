using System.Net;
using Pulse.IntegrationTests.Support;

namespace Pulse.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public class StatusPageTests(PulseApi api)
{
    private static string Path(TestUser user) => $"/api/orgs/{user.OrgId}/status-pages";

    [Fact]
    public async Task Creates_a_page_with_defaults_and_lists_it()
    {
        var owner = await api.SignUp("sp-create");
        var slug = TestStatusPages.NewSlug();

        var page = await owner.CreateStatusPage(slug, "  Acme status ");

        page.Slug.Should().Be(slug);
        page.Title.Should().Be("Acme status");
        page.AccentColor.Should().Be("#fac303");
        page.Footer.Should().BeEmpty();
        page.ShowResponseTimes.Should().BeTrue();
        page.LogoUrl.Should().BeNull();
        page.Components.Should().BeEmpty();
        page.SubscriberCount.Should().Be(0);

        var list = await owner.Client.Read<List<StatusPageInfo>>(await owner.Client.Get(Path(owner)));
        list.Should().ContainSingle().Which.Id.Should().Be(page.Id);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("Has-Capitals")]
    [InlineData("has space")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("api")]
    [InlineData("status")]
    public async Task Turns_away_addresses_that_are_not_valid(string slug)
    {
        var owner = await api.SignUp("sp-slug");

        var response = await owner.Client.Post(Path(owner), new { slug, title = "Status" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.Client.ReadProblem(response)).Errors.Should().ContainKey("slug");
    }

    [Fact]
    public async Task Checks_title_accent_and_footer()
    {
        var owner = await api.SignUp("sp-fields");

        var response = await owner.Client.Post(Path(owner), new { slug = TestStatusPages.NewSlug(), title = " ", accentColor = "yellow", footer = new string('x', 301) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.Client.ReadProblem(response)).Errors.Should().ContainKeys("title", "accentColor", "footer");
    }

    [Fact]
    public async Task An_address_belongs_to_one_page_across_all_teams()
    {
        var first = await api.SignUp("sp-taken-a");
        var second = await api.SignUp("sp-taken-b");
        var slug = TestStatusPages.NewSlug();
        await first.CreateStatusPage(slug);

        var response = await second.Client.Post(Path(second), new { slug, title = "Copy" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await second.Client.ReadProblem(response);
        problem.Code.Should().Be("slug_taken");
        problem.Errors.Should().ContainKey("slug");
    }

    [Fact]
    public async Task A_team_can_have_five_pages()
    {
        var owner = await api.SignUp("sp-limit");
        for (var i = 0; i < 5; i++)
        {
            await owner.CreateStatusPage();
        }

        var response = await owner.Client.Post(Path(owner), new { slug = TestStatusPages.NewSlug(), title = "One too many" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await owner.Client.ReadProblem(response)).Code.Should().Be("status_page_limit_reached");
    }

    [Fact]
    public async Task Updating_changes_the_settings_and_the_address()
    {
        var owner = await api.SignUp("sp-update");
        var page = await owner.CreateStatusPage();
        var newSlug = TestStatusPages.NewSlug("renamed");

        var updated = await owner.Client.Read<StatusPageInfo>(await owner.Client.Patch(
            $"/api/status-pages/{page.Id}",
            new { slug = newSlug, title = "New title", accentColor = "#3B82F6", footer = "Contact ops@acme.dev", showResponseTimes = false }));

        updated.Slug.Should().Be(newSlug);
        updated.Title.Should().Be("New title");
        updated.AccentColor.Should().Be("#3b82f6");
        updated.Footer.Should().Be("Contact ops@acme.dev");
        updated.ShowResponseTimes.Should().BeFalse();

        (await api.NewClient().Get($"/api/public/status/{page.Slug}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "the old address stops working");
        (await api.NewClient().Get($"/api/public/status/{newSlug}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Keeping_the_same_address_on_update_is_not_a_clash()
    {
        var owner = await api.SignUp("sp-same");
        var page = await owner.CreateStatusPage();

        var response = await owner.Client.Patch($"/api/status-pages/{page.Id}", new { slug = page.Slug, title = "Renamed" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deleting_removes_the_page_and_its_public_address()
    {
        var owner = await api.SignUp("sp-delete");
        var page = await owner.CreateStatusPage();
        var monitor = await owner.CreateMonitor();
        await owner.AddComponent(page, "API", monitor);

        (await owner.Client.Delete($"/api/status-pages/{page.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await owner.Client.Get($"/api/status-pages/{page.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await api.NewClient().Get($"/api/public/status/{page.Slug}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.Client.Get($"/api/monitors/{monitor.Id}")).StatusCode.Should().Be(HttpStatusCode.OK, "the monitors stay");
    }

    [Fact]
    public async Task Admins_build_pages_and_viewers_can_only_look()
    {
        var owner = await api.SignUp("sp-roles");
        var admin = await api.SignUp("sp-roles-admin");
        var viewer = await api.SignUp("sp-roles-viewer");
        await api.Join(owner, admin, "Admin");
        await api.Join(owner, viewer, "Viewer");
        var page = await owner.CreateStatusPage();

        // Each person signed up with a team of their own; address the owner's team explicitly.
        var created = await admin.Client.Post($"/api/orgs/{owner.OrgId}/status-pages", new { slug = TestStatusPages.NewSlug(), title = "By an admin" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        foreach (var person in new[] { viewer })
        {
            var create = await person.Client.Post($"/api/orgs/{owner.OrgId}/status-pages", new { slug = TestStatusPages.NewSlug(), title = "Nope" });
            var update = await person.Client.Patch($"/api/status-pages/{page.Id}", new { slug = page.Slug, title = "Nope" });
            var delete = await person.Client.Delete($"/api/status-pages/{page.Id}");
            var logo = await person.Client.PutFile($"/api/status-pages/{page.Id}/logo", TestStatusPages.Png(), "image/png");

            foreach (var response in new[] { create, update, delete, logo })
            {
                response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
                (await person.Client.ReadProblem(response)).Code.Should().Be("insufficient_role");
            }

            (await person.Client.Get($"/api/status-pages/{page.Id}")).StatusCode.Should().Be(HttpStatusCode.OK, "everyone on the team can look");
            (await person.Client.Get($"/api/orgs/{owner.OrgId}/status-pages")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Another_team_cannot_see_or_change_a_page()
    {
        var owner = await api.SignUp("sp-tenant-a");
        var stranger = await api.SignUp("sp-tenant-b");
        var page = await owner.CreateStatusPage();
        var component = await owner.AddComponent(page, "API", await owner.CreateMonitor());

        var attempts = new[]
        {
            await stranger.Client.Get($"/api/status-pages/{page.Id}"),
            await stranger.Client.Patch($"/api/status-pages/{page.Id}", new { slug = page.Slug, title = "Mine now" }),
            await stranger.Client.Delete($"/api/status-pages/{page.Id}"),
            await stranger.Client.PutFile($"/api/status-pages/{page.Id}/logo", TestStatusPages.Png(), "image/png"),
            await stranger.Client.Delete($"/api/status-pages/{page.Id}/logo"),
            await stranger.Client.Post($"/api/status-pages/{page.Id}/components", new { name = "X", monitorIds = new[] { Guid.NewGuid() } }),
            await stranger.Client.Delete($"/api/status-pages/{page.Id}/components/{component.Id}"),
            await stranger.Client.Get($"/api/orgs/{owner.OrgId}/status-pages"),
        };

        attempts.Select(r => r.StatusCode).Should().OnlyContain(code => code == HttpStatusCode.NotFound || code == HttpStatusCode.Forbidden);
        (await owner.GetStatusPage(page.Id)).Title.Should().Be(page.Title);
        (await stranger.Client.Read<List<StatusPageInfo>>(await stranger.Client.Get(Path(stranger)))).Should().BeEmpty();
    }

    [Fact]
    public async Task Components_keep_their_order_and_belong_to_monitors_of_the_team()
    {
        var owner = await api.SignUp("sp-components");
        var page = await owner.CreateStatusPage();
        var website = await owner.CreateMonitor("Website");
        var checkout = await owner.CreateMonitor("Checkout");

        var first = await owner.AddComponent(page, "Website", website);
        var second = await owner.AddComponent(page, "Checkout", checkout, website);

        first.SortOrder.Should().Be(0);
        second.SortOrder.Should().Be(1);
        second.MonitorIds.Should().BeEquivalentTo(new[] { checkout.Id, website.Id });

        var reordered = await owner.Client.Read<List<ComponentInfo>>(await owner.Client.Put($"/api/status-pages/{page.Id}/components/order", new { componentIds = new[] { second.Id, first.Id } }));
        reordered.Select(c => c.Name).Should().Equal("Checkout", "Website");

        var edited = await owner.Client.Read<ComponentInfo>(await owner.Client.Patch(
            $"/api/status-pages/{page.Id}/components/{second.Id}",
            new { name = " Payments ", groupName = " Core ", monitorIds = new[] { checkout.Id } }));
        edited.Name.Should().Be("Payments");
        edited.GroupName.Should().Be("Core");
        edited.MonitorIds.Should().Equal(checkout.Id);

        (await owner.Client.Delete($"/api/status-pages/{page.Id}/components/{first.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.GetStatusPage(page.Id)).Components.Select(c => c.Name).Should().Equal("Payments");
    }

    [Fact]
    public async Task A_component_needs_a_name_and_monitors_of_the_same_team()
    {
        var owner = await api.SignUp("sp-comp-invalid");
        var stranger = await api.SignUp("sp-comp-stranger");
        var page = await owner.CreateStatusPage();
        var foreign = await stranger.CreateMonitor();

        var noName = await owner.Client.Post($"/api/status-pages/{page.Id}/components", new { name = "", monitorIds = new[] { foreign.Id } });
        var noMonitors = await owner.Client.Post($"/api/status-pages/{page.Id}/components", new { name = "API", monitorIds = Array.Empty<Guid>() });
        var foreignMonitor = await owner.Client.Post($"/api/status-pages/{page.Id}/components", new { name = "API", monitorIds = new[] { foreign.Id } });

        (await owner.Client.ReadProblem(noName)).Errors.Should().ContainKey("name");
        (await owner.Client.ReadProblem(noMonitors)).Errors.Should().ContainKey("monitorIds");
        (await owner.Client.ReadProblem(foreignMonitor)).Errors.Should().ContainKey("monitorIds");
    }

    [Fact]
    public async Task A_page_shows_at_most_thirty_components()
    {
        var owner = await api.SignUp("sp-comp-limit");
        var page = await owner.CreateStatusPage();
        var monitor = await owner.CreateMonitor();
        for (var i = 0; i < 30; i++)
        {
            await owner.AddComponent(page, $"Part {i}", monitor);
        }

        var response = await owner.Client.Post($"/api/status-pages/{page.Id}/components", new { name = "One more", monitorIds = new[] { monitor.Id } });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await owner.Client.ReadProblem(response)).Code.Should().Be("component_limit_reached");
    }

    [Fact]
    public async Task Reordering_needs_every_component_exactly_once()
    {
        var owner = await api.SignUp("sp-order");
        var page = await owner.CreateStatusPage();
        var monitor = await owner.CreateMonitor();
        var a = await owner.AddComponent(page, "A", monitor);
        await owner.AddComponent(page, "B", monitor);

        var missing = await owner.Client.Put($"/api/status-pages/{page.Id}/components/order", new { componentIds = new[] { a.Id } });
        var twice = await owner.Client.Put($"/api/status-pages/{page.Id}/components/order", new { componentIds = new[] { a.Id, a.Id } });

        foreach (var response in new[] { missing, twice })
        {
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await owner.Client.ReadProblem(response)).Code.Should().Be("order_mismatch");
        }
    }

    [Fact]
    public async Task A_deleted_monitor_leaves_its_components_without_it()
    {
        var owner = await api.SignUp("sp-monitor-gone");
        var page = await owner.CreateStatusPage();
        var kept = await owner.CreateMonitor("Kept");
        var removed = await owner.CreateMonitor("Removed");
        var component = await owner.AddComponent(page, "Both", kept, removed);

        (await owner.Client.Delete($"/api/monitors/{removed.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await owner.GetStatusPage(page.Id);
        after.Components.Single(c => c.Id == component.Id).MonitorIds.Should().Equal(kept.Id);
    }

    [Fact]
    public async Task Uploads_serves_replaces_and_removes_a_logo()
    {
        var owner = await api.SignUp("sp-logo");
        var page = await owner.CreateStatusPage();
        var png = TestStatusPages.Png(200);

        var withLogo = await owner.Client.Read<StatusPageInfo>(await owner.Client.PutFile($"/api/status-pages/{page.Id}/logo", png, "image/png"));
        withLogo.LogoUrl.Should().Be($"/api/public/status/{page.Slug}/logo?v=1");

        var served = await api.NewClient().Get(withLogo.LogoUrl!);
        served.StatusCode.Should().Be(HttpStatusCode.OK);
        served.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await served.Content.ReadAsByteArrayAsync()).Should().Equal(png);
        served.Headers.CacheControl!.MaxAge.Should().BeGreaterThan(TimeSpan.FromDays(30), "a versioned address never changes");

        var replaced = await owner.Client.Read<StatusPageInfo>(await owner.Client.PutFile($"/api/status-pages/{page.Id}/logo", TestStatusPages.Jpeg(100), "image/jpeg"));
        replaced.LogoUrl.Should().EndWith("?v=2");
        (await api.NewClient().Get(replaced.LogoUrl!)).Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");

        var webp = await owner.Client.PutFile($"/api/status-pages/{page.Id}/logo", TestStatusPages.Webp(100), "image/webp");
        webp.StatusCode.Should().Be(HttpStatusCode.OK);

        var removed = await owner.Client.Read<StatusPageInfo>(await owner.Client.Delete($"/api/status-pages/{page.Id}/logo"));
        removed.LogoUrl.Should().BeNull();
        (await api.NewClient().Get($"/api/public/status/{page.Slug}/logo")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Turns_away_logos_that_are_not_safe_images()
    {
        var owner = await api.SignUp("sp-logo-bad");
        var page = await owner.CreateStatusPage();
        var path = $"/api/status-pages/{page.Id}/logo";

        var svg = await owner.Client.PutFile(path, "<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>"u8.ToArray(), "image/svg+xml");
        var disguised = await owner.Client.PutFile(path, "<html>not an image</html>"u8.ToArray(), "image/png");
        var tooBig = await owner.Client.PutFile(path, TestStatusPages.Png(256 * 1024 + 1), "image/png");
        var empty = await owner.Client.PutFile(path, [], "image/png");

        (await owner.Client.ReadProblem(svg)).Code.Should().Be("logo_type_unsupported");
        (await owner.Client.ReadProblem(disguised)).Code.Should().Be("logo_invalid");
        (await owner.Client.ReadProblem(tooBig)).Code.Should().Be("logo_too_large");
        (await owner.Client.ReadProblem(empty)).Code.Should().Be("logo_too_large");
        (await owner.GetStatusPage(page.Id)).LogoUrl.Should().BeNull();
    }

    [Fact]
    public async Task A_logo_of_exactly_the_limit_is_accepted()
    {
        var owner = await api.SignUp("sp-logo-limit");
        var page = await owner.CreateStatusPage();

        var response = await owner.Client.PutFile($"/api/status-pages/{page.Id}/logo", TestStatusPages.Png(256 * 1024), "image/png");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Managing_pages_needs_a_session_and_a_token()
    {
        var owner = await api.SignUp("sp-auth");
        var page = await owner.CreateStatusPage();

        var anonymous = api.NewClient();
        (await anonymous.Get($"/api/status-pages/{page.Id}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var withoutToken = await owner.Client.PostWithoutToken($"/api/orgs/{owner.OrgId}/status-pages", new { slug = TestStatusPages.NewSlug(), title = "No token" });
        withoutToken.IsSuccessStatusCode.Should().BeFalse("a change without the antiforgery token is refused");
        (await owner.Client.Read<List<StatusPageInfo>>(await owner.Client.Get(Path(owner)))).Should().ContainSingle();
    }
}
