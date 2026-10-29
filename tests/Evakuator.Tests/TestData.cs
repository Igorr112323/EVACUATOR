using Evakuator.Domain.Entities;
using Evakuator.Domain.Enums;
using Evakuator.Domain.ValueObjects;
using Evakuator.Geometry;
using Evakuator.Norms.Models;
using Evakuator.Norms.Services;

namespace Evakuator.Tests;

internal static class TestData
{
    public static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Evakuator.sln")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
        }
    }
    public static string NormPath => Path.Combine(Root, "norms", "gost_12_4_026_2015.json");
    public static NormDatabaseDto Database() => new JsonNormRepository(NormPath).Load();
    public static EvacuationSign Sign(string id, string code, double x, double y, double paperHeight = 10, double scale = 100) => new()
    {
        Id = id, Code = code, InsertionPoint = new Point2D(x, y), ModelHeight = paperHeight * scale, DrawingScale = scale
    };
    public static EvacuationPlan CleanPlan()
    {
        var db = Database();
        var plan = new EvacuationPlan
        {
            Id = "plan-1", Name = "Учебный план", Width = 20000, Height = 12000,
            Signs = [Sign("exit-1", "E01-01", 15000, 6000), Sign("fire-1", "F04", 5000, 8000)],
            Paths = [new EvacuationPath { Id = "path-1", Type = EvacuationPathType.Primary, Points = [new(1000,5000), new(8000,5000), new(15000,6000)], HasDirectionArrow = true, LineStyle = PathLineStyle.Solid, Color = db.Parameters.PathColor, LineWeightMmOnPaper = db.Parameters.PathLineWeightMm }],
            Markers = [new YouAreHereMarker { Id = "here-1", Position = new Point2D(1000,5000) }],
            Header = new PlanHeader { Approval = db.FireInstructionTemplate.Approval, Title = db.FireInstructionTemplate.Title },
            FireInstructions = db.FireInstructionTemplate.Instructions
        };
        return plan with { Legend = new LegendLayoutEngine().Build(plan, db) };
    }
}
