using FluentValidation;
using Pulse.Domain.Organizations;

namespace Pulse.Application.Organizations;

public sealed class CreateOrganizationValidator : AbstractValidator<CreateOrganizationRequest>
{
    public CreateOrganizationValidator() =>
        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Enter a team name")
            .MaximumLength(Organization.NameMaxLength).WithMessage($"Use {Organization.NameMaxLength} characters or fewer");
}

public sealed class UpdateOrganizationValidator : AbstractValidator<UpdateOrganizationRequest>
{
    public UpdateOrganizationValidator() =>
        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Enter a team name")
            .MaximumLength(Organization.NameMaxLength).WithMessage($"Use {Organization.NameMaxLength} characters or fewer");
}

public sealed class CreateInviteValidator : AbstractValidator<CreateInviteRequest>
{
    public CreateInviteValidator()
    {
        RuleFor(x => x.Email).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Enter an email address")
            .MaximumLength(254).WithMessage("That email address is too long")
            .EmailAddress().WithMessage("Enter an email like name@company.com");
        RuleFor(x => x.Role).IsInEnum().WithMessage("Choose Owner, Admin or Viewer");
    }
}

public sealed class ChangeRoleValidator : AbstractValidator<ChangeRoleRequest>
{
    public ChangeRoleValidator() =>
        RuleFor(x => x.Role).IsInEnum().WithMessage("Choose Owner, Admin or Viewer");
}
