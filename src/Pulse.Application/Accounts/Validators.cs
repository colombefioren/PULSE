using System.Linq.Expressions;
using FluentValidation;
using Pulse.Domain.Organizations;

namespace Pulse.Application.Accounts;

public static class PasswordRules
{
    public const int MinLength = 10;
    public const int MaxLength = 128;
}

/// <summary>Validator base with the rules the account forms share.</summary>
public abstract class AccountValidator<T> : AbstractValidator<T>
{
    protected void PasswordRule(Expression<Func<T, string>> property) =>
        RuleFor(property).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Enter a password")
            .MinimumLength(PasswordRules.MinLength).WithMessage($"Use at least {PasswordRules.MinLength} characters")
            .MaximumLength(PasswordRules.MaxLength).WithMessage($"Use {PasswordRules.MaxLength} characters or fewer");

    protected void EmailRule(Expression<Func<T, string>> property) =>
        RuleFor(property).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Enter an email address")
            .MaximumLength(254).WithMessage("That email address is too long")
            .EmailAddress().WithMessage("Enter an email like name@company.com");
}

public sealed class RegisterValidator : AccountValidator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Enter your name")
            .MaximumLength(80).WithMessage("Use 80 characters or fewer");
        RuleFor(x => x.TeamName).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Enter a team name")
            .MaximumLength(Organization.NameMaxLength).WithMessage($"Use {Organization.NameMaxLength} characters or fewer");
        EmailRule(x => x.Email);
        PasswordRule(x => x.Password);
    }
}

public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Enter your email");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Enter your password");
    }
}

public sealed class EmailRequestValidator : AccountValidator<EmailRequest>
{
    public EmailRequestValidator() => EmailRule(x => x.Email);
}

public sealed class ResetPasswordValidator : AccountValidator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("This reset link is incomplete");
        PasswordRule(x => x.Password);
    }
}
