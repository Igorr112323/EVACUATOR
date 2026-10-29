using System.Text.RegularExpressions;
using Evakuator.Domain.Enums;
using Evakuator.Domain.ValueObjects;
using Evakuator.Norms.Exceptions;
using Evakuator.Norms.Models;

namespace Evakuator.Norms.Services;

public sealed class NormDatabaseValidator
{
    public void ValidateAndThrow(NormDatabaseDto database)
    {
        var errors = Validate(database);
        if (errors.Count > 0) throw new NormDatabaseValidationException(errors);
    }

    public IReadOnlyList<string> Validate(NormDatabaseDto database)
    {
        var errors = new List<string>();
        if (database is null) return ["Database is null"];
        if (string.IsNullOrWhiteSpace(database.Version)) errors.Add("version is required");
        if (string.IsNullOrWhiteSpace(database.Notice)) errors.Add("notice is required");
        if (database.Parameters is null) errors.Add("parameters are required");
        else ValidateParameters(database.Parameters, errors);
        if (database.FireInstructionTemplate is null ||
            string.IsNullOrWhiteSpace(database.FireInstructionTemplate.Approval) ||
            string.IsNullOrWhiteSpace(database.FireInstructionTemplate.Title) ||
            string.IsNullOrWhiteSpace(database.FireInstructionTemplate.Instructions) ||
            database.FireInstructionTemplate.Approval?.Contains("УТВЕРЖДАЮ", StringComparison.OrdinalIgnoreCase) != true ||
            database.FireInstructionTemplate.Title?.Contains("ПЛАН ЭВАКУАЦИИ", StringComparison.OrdinalIgnoreCase) != true ||
            database.FireInstructionTemplate.Instructions?.Contains("пожар", StringComparison.OrdinalIgnoreCase) != true ||
            !HasPhone(database.FireInstructionTemplate.Instructions, "101") ||
            !HasPhone(database.FireInstructionTemplate.Instructions, "112"))
            errors.Add("fire instruction template must include approval, title and both 101 and 112");
        if (database.Signs.IsDefaultOrEmpty) { errors.Add("at least one sign is required"); return errors; }

        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sign in database.Signs)
        {
            if (sign is null) { errors.Add("null sign"); continue; }
            var context = string.IsNullOrWhiteSpace(sign.Code) ? "<empty code>" : sign.Code;
            try { _ = new SignCode(sign.Code ?? string.Empty); }
            catch (ArgumentException) { errors.Add($"{context}: invalid code"); }
            if (!codes.Add(sign.Code ?? string.Empty)) errors.Add($"{context}: duplicate code");
            var prefix = sign.Category.ToString();
            if (sign.Code is null || !Regex.IsMatch(sign.Code, "^" + prefix + @"\d", RegexOptions.CultureInvariant))
                errors.Add($"{context}: category does not match code");
            if (!Enum.IsDefined(sign.Category) || !Enum.IsDefined(sign.Shape)) errors.Add($"{context}: invalid category or shape");
            if (string.IsNullOrWhiteSpace(sign.Name) || string.IsNullOrWhiteSpace(sign.OfficialDescription))
                errors.Add($"{context}: name and description are required");
            if (sign.GostClauseRef?.Contains("[ПРОВЕРИТЬ ПО ТЕКСТУ ГОСТ]", StringComparison.Ordinal) != true)
                errors.Add($"{context}: missing verification marker");
            var expectedRatio = sign.Shape == SignShape.Rectangle2x1 ? 2d : 1d;
            if (!double.IsFinite(sign.AspectRatio) || Math.Abs(sign.AspectRatio - expectedRatio) > 1e-9)
                errors.Add($"{context}: aspect ratio does not match carrier shape");
            if (!Positive(sign.MinHeightMm) || !Positive(sign.MaxHeightMm) || !Positive(sign.DefaultHeightMm) ||
                sign.MinHeightMm > sign.DefaultHeightMm || sign.DefaultHeightMm > sign.MaxHeightMm)
                errors.Add($"{context}: invalid height bounds");
            if (!IsColor(sign.BackgroundColor)) errors.Add($"{context}: invalid background color");
            if (sign.Primitives.IsDefaultOrEmpty) { errors.Add($"{context}: empty vector recipe"); continue; }
            foreach (var primitive in sign.Primitives)
                ValidatePrimitive(primitive, context, errors);
        }
        return errors;
    }

    private static void ValidateParameters(NormParametersDto p, List<string> errors)
    {
        if (!Positive(p.DefaultHeightMm) || !NonNegative(p.UniformToleranceMm) ||
            !Positive(p.SnapToleranceMm) || !Positive(p.MaxAutoSnapDistanceMm) ||
            p.MaxAutoSnapDistanceMm < p.SnapToleranceMm || !Positive(p.PathLineWeightMm) || !IsColor(p.PathColor) ||
            !Positive(p.ArrowSpacingMm) || !Positive(p.ArrowSizeMm) ||
            !double.IsFinite(p.OverlapThreshold) || p.OverlapThreshold < 0 || p.OverlapThreshold > 1 ||
            !Positive(p.OffsetStepMm) || p.MaxOffsetSteps < 1 || p.MaxOffsetSteps > 1000 || !NonNegative(p.DuplicateToleranceMm) ||
            !Positive(p.FloorFrameWidthMm) || !Positive(p.FloorFrameHeightMm) ||
            !Positive(p.LocalFrameWidthMm) || !Positive(p.LocalFrameHeightMm))
            errors.Add("invalid rule parameters");
    }

    private static void ValidatePrimitive(VectorPrimitiveDto primitive, string context, List<string> errors)
    {
        if (primitive is null) { errors.Add($"{context}: null primitive"); return; }
        if (!IsPaint(primitive.Fill) || !IsPaint(primitive.Stroke) || !NonNegative(primitive.StrokeWidth) || primitive.StrokeWidth > 1)
            errors.Add($"{context}: invalid primitive paint");
        var visibleStroke = primitive.Stroke != "none" && primitive.StrokeWidth > 0;
        if (primitive.Type is "Polyline" or "Arc" ? !visibleStroke : primitive.Fill == "none" && !visibleStroke)
            errors.Add($"{context}: invisible primitive");
        bool Unit(double v) => double.IsFinite(v) && v >= 0 && v <= 1;
        switch (primitive.Type)
        {
            case "Polygon":
            case "Polyline":
                var minimum = primitive.Type == "Polygon" ? 3 : 2;
                if (primitive.Points.IsDefault || primitive.Points.Length < minimum ||
                    primitive.Points.Any(p => !Unit(p.X) || !Unit(p.Y)))
                    errors.Add($"{context}: invalid {primitive.Type} points");
                break;
            case "Rect":
                if (!Unit(primitive.X) || !Unit(primitive.Y) || !Positive(primitive.Width) || !Positive(primitive.Height) ||
                    primitive.X + primitive.Width > 1 || primitive.Y + primitive.Height > 1)
                    errors.Add($"{context}: invalid rect bounds");
                break;
            case "Circle":
            case "Arc":
                if (!Unit(primitive.X) || !Unit(primitive.Y) || !Positive(primitive.Radius) ||
                    primitive.X - primitive.Radius < 0 || primitive.Y - primitive.Radius < 0 ||
                    primitive.X + primitive.Radius > 1 || primitive.Y + primitive.Radius > 1)
                    errors.Add($"{context}: invalid circle/arc bounds");
                if (primitive.Type == "Arc" && (!double.IsFinite(primitive.StartAngleDegrees) || primitive.StartAngleDegrees < 0 || primitive.StartAngleDegrees >= 360 ||
                    !double.IsFinite(primitive.SweepAngleDegrees) || Math.Abs(primitive.SweepAngleDegrees) >= 360 || primitive.SweepAngleDegrees == 0 || primitive.Fill != "none"))
                    errors.Add($"{context}: invalid arc angles or fill");
                break;
            case "Text":
                if (!Unit(primitive.X) || !Unit(primitive.Y) || !Positive(primitive.FontSize) || primitive.FontSize > 1 ||
                    string.IsNullOrWhiteSpace(primitive.Text)) errors.Add($"{context}: invalid text");
                break;
            default:
                errors.Add($"{context}: unknown primitive type {primitive.Type}");
                break;
        }
    }

    private static bool HasPhone(string? text, string phone) => text is not null && Regex.IsMatch(text, $@"(?<!\d){phone}(?!\d)");
    private static bool Positive(double value) => double.IsFinite(value) && value > 0;
    private static bool NonNegative(double value) => double.IsFinite(value) && value >= 0;
    private static bool IsColor(string? color) => color is not null && Regex.IsMatch(color, @"^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant);
    private static bool IsPaint(string? paint) => paint == "none" || IsColor(paint);
}
