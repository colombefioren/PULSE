namespace Pulse.Application.Common;

public sealed class AppOptions
{
    public const string Section = "App";

    /// <summary>Where the web app lives. Links in emails point here, never at the API.</summary>
    public string PublicUrl { get; set; } = "http://localhost:8080";
}
