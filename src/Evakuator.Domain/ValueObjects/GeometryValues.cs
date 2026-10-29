using System.Globalization;
using System.Text.Json.Serialization;

namespace Evakuator.Domain.ValueObjects;

/// <summary>Drawing coordinates in millimetres, X to the right and Y upwards.</summary>
public readonly record struct Point2D
{
    public double X { get; }
    public double Y { get; }

    [JsonConstructor]
    public Point2D(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
            throw new ArgumentOutOfRangeException(nameof(x), "Point coordinates must be finite.");
        X = x;
        Y = y;
    }

    public double DistanceTo(Point2D other) => Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2));
    public Point2D Translate(double dx, double dy) => new(X + dx, Y + dy);
    public Point2D RotateAround(Point2D origin, double degrees)
    {
        var angle = degrees * Math.PI / 180;
        var dx = X - origin.X;
        var dy = Y - origin.Y;
        return new Point2D(origin.X + dx * Math.Cos(angle) - dy * Math.Sin(angle),
            origin.Y + dx * Math.Sin(angle) + dy * Math.Cos(angle));
    }
}

public readonly record struct Vector2D(double X, double Y)
{
    public double Length => Math.Sqrt(X * X + Y * Y);
    public Vector2D Normalize() => Length > 1e-12
        ? new(X / Length, Y / Length)
        : throw new InvalidOperationException("Cannot normalize a zero vector.");
}

public readonly record struct BoundingBox2D
{
    public double MinX { get; }
    public double MinY { get; }
    public double MaxX { get; }
    public double MaxY { get; }
    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;
    public double Area => Width * Height;

    public BoundingBox2D(double minX, double minY, double maxX, double maxY)
    {
        if (!double.IsFinite(minX) || !double.IsFinite(minY) || !double.IsFinite(maxX) ||
            !double.IsFinite(maxY) || maxX < minX || maxY < minY)
            throw new ArgumentOutOfRangeException(nameof(minX), "Invalid bounding box.");
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    public static BoundingBox2D FromPoints(IEnumerable<Point2D> points)
    {
        var array = points.ToArray();
        if (array.Length == 0) throw new ArgumentException("At least one point is required.", nameof(points));
        return new(array.Min(p => p.X), array.Min(p => p.Y), array.Max(p => p.X), array.Max(p => p.Y));
    }

    /// <summary>Intersection divided by the smaller area; containment is treated as full overlap.</summary>
    public double OverlapOfSmaller(BoundingBox2D other)
    {
        var width = Math.Max(0, Math.Min(MaxX, other.MaxX) - Math.Max(MinX, other.MinX));
        var height = Math.Max(0, Math.Min(MaxY, other.MaxY) - Math.Max(MinY, other.MinY));
        var denominator = Math.Min(Area, other.Area);
        return denominator > 0 ? width * height / denominator : 0;
    }

    public bool Contains(BoundingBox2D other) =>
        other.MinX >= MinX && other.MinY >= MinY && other.MaxX <= MaxX && other.MaxY <= MaxY;
}

public readonly record struct PaperScale
{
    public double Denominator { get; }
    public PaperScale(double denominator)
    {
        if (!double.IsFinite(denominator) || denominator <= 0)
            throw new ArgumentOutOfRangeException(nameof(denominator), "Scale denominator must be positive and finite.");
        Denominator = denominator;
    }
    public double ToPaper(double modelMillimetres) => modelMillimetres / Denominator;
    public double ToModel(double paperMillimetres) => paperMillimetres * Denominator;
}

public readonly record struct MillimeterSize
{
    public double Value { get; }
    public MillimeterSize(double value)
    {
        if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }
}

public readonly record struct RgbColor(byte Red, byte Green, byte Blue)
{
    public static RgbColor Parse(string value)
    {
        if (value is null || value.Length != 7 || value[0] != '#' ||
            !int.TryParse(value.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
            throw new FormatException("Expected a #RRGGBB colour.");
        return new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }
    public override string ToString() => $"#{Red:X2}{Green:X2}{Blue:X2}";
}
