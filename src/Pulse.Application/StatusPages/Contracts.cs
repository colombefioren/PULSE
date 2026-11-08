using FluentValidation;
using Pulse.Domain.Incidents;
using Pulse.Domain.StatusPages;

namespace Pulse.Application.StatusPages;

/// <summary>What people send to create or change a status page.</summary>
public sealed record StatusPageInput(
    string Slug,
    string Title,
    string? AccentColor = null,
    string? Footer = null,
    bool ShowResponseTimes = true);

public sealed record ComponentInput(string Name, string? GroupName, Guid[] MonitorIds);

/// <summary>The component ids in the order they should appear.</summary>
public sealed record ComponentOrderInput(Guid[] ComponentIds);

public sealed record ComponentDto(Guid Id, string Name, string? GroupName, int SortOrder, IReadOnlyList<Guid> MonitorIds);

public sealed record StatusPageDto(
    Guid Id,
    Guid OrganizationId,
    string Slug,
    string Title,
    string AccentColor,
    string Footer,
    bool ShowResponseTimes,
    string? LogoUrl,
    int SubscriberCount,
    IReadOnlyList<ComponentDto> Components,
    DateTimeOffset CreatedAt);

/// <summary>The page as a visitor sees it. Nothing here names a monitor, a URL or an internal error.</summary>
public sealed record PublicStatusDto(
    string Slug,
    string Title,
    string? LogoUrl,
    string AccentColor,
    string Footer,
    bool ShowResponseTimes,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<PublicComponentDto> Components,
    IReadOnlyList<PublicIncidentDto> ActiveIncidents,
    IReadOnlyList<PublicIncidentDto> History);

/// <param name="Status">"up", "degraded", "down" or "maintenance".</param>
public sealed record PublicComponentDto(
    Guid Id,
    string Name,
    string? Group,
    string Status,
    double? Uptime90d,
    int? AvgResponseMs,
    IReadOnlyList<PublicDayDto> Days);

/// <param name="Date">The UTC day, as yyyy-MM-dd.</param>
public sealed record PublicDayDto(string Date, double? UptimePct, int DowntimeSeconds, int Incidents);

public sealed record PublicIncidentDto(
    Guid Id,
    string Title,
    IncidentSeverity Severity,
    IncidentStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? ResolvedAt,
    IReadOnlyList<string> Components,
    IReadOnlyList<PublicUpdateDto> Updates);

public sealed record PublicUpdateDto(IncidentStatus Status, string Message, DateTimeOffset At);

public sealed record SubscribeInput(string Email);

public sealed class StatusPageInputValidator : AbstractValidator<StatusPageInput>
{
    public StatusPageInputValidator()
    {
        RuleFor(x => x.Title).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Enter a title")
            .Must(title => title.Trim().Length <= StatusPage.TitleMaxLength).WithMessage($"Use {StatusPage.TitleMaxLength} characters or fewer");

        RuleFor(x => x.Slug).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Choose an address for the page")
            .Must(slug => slug.Trim().Length >= StatusPage.SlugMinLength).WithMessage($"Use at least {StatusPage.SlugMinLength} characters")
            .Must(slug => slug.Trim().Length <= StatusPage.SlugMaxLength).WithMessage($"Use {StatusPage.SlugMaxLength} characters or fewer")
            .Must(slug => StatusPage.IsValidSlug(slug.Trim())).WithMessage("Use lower case letters, numbers and hyphens, and avoid words the app already uses");

        RuleFor(x => x.AccentColor)
            .Must(color => StatusPage.IsValidAccent(color!.Trim())).WithMessage("Use a colour like #fac303")
            .When(x => !string.IsNullOrWhiteSpace(x.AccentColor));

        RuleFor(x => x.Footer)
            .Must(footer => footer!.Trim().Length <= StatusPage.FooterMaxLength).WithMessage($"Use {StatusPage.FooterMaxLength} characters or fewer")
            .When(x => x.Footer is not null);
    }
}

public sealed class ComponentInputValidator : AbstractValidator<ComponentInput>
{
    public ComponentInputValidator()
    {
        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Enter a name")
            .Must(name => name.Trim().Length <= StatusComponent.NameMaxLength).WithMessage($"Use {StatusComponent.NameMaxLength} characters or fewer");

        RuleFor(x => x.GroupName)
            .Must(group => group!.Trim().Length <= StatusComponent.GroupMaxLength).WithMessage($"Use {StatusComponent.GroupMaxLength} characters or fewer")
            .When(x => x.GroupName is not null);

        RuleFor(x => x.MonitorIds).Cascade(CascadeMode.Stop)
            .Must(ids => ids is { Length: > 0 }).WithMessage("Choose at least one monitor")
            .Must(ids => ids.Length <= 20).WithMessage("Use 20 monitors or fewer");
    }
}

public sealed class SubscribeInputValidator : AbstractValidator<SubscribeInput>
{
    public SubscribeInputValidator()
    {
        RuleFor(x => x.Email).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Enter an email address like name@company.com")
            .Must(email => email.Trim().Length <= 254 && Alerts.AlertChannelService.IsEmail(email.Trim())).WithMessage("Enter an email address like name@company.com");
    }
}
