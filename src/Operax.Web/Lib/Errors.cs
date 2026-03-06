namespace Operax.Web.Lib;

public record ErrorResponse(string Code, string Message, string? Field = null);

public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFound = "NOT_FOUND";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string BusinessRuleViolation = "BUSINESS_RULE_VIOLATION";
    public const string SystemError = "SYSTEM_ERROR";
}
