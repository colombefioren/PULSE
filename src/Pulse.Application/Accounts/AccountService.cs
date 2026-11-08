using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pulse.Application.Abstractions;
using Pulse.Application.Common;
using Pulse.Application.Organizations;
using Pulse.Domain.Common;
using Pulse.Domain.Organizations;

namespace Pulse.Application.Accounts;

public sealed class AccountService(
    IAppDbContext db,
    IIdentityService identity,
    IEmailSender email,
    IOptions<AppOptions> options,
    OrgSlugs slugs,
    TimeProvider clock)
{
    private readonly string _publicUrl = options.Value.PublicUrl.TrimEnd('/');

    /// <summary>
    /// Creates the user, their first organization and the Owner membership together. The response never
    /// says whether the address was already registered; that person gets an email instead.
    /// </summary>
    public async Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        new RegisterValidator().Check(request);
        var address = Validation.NormalizeEmail(request.Email);

        var created = await db.InTransactionAsync<UserInfo?>(
            async () =>
            {
                var user = await identity.CreateUserAsync(address, request.Name.Trim(), request.Password, cancellationToken);
                if (user is null)
                {
                    return null;
                }

                var organization = new Organization(request.TeamName.Trim(), await slugs.UniqueAsync(request.TeamName, cancellationToken), clock.GetUtcNow());
                db.Organizations.Add(organization);
                db.Memberships.Add(new Membership(user.Id, organization.Id, Role.Owner, clock.GetUtcNow()));
                await db.SaveChangesAsync(cancellationToken);
                return user;
            },
            cancellationToken);

        if (created is null)
        {
            var existing = await identity.FindByEmailAsync(address, cancellationToken);
            if (existing is not null)
            {
                var resetToken = await identity.GeneratePasswordResetTokenAsync(existing.Id);
                await email.SendAsync(
                    AccountEmails.AlreadyRegistered(address, $"{_publicUrl}/login", ResetLink(existing.Id, resetToken)),
                    cancellationToken);
            }

            return;
        }

        await SendVerificationAsync(created, cancellationToken);
    }

    public async Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || !await identity.ConfirmEmailAsync(request.UserId, request.Token))
        {
            throw AppException.Invalid("invalid_token", "This confirmation link is invalid or has expired. Request a new one.");
        }
    }

    public async Task ResendVerificationAsync(EmailRequest request, CancellationToken cancellationToken)
    {
        new EmailRequestValidator().Check(request);
        var user = await identity.FindByEmailAsync(Validation.NormalizeEmail(request.Email), cancellationToken);
        if (user is { EmailConfirmed: false })
        {
            await SendVerificationAsync(user, cancellationToken);
        }
    }

    public async Task ForgotPasswordAsync(EmailRequest request, CancellationToken cancellationToken)
    {
        new EmailRequestValidator().Check(request);
        var user = await identity.FindByEmailAsync(Validation.NormalizeEmail(request.Email), cancellationToken);
        if (user is null)
        {
            return;
        }

        var token = await identity.GeneratePasswordResetTokenAsync(user.Id);
        await email.SendAsync(AccountEmails.ResetPassword(user.Email, user.Name, ResetLink(user.Id, token)), cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        new ResetPasswordValidator().Check(request);
        if (!await identity.ResetPasswordAsync(request.UserId, request.Token, request.Password))
        {
            throw AppException.Invalid("invalid_token", "This reset link is invalid or has expired. Request a new one.");
        }
    }

    public async Task LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        new LoginValidator().Check(request);
        var outcome = await identity.SignInAsync(Validation.NormalizeEmail(request.Email), request.Password, request.Remember);

        switch (outcome)
        {
            case SignInOutcome.Succeeded:
                return;
            case SignInOutcome.EmailNotConfirmed:
                throw AppException.Forbidden("email_not_verified", "Confirm your email first. Check your inbox for the link.");
            case SignInOutcome.LockedOut:
                throw new AppException(423, "locked_out", "Too many failed attempts. Try again in a few minutes.");
            default:
                throw AppException.Unauthorized("invalid_credentials", "Email or password is incorrect.");
        }
    }

    public Task LogoutAsync() => identity.SignOutAsync();

    public async Task<MeResponse> MeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await identity.FindByIdAsync(userId, cancellationToken)
            ?? throw AppException.Unauthorized("not_signed_in", "Sign in to continue.");

        var organizations = await (
                from membership in db.Memberships
                join organization in db.Organizations on membership.OrganizationId equals organization.Id
                where membership.UserId == userId
                orderby organization.Name
                select new OrganizationSummary(organization.Id, organization.Name, organization.Slug, membership.Role))
            .ToListAsync(cancellationToken);

        return new MeResponse(user.Id, user.Email, user.Name, user.EmailConfirmed, organizations);
    }

    private async Task SendVerificationAsync(UserInfo user, CancellationToken cancellationToken)
    {
        var token = await identity.GenerateEmailConfirmationTokenAsync(user.Id);
        var link = $"{_publicUrl}/verify-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";
        await email.SendAsync(AccountEmails.VerifyEmail(user.Email, user.Name, link), cancellationToken);
    }

    private string ResetLink(Guid userId, string token) =>
        $"{_publicUrl}/reset-password?userId={userId}&token={Uri.EscapeDataString(token)}";
}
