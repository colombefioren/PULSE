using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pulse.Application.Abstractions;
using Pulse.Application.Common;
using Pulse.Domain.Common;

namespace Pulse.Infrastructure.Identity;

public sealed class IdentityService(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) : IIdentityService
{
    public async Task<UserInfo?> CreateUserAsync(string email, string name, string password, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser { Id = Ids.New(), UserName = email, Email = email, Name = name };
        var result = await users.CreateAsync(user, password);

        if (result.Succeeded)
        {
            return ToInfo(user);
        }

        if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return null;
        }

        throw PasswordRejected(result);
    }

    public async Task<UserInfo?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        (await users.FindByIdAsync(id.ToString())) is { } user ? ToInfo(user) : null;

    public async Task<UserInfo?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        (await users.FindByEmailAsync(email)) is { } user ? ToInfo(user) : null;

    public async Task<IReadOnlyList<UserInfo>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await users.Users.Where(u => ids.Contains(u.Id)).Select(u => new UserInfo(u.Id, u.Email!, u.Name, u.EmailConfirmed)).ToListAsync(cancellationToken);

    public async Task<string> GenerateEmailConfirmationTokenAsync(Guid userId) =>
        await users.GenerateEmailConfirmationTokenAsync(await Require(userId));

    public async Task<bool> ConfirmEmailAsync(Guid userId, string token)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        return user is not null && (await users.ConfirmEmailAsync(user, token)).Succeeded;
    }

    public async Task<string> GeneratePasswordResetTokenAsync(Guid userId) =>
        await users.GeneratePasswordResetTokenAsync(await Require(userId));

    public async Task<bool> ResetPasswordAsync(Guid userId, string token, string newPassword)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return false;
        }

        var result = await users.ResetPasswordAsync(user, token, newPassword);
        if (result.Succeeded)
        {
            return true;
        }

        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
        {
            return false;
        }

        throw PasswordRejected(result);
    }

    public async Task<SignInOutcome> SignInAsync(string email, string password, bool remember)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            return SignInOutcome.InvalidCredentials;
        }

        // Check the password before revealing anything about the account, and count failures toward lockout.
        var result = await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return SignInOutcome.LockedOut;
        }

        if (!result.Succeeded)
        {
            return SignInOutcome.InvalidCredentials;
        }

        if (!user.EmailConfirmed)
        {
            return SignInOutcome.EmailNotConfirmed;
        }

        await signIn.SignInAsync(user, isPersistent: remember);
        return SignInOutcome.Succeeded;
    }

    public Task SignOutAsync() => signIn.SignOutAsync();

    private async Task<ApplicationUser> Require(Guid userId) =>
        await users.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException($"User {userId} does not exist.");

    private static UserInfo ToInfo(ApplicationUser user) => new(user.Id, user.Email!, user.Name, user.EmailConfirmed);

    private static AppException PasswordRejected(IdentityResult result) =>
        new(400, "validation_failed", "Some fields need attention.", new Dictionary<string, string[]>
        {
            ["password"] = result.Errors.Select(e => e.Description).ToArray(),
        });
}
