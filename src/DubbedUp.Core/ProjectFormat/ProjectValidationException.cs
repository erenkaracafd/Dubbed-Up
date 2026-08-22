namespace DubbedUp.Core.ProjectFormat;

public sealed class ProjectValidationException : Exception
{
    public ProjectValidationException(IReadOnlyList<string> errors)
        : base($"Project data is invalid:{Environment.NewLine}{string.Join(Environment.NewLine, errors.Select(error => $"- {error}"))}")
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
