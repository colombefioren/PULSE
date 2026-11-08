using System.Net;
using Pulse.Application.Abstractions;

namespace Pulse.Application.Accounts;

/// <summary>Plain, short account emails. Every one has a text body and a minimal HTML twin.</summary>
public static class AccountEmails
{
    public static EmailMessage VerifyEmail(string to, string name, string link) => Build(
        to,
        "Confirm your email for Pulse",
        $"Hi {name},",
        "Confirm your email address to finish setting up Pulse.",
        "Confirm email",
        link,
        "If you did not create a Pulse account you can ignore this message.");

    public static EmailMessage AlreadyRegistered(string to, string signInLink, string resetLink) => Build(
        to,
        "Someone tried to sign up to Pulse with your email",
        "Hi,",
        "This email address already has a Pulse account, so no new account was created. "
        + $"You can sign in, or reset your password if you have forgotten it: {resetLink}",
        "Sign in",
        signInLink,
        "If this was not you, no action is needed.");

    public static EmailMessage ResetPassword(string to, string name, string link) => Build(
        to,
        "Reset your Pulse password",
        $"Hi {name},",
        "Use the link below to choose a new password. It works once and expires soon.",
        "Choose a new password",
        link,
        "If you did not ask for this, you can ignore this message. Your password has not changed.");

    public static EmailMessage Invite(string to, string organizationName, string inviterName, string role, string link) => Build(
        to,
        $"{inviterName} invited you to {organizationName} on Pulse",
        "Hi,",
        $"{inviterName} invited you to join {organizationName} on Pulse as {role}. The invitation is valid for 7 days.",
        "Accept invitation",
        link,
        "If you were not expecting this, you can ignore this message.");

    private static EmailMessage Build(string to, string subject, string greeting, string body, string action, string link, string footer)
    {
        var text = $"{greeting}\n\n{body}\n\n{action}: {link}\n\n{footer}\n";
        var html =
            $"<p>{WebUtility.HtmlEncode(greeting)}</p><p>{WebUtility.HtmlEncode(body)}</p>"
            + $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">{WebUtility.HtmlEncode(action)}</a></p>"
            + $"<p style=\"color:#5c5a54\">{WebUtility.HtmlEncode(footer)}</p>";
        return new EmailMessage(to, subject, text, html);
    }
}
