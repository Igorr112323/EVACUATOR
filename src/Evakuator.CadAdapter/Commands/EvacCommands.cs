using System.Collections.Immutable;
using Evakuator.CadAdapter.Contracts;
using Evakuator.CadAdapter.CustomObjects;
using Evakuator.Domain.Entities;
using Evakuator.Domain.Enums;
using Evakuator.Domain.ValueObjects;
using Evakuator.Geometry;
using Evakuator.Norms.Models;
using Evakuator.Validation.Abstractions;
using Evakuator.Validation.Engine;

namespace Evakuator.CadAdapter.Commands;

/// <summary>Executable command semantics for the simulator; no nanoCAD registration is implied.</summary>
public sealed class EvacCommands
{
    private readonly ICadDocumentContext context;
    private readonly Func<NormDatabaseDto> database;
    private readonly NormController controller = new();
    private readonly NormAutoFixer fixer = new();
    private readonly LegendLayoutEngine legends = new();
    public EvacCommands(ICadDocumentContext context, Func<NormDatabaseDto> database)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public IReadOnlyList<SignDefinitionDto> Palette() => database().Signs;

    public string InsertSign(string code, Point2D position, double heightMm, double drawingScale, double rotationDegrees = 0, bool needsReview = false)
    {
        var db = database();
        var def = db.FindSign(code) ?? throw new ArgumentException("Unknown sign code.", nameof(code));
        _ = new MillimeterSize(heightMm);
        var scale = new PaperScale(drawingScale);
        if (!double.IsFinite(rotationDegrees) || !double.IsFinite(scale.ToModel(heightMm))) throw new ArgumentOutOfRangeException(nameof(rotationDegrees));
        var id = Guid.NewGuid().ToString("N");
        var sign = new EvacuationSign { Id = id, Code = def.Code, Name = def.Name, Category = def.Category.ToString(),
            InsertionPoint = position, ModelHeight = scale.ToModel(heightMm), DrawingScale = drawingScale, RotationDegrees = rotationDegrees,
            NeedsReview = needsReview, ReviewNote = needsReview ? "Знак вставлен в центр для предварительного размещения; проверьте фактическое положение." : string.Empty };
        Apply(plan => plan with { Signs = plan.Signs.Add(sign) }, db, "Insert safety sign");
        return id;
    }

    public void EditSign(string id, Func<EvacSignCustomObject, EvacSignCustomObject> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        var db = database();
        Apply(plan =>
        {
            var index = FindIndex(plan.Signs, id, s => s.Id);
            var replacement = edit(new EvacSignCustomObject(plan.Signs[index])).Model;
            if (replacement.Id != id || db.FindSign(replacement.Code) is null) throw new InvalidOperationException("Invalid sign edit.");
            return plan with { Signs = plan.Signs.SetItem(index, replacement) };
        }, db, "Edit safety sign");
    }

    public void DeleteSign(string id)
    {
        var db = database();
        Apply(plan => plan with { Signs = plan.Signs.RemoveAt(FindIndex(plan.Signs, id, s => s.Id)) }, db, "Delete safety sign");
    }

    public string InsertPath(IEnumerable<Point2D> points, EvacuationPathType type, double drawingScale = 100)
    {
        var db = database();
        var vertices = points.ToImmutableArray();
        if (vertices.Length < 2) throw new ArgumentException("A path requires at least two vertices.", nameof(points));
        _ = new PaperScale(drawingScale);
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        var id = Guid.NewGuid().ToString("N");
        var path = new EvacuationPath { Id = id, Points = vertices, Type = type, DrawingScale = drawingScale, HasDirectionArrow = true,
            LineStyle = type == EvacuationPathType.Primary ? PathLineStyle.Solid : PathLineStyle.Dashed,
            Color = db.Parameters.PathColor, LineWeightMmOnPaper = db.Parameters.PathLineWeightMm };
        Apply(plan => plan with { Paths = plan.Paths.Add(path) }, db, "Insert evacuation path");
        return id;
    }

    public void EditPath(string id, Func<EvacPathCustomObject, EvacPathCustomObject> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        var db = database();
        Apply(plan =>
        {
            var index = FindIndex(plan.Paths, id, p => p.Id);
            var replacement = edit(new EvacPathCustomObject(plan.Paths[index])).Model;
            if (replacement.Id != id) throw new InvalidOperationException("Invalid path edit.");
            return plan with { Paths = plan.Paths.SetItem(index, replacement) };
        }, db, "Edit evacuation path");
    }

    public string InsertHere(Point2D position)
    {
        var db = database();
        var id = Guid.NewGuid().ToString("N");
        Apply(plan => plan with { Markers = plan.Markers.Add(new YouAreHereMarker { Id = id, Position = position }) }, db, "Insert location marker");
        return id;
    }

    public void RebuildLegend()
    {
        var db = database();
        Apply(plan => plan, db, "Rebuild legend");
    }

    public ValidationReport Check() => controller.Validate(context.Read().Plan, database());

    public AutoFixResult FixAll()
    {
        var db = database(); // single validated configuration snapshot for the entire transaction
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var snapshot = context.Read();
            var result = fixer.HealAll(snapshot.Plan, db);
            if (context.TryCommit(snapshot.Revision, result.Plan, "One-click fix")) return result;
        }
        throw new InvalidOperationException("Document changed concurrently; retry the operation.");
    }

    public string ExportSvg(ISet<string>? highlights = null) => new SvgPreviewExporter().ExportPlan(context.Read().Plan, database(), highlights);

    private void Apply(Func<EvacuationPlan, EvacuationPlan> update, NormDatabaseDto db, string description)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var snapshot = context.Read();
            var next = update(snapshot.Plan);
            var legend = legends.Build(next, db);
            if (next.Legend is null || !next.Legend.Entries.SequenceEqual(legend.Entries)) next = next with { Legend = legend };
            if (context.TryCommit(snapshot.Revision, next, description)) return;
        }
        throw new InvalidOperationException("Document changed concurrently; retry the operation.");
    }

    private static int FindIndex<T>(ImmutableArray<T> items, string id, Func<T, string> getId)
    {
        for (var i = 0; i < items.Length; i++) if (getId(items[i]) == id) return i;
        throw new KeyNotFoundException($"Object {id} was not found.");
    }
}
