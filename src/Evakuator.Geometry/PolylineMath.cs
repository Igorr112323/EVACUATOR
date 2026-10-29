using System.Collections.Immutable;
using Evakuator.Domain.ValueObjects;

namespace Evakuator.Geometry;

public static class PolylineMath
{
    private const double LengthEpsilon = 1e-8;

    public static bool HasDegenerateSegments(IReadOnlyList<Point2D> points)
    {
        for (var i = 1; i < points.Count; i++)
            if (points[i - 1].DistanceTo(points[i]) <= LengthEpsilon) return true;
        return false;
    }

    public static ImmutableArray<Point2D> RemoveConsecutiveDuplicates(IReadOnlyList<Point2D> points)
    {
        var result = ImmutableArray.CreateBuilder<Point2D>();
        foreach (var p in points)
            if (result.Count == 0 || result[^1].DistanceTo(p) > LengthEpsilon) result.Add(p);
        return result.ToImmutable();
    }

    public static bool HasSelfIntersection(IReadOnlyList<Point2D> points)
    {
        // Adjacent segments normally meet at one vertex. A collinear reversal instead
        // retraces a non-zero portion of the preceding segment and must not be ignored.
        for (var i = 0; i + 2 < points.Count; i++)
            if (AdjacentSegmentsOverlap(points[i], points[i + 1], points[i + 2])) return true;
        for (var i = 0; i + 1 < points.Count; i++)
            for (var j = i + 2; j + 1 < points.Count; j++)
                if (Intersects(points[i], points[i + 1], points[j], points[j + 1])) return true;
        return false;
    }

    private static bool AdjacentSegmentsOverlap(Point2D a, Point2D b, Point2D c)
    {
        var abLength = a.DistanceTo(b);
        var bcLength = b.DistanceTo(c);
        if (abLength <= LengthEpsilon || bcLength <= LengthEpsilon) return false;
        var abX = b.X - a.X; var abY = b.Y - a.Y;
        var bcX = c.X - b.X; var bcY = c.Y - b.Y;
        var cross = abX * bcY - abY * bcX;
        var dot = abX * bcX + abY * bcY;
        var epsilon = 1e-10 * Math.Max(1, abLength * bcLength);
        return Math.Abs(cross) <= epsilon && dot < 0;
    }

    private static bool Intersects(Point2D a, Point2D b, Point2D c, Point2D d)
    {
        var epsilon = 1e-10 * Math.Max(1, a.DistanceTo(b) * c.DistanceTo(d));
        static double Cross(Point2D p, Point2D q, Point2D r) => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);
        static bool Between(Point2D p, Point2D q, Point2D r) =>
            r.X >= Math.Min(p.X, q.X) - LengthEpsilon && r.X <= Math.Max(p.X, q.X) + LengthEpsilon &&
            r.Y >= Math.Min(p.Y, q.Y) - LengthEpsilon && r.Y <= Math.Max(p.Y, q.Y) + LengthEpsilon;
        var abC = Cross(a,b,c); var abD = Cross(a,b,d); var cdA = Cross(c,d,a); var cdB = Cross(c,d,b);
        if (((abC > epsilon && abD < -epsilon) || (abC < -epsilon && abD > epsilon)) &&
            ((cdA > epsilon && cdB < -epsilon) || (cdA < -epsilon && cdB > epsilon))) return true;
        return (Math.Abs(abC) <= epsilon && Between(a,b,c)) || (Math.Abs(abD) <= epsilon && Between(a,b,d)) ||
               (Math.Abs(cdA) <= epsilon && Between(c,d,a)) || (Math.Abs(cdB) <= epsilon && Between(c,d,b));
    }
}
