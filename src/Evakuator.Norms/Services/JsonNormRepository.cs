using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Evakuator.Norms.Exceptions;
using Evakuator.Norms.Models;

namespace Evakuator.Norms.Services;

public sealed class JsonNormRepository
{
    private static readonly JsonSerializerOptions Options = CreateOptions();
    private readonly NormDatabaseValidator validator = new();
    public string Path { get; }

    public JsonNormRepository(string path) => Path = System.IO.Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }

    public NormDatabaseDto Load(string? amendmentPath = null)
    {
        try
        {
            var baseJson = File.ReadAllText(Path);
            NormJsonShapeValidator.Validate(baseJson, amendment: false);
            var database = JsonSerializer.Deserialize<NormDatabaseDto>(baseJson, Options)
                ?? throw new NormDatabaseValidationException(["empty JSON document"]);
            validator.ValidateAndThrow(database);
            if (amendmentPath is null) return database;
            var amendmentJson = File.ReadAllText(amendmentPath);
            NormJsonShapeValidator.Validate(amendmentJson, amendment: true);
            var amendment = JsonSerializer.Deserialize<NormAmendmentDto>(amendmentJson, Options)
                ?? throw new NormDatabaseValidationException(["empty amendment"]);
            if (amendment.Signs.IsDefault || amendment.Signs.Any(s => s is null) ||
                amendment.Signs.Select(s => s.Code).Distinct(StringComparer.Ordinal).Count() != amendment.Signs.Length)
                throw new NormDatabaseValidationException(["null or duplicate amendment signs"]);
            var overrides = amendment.Signs.ToDictionary(s => s.Code, StringComparer.Ordinal);
            var signs = database.Signs.Select(s => overrides.Remove(s.Code, out var replacement) ? replacement : s)
                .Concat(overrides.Values).ToImmutableArray();
            var merged = database with
            {
                Signs = signs,
                Parameters = amendment.Parameters ?? database.Parameters,
                FireInstructionTemplate = amendment.FireInstructionTemplate ?? database.FireInstructionTemplate
            };
            validator.ValidateAndThrow(merged);
            return merged;
        }
        catch (JsonException ex)
        {
            throw new NormDatabaseValidationException([$"JSON: {ex.Message}"]);
        }
    }
}
