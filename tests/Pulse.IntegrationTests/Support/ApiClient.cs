using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace Pulse.IntegrationTests.Support;

public sealed record Problem(int Status, string? Title, string? Code, Dictionary<string, string[]>? Errors);

public sealed record OrgInfo(Guid Id, string Name, string Slug, string Role);

public sealed record Me(Guid Id, string Email, string Name, bool EmailConfirmed, List<OrgInfo> Organizations);

public sealed record Member(Guid UserId, string Email, string Name, string Role);

public sealed record InviteInfo(Guid Id, string Email, string Role, DateTimeOffset ExpiresAt);

/// <summary>An HttpClient that behaves like the SPA: keeps cookies and sends the CSRF header on every change.</summary>
public sealed class ApiClient(HttpClient http)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Task<HttpResponseMessage> Get(string path) => http.GetAsync(path);

    public Task<HttpResponseMessage> Post(string path, object? body = null) => Send(HttpMethod.Post, path, body);

    public Task<HttpResponseMessage> Patch(string path, object? body = null) => Send(HttpMethod.Patch, path, body);

    public Task<HttpResponseMessage> Delete(string path) => Send(HttpMethod.Delete, path, null);

    /// <summary>Sends without the security token, to prove the server turns it away.</summary>
    public Task<HttpResponseMessage> PostWithoutToken(string path, object? body = null) =>
        http.PostAsJsonAsync(path, body ?? new { }, Json);

    public async Task<T> Read<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    public async Task<Problem> ReadProblem(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        return new Problem(
            (int)response.StatusCode,
            root.TryGetProperty("title", out var title) ? title.GetString() : null,
            root.TryGetProperty("code", out var code) ? code.GetString() : null,
            root.TryGetProperty("errors", out var errors)
                ? errors.Deserialize<Dictionary<string, string[]>>(Json)
                : null);
    }

    public async Task<Me> Me() => await Read<Me>(await Get("/api/auth/me"));

    /// <summary>The sign-in cookie as a request header value, for connections that bypass the HttpClient (WebSockets).</summary>
    public string? AuthCookie { get; private set; }

    public async Task<HttpResponseMessage> Login(string email, string password, bool remember = false)
    {
        var response = await Post("/api/auth/login", new { email, password, remember });
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            AuthCookie = cookies.Select(c => c.Split(';')[0]).FirstOrDefault(c => c.StartsWith("pulse.auth=", StringComparison.Ordinal));
        }

        return response;
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body)
    {
        // The token is tied to who is signed in, so ask for a fresh one each time rather than caching it.
        var csrf = await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-XSRF-TOKEN", csrf.GetProperty("token").GetString());
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        return await http.SendAsync(request);
    }

    public static (Guid UserId, string Token) ParseVerificationLink(Uri link)
    {
        var query = HttpUtility.ParseQueryString(link.Query);
        return (Guid.Parse(query["userId"]!), query["token"]!);
    }

    public static string TokenAfterLastSlash(Uri link) => link.Segments[^1];
}
