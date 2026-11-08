namespace Pulse.Application.Common;

/// <summary>
/// An expected failure with an HTTP meaning. The API turns it into a problem+json response, so services
/// never reach for HTTP types themselves. <see cref="Code"/> is stable and safe for clients to switch on.
/// </summary>
public sealed class AppException(int status, string code, string message, IReadOnlyDictionary<string, string[]>? errors = null)
    : Exception(message)
{
    public int Status { get; } = status;

    public string Code { get; } = code;

    public IReadOnlyDictionary<string, string[]>? Errors { get; } = errors;

    public static AppException Invalid(string code, string message) => new(400, code, message);

    public static AppException Unauthorized(string code, string message) => new(401, code, message);

    public static AppException Forbidden(string code, string message) => new(403, code, message);

    public static AppException NotFound(string code, string message) => new(404, code, message);

    public static AppException Conflict(string code, string message) => new(409, code, message);

    public static AppException Gone(string code, string message) => new(410, code, message);
}
