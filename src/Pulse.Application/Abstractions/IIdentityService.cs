namespace Pulse.Application.Abstractions;

public sealed record UserInfo(Guid Id, string Email, string Name, bool EmailConfirmed);

public enum SignInOutcome
{
    Succeeded,
    InvalidCredentials,
    EmailNotConfirmed,
    LockedOut,
}

/// <summary>Everything the application needs from the identity system, so it never touches ASP.NET Identity types.</summary>
public interface IIdentityService
{
    /// <summary>Creates an unconfirmed user. Returns null when the email is already registered.</summary>
    Task<UserInfo?> CreateUserAsync(string email, string name, string password, CancellationToken cancellationToken);

    Task<UserInfo?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<UserInfo?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserInfo>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<string> GenerateEmailConfirmationTokenAsync(Guid userId);

    /// <summary>Confirms the address. False when the token is wrong or expired.</summary>
    Task<bool> ConfirmEmailAsync(Guid userId, string token);

    Task<string> GeneratePasswordResetTokenAsync(Guid userId);

    /// <summary>Returns false when the token is invalid. Throws <see cref="Common.AppException"/> when the password is rejected.</summary>
    Task<bool> ResetPasswordAsync(Guid userId, string token, string newPassword);

    Task<SignInOutcome> SignInAsync(string email, string password, bool remember);

    Task SignOutAsync();

    /// <summary>Finds the user or creates one with a confirmed address and a password nobody knows, for the shared demo account.</summary>
    Task<UserInfo> EnsureConfirmedUserAsync(string email, string name, CancellationToken cancellationToken);

    /// <summary>Opens a session for a user without a password. Only the demo account is ever signed in this way.</summary>
    Task SignInUserAsync(Guid userId);
}
