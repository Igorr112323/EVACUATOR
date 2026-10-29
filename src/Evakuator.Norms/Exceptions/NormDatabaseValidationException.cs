namespace Evakuator.Norms.Exceptions;

public sealed class NormDatabaseValidationException : Exception
{
    public IReadOnlyList<string> Errors { get; }
    public NormDatabaseValidationException(IEnumerable<string> errors)
        : base("Invalid norm database: " + string.Join("; ", errors)) => Errors = errors.ToArray();
}
