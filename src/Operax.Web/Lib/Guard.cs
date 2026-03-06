using System.Diagnostics.CodeAnalysis;

namespace Operax.Web.Lib;

public class BusinessException : Exception
{
    public BusinessException(string message) : base(message) { }
}

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}

public static class Guard
{
    public static T NotNull<T>([NotNull] T? value, string fieldName)
    {
        if (value is null)
            throw new ValidationException($"{fieldName} cannot be null.");
            
        return value;
    }

    public static string NotNullOrWhiteSpace([NotNull] string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException($"{fieldName} cannot be empty.");
            
        return value;
    }

    public static void Against(bool condition, string message)
    {
        if (condition)
            throw new BusinessException(message);
    }
}
