using System.Text.RegularExpressions;

namespace Evakuator.Domain.ValueObjects;

public readonly record struct SignCode
{
    public string Value { get; }
    public SignCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A sign code is required.", nameof(value));
        value = value.Trim().ToUpperInvariant();
        if (!Regex.IsMatch(value, @"^(?:E|F|EC|P|W)\d{2}(?:-\d{2})?$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Invalid sign code.", nameof(value));
        Value = value;
    }
    public override string ToString() => Value ?? string.Empty;
}
