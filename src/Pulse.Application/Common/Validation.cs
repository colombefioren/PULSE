using FluentValidation;

namespace Pulse.Application.Common;

public static class Validation
{
    /// <summary>Runs a validator and throws a 400 with one message list per field when it fails.</summary>
    public static void Check<T>(this IValidator<T> validator, T instance)
    {
        var result = validator.Validate(instance);
        if (result.IsValid)
        {
            return;
        }

        var errors = result.Errors
            .GroupBy(failure => char.ToLowerInvariant(failure.PropertyName[0]) + failure.PropertyName[1..])
            .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());

        throw new AppException(400, "validation_failed", "Some fields need attention.", errors);
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
