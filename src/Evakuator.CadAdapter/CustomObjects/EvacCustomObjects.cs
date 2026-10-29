using System.Collections.Immutable;
using Evakuator.Domain.Entities;
using Evakuator.Domain.ValueObjects;
using Evakuator.Norms.Models;

namespace Evakuator.CadAdapter.CustomObjects;

// These are functional host-independent editing models, NOT McCustomBase objects or proof of nanoCAD integration.
public sealed record EvacSignCustomObject(EvacuationSign Model)
{
    public EvacSignCustomObject Move(Point2D position) => this with { Model = Model with { InsertionPoint = position } };
    public EvacSignCustomObject Rotate(double degrees)
    {
        if (!double.IsFinite(degrees)) throw new ArgumentOutOfRangeException(nameof(degrees));
        return this with { Model = Model with { RotationDegrees = degrees } };
    }
    public EvacSignCustomObject Resize(double paperHeightMm)
    {
        _ = new MillimeterSize(paperHeightMm);
        return this with { Model = Model.WithPaperHeight(paperHeightMm) };
    }
    public EvacSignCustomObject ChangeType(SignDefinitionDto definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return this with { Model = Model with { Code = definition.Code, Name = definition.Name, Category = definition.Category.ToString() } };
    }
}

public sealed record EvacPathCustomObject(EvacuationPath Model)
{
    public EvacPathCustomObject MoveVertex(int index, Point2D position) => this with { Model = Model with { Points = Model.Points.SetItem(index, position) } };
    public EvacPathCustomObject ReverseDirection() => this with { Model = Model with { Points = Model.Points.Reverse().ToImmutableArray() } };
}

public sealed record EvacLegendCustomObject(LegendTable Model)
{
    public int Count => Model.Entries.Length;
}
