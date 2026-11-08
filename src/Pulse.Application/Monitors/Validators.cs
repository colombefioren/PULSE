using System.Text.RegularExpressions;
using FluentValidation;
using Pulse.Domain.Monitors;

namespace Pulse.Application.Monitors;

public sealed partial class MonitorInputValidator : AbstractValidator<MonitorInput>
{
    /// <summary>Headers the HTTP stack sets itself. Letting people override them breaks requests or smuggles them.</summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "Content-Length", "Transfer-Encoding", "Connection", "Upgrade", "Expect", "TE", "Trailer",
    };

    public MonitorInputValidator()
    {
        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Enter a name")
            .Must(name => name.Trim().Length <= HttpMonitor.NameMaxLength).WithMessage($"Use {HttpMonitor.NameMaxLength} characters or fewer");

        RuleFor(x => x.Url).Custom((url, context) =>
        {
            var (_, error) = MonitorUrl.Parse(url);
            if (error is not null)
            {
                context.AddFailure(error);
            }
        });

        RuleFor(x => x.Method).IsInEnum().WithMessage("Choose GET, HEAD or POST");

        RuleFor(x => x.IntervalSeconds)
            .Must(seconds => Scheduling.AllowedIntervals.Contains(seconds))
            .WithMessage("Check every 30 seconds, 1, 5, 10 or 30 minutes");

        RuleFor(x => x.TimeoutMs).Cascade(CascadeMode.Stop)
            .InclusiveBetween(HttpMonitor.MinTimeoutMs, HttpMonitor.MaxTimeoutMs).WithMessage("Use a timeout between 1 and 30 seconds")
            .Must((input, timeout) => timeout <= input.IntervalSeconds * 1000).WithMessage("The timeout cannot be longer than the check interval");

        RuleFor(x => x.ExpectedCodes)
            .Must(codes => string.IsNullOrWhiteSpace(codes) || ExpectedCodes.Parse(codes) is not null)
            .WithMessage("Use status codes like 200-299 or 200, 301");

        RuleFor(x => x.Keyword)
            .Must(keyword => keyword is null || keyword.Length <= HttpMonitor.KeywordMaxLength)
            .WithMessage($"Use {HttpMonitor.KeywordMaxLength} characters or fewer");

        RuleFor(x => x.Headers).Custom((headers, context) =>
        {
            if (headers is null)
            {
                return;
            }

            if (headers.Count > HttpMonitor.MaxHeaders)
            {
                context.AddFailure($"Use {HttpMonitor.MaxHeaders} headers or fewer");
                return;
            }

            foreach (var (name, value) in headers)
            {
                if (!HeaderName().IsMatch(name))
                {
                    context.AddFailure($"\"{name}\" is not a valid header name");
                }
                else if (Reserved.Contains(name))
                {
                    context.AddFailure($"{name} is set by Pulse and cannot be changed");
                }
                else if (value.Length > HttpMonitor.MaxHeaderValueLength || value.AsSpan().IndexOfAny('\r', '\n') >= 0)
                {
                    context.AddFailure($"The value of {name} is too long or contains a line break");
                }
            }
        });

        RuleFor(x => x.FailureThreshold)
            .InclusiveBetween(HttpMonitor.MinThreshold, HttpMonitor.MaxThreshold)
            .WithMessage($"Use a number from {HttpMonitor.MinThreshold} to {HttpMonitor.MaxThreshold}");
        RuleFor(x => x.RecoveryThreshold)
            .InclusiveBetween(HttpMonitor.MinThreshold, HttpMonitor.MaxThreshold)
            .WithMessage($"Use a number from {HttpMonitor.MinThreshold} to {HttpMonitor.MaxThreshold}");
    }

    [GeneratedRegex("^[!#$%&'*+\\-.^_`|~0-9A-Za-z]{1,100}$")]
    private static partial Regex HeaderName();
}
