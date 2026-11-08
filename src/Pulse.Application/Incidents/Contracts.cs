using FluentValidation;
using Pulse.Application.Alerts;
using Pulse.Domain.Incidents;

namespace Pulse.Application.Incidents;

/// <summary>An incident written by hand. It starts open, with a first status update.</summary>
public sealed record ManualIncidentInput(
    string Title,
    IncidentSeverity Severity,
    IncidentStatus Status = IncidentStatus.Investigating,
    string Message = "",
    Guid? MonitorId = null);

public sealed record IncidentEditInput(string Title, IncidentSeverity Severity);

/// <summary>
/// A timeline entry from a person. With a <see cref="Status"/> it is a public update on a manual incident;
/// without one it is an internal note, which any incident accepts.
/// </summary>
public sealed record IncidentUpdateInput(string Message, IncidentStatus? Status = null);

public sealed record IncidentDto(
    Guid Id,
    Guid OrganizationId,
    Guid? MonitorId,
    string? MonitorName,
    string Title,
    IncidentSeverity Severity,
    IncidentStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? AcknowledgedAt,
    string? AcknowledgedBy,
    DateTimeOffset? ResolvedAt,
    bool IsManual);

public sealed record IncidentUpdateDto(
    Guid Id,
    IncidentUpdateKind Kind,
    IncidentStatus Status,
    string Message,
    string? Author,
    DateTimeOffset CreatedAt);

public sealed record IncidentDetailDto(IncidentDto Incident, IReadOnlyList<IncidentUpdateDto> Updates, IReadOnlyList<DeliveryDto> Alerts);

public sealed record IncidentPageDto(IReadOnlyList<IncidentDto> Items, DateTimeOffset? NextBefore);

public sealed class ManualIncidentInputValidator : AbstractValidator<ManualIncidentInput>
{
    public ManualIncidentInputValidator()
    {
        RuleFor(x => x.Title).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Enter a title")
            .Must(title => title.Trim().Length <= Incident.TitleMaxLength).WithMessage($"Use {Incident.TitleMaxLength} characters or fewer");
        RuleFor(x => x.Severity).IsInEnum().WithMessage("Choose a severity");
        RuleFor(x => x.Status).Cascade(CascadeMode.Stop)
            .IsInEnum().WithMessage("Choose a status")
            .Must(status => status != IncidentStatus.Resolved).WithMessage("A new incident can't start resolved");
        RuleFor(x => x.Message).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Say what is happening")
            .Must(message => message.Trim().Length <= Incident.MessageMaxLength).WithMessage($"Use {Incident.MessageMaxLength} characters or fewer");
    }
}

public sealed class IncidentEditInputValidator : AbstractValidator<IncidentEditInput>
{
    public IncidentEditInputValidator()
    {
        RuleFor(x => x.Title).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Enter a title")
            .Must(title => title.Trim().Length <= Incident.TitleMaxLength).WithMessage($"Use {Incident.TitleMaxLength} characters or fewer");
        RuleFor(x => x.Severity).IsInEnum().WithMessage("Choose a severity");
    }
}

public sealed class IncidentUpdateInputValidator : AbstractValidator<IncidentUpdateInput>
{
    public IncidentUpdateInputValidator()
    {
        RuleFor(x => x.Message).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Write a message")
            .Must(message => message.Trim().Length <= Incident.MessageMaxLength).WithMessage($"Use {Incident.MessageMaxLength} characters or fewer");
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null).WithMessage("Choose a status");
    }
}
