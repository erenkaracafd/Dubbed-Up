using System.Text.RegularExpressions;

namespace DubbedUp.Core.ProjectFormat;

internal static partial class ValidationRules
{
    public static void ValidateSchemaVersion(int schemaVersion, string documentName, ICollection<string> errors)
    {
        if (schemaVersion != ProjectSchema.CurrentVersion)
        {
            errors.Add($"{documentName} schemaVersion '{schemaVersion}' is unsupported; expected '{ProjectSchema.CurrentVersion}'.");
        }
    }

    public static void ValidateIdentifier(string? value, string fieldName, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || !IdentifierPattern().IsMatch(value))
        {
            errors.Add($"{fieldName} must be a lowercase kebab-case identifier.");
        }
    }

    public static void ValidateRequiredText(string? value, string fieldName, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
        }
    }

    public static void ValidateRelativePath(string? value, string fieldName, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
            return;
        }

        var hasInvalidSegment = value
            .Split('/')
            .Any(segment => string.IsNullOrWhiteSpace(segment) || segment is "." or "..");
        var isAbsolute = value.StartsWith("/", StringComparison.Ordinal) || DrivePathPattern().IsMatch(value);
        var hasInvalidCharacters = value.Contains('\\') ||
            value.IndexOfAny(['<', '>', ':', '"', '|', '?', '*']) >= 0;

        if (hasInvalidSegment || isAbsolute || hasInvalidCharacters)
        {
            errors.Add($"{fieldName} must be a portable forward-slash relative path without traversal.");
        }
    }

    public static void AddDuplicateErrors(
        IEnumerable<string> values,
        string fieldName,
        ICollection<string> errors)
    {
        foreach (var duplicate in values
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .GroupBy(value => value, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1)
                     .Select(group => group.Key))
        {
            errors.Add($"{fieldName} contains duplicate identifier '{duplicate}'.");
        }
    }

    public static void ThrowIfInvalid(IReadOnlyCollection<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new ProjectValidationException(errors.ToArray());
        }
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();

    [GeneratedRegex("^[A-Za-z]:", RegexOptions.CultureInvariant)]
    private static partial Regex DrivePathPattern();
}
