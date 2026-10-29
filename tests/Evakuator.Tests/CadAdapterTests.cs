using System.Collections.Concurrent;
using Evakuator.CadAdapter.Commands;
using Evakuator.CadAdapter.Simulation;
using Evakuator.Domain.Entities;
using Evakuator.Domain.Enums;
using Evakuator.Domain.ValueObjects;
using Evakuator.Validation.Abstractions;

namespace Evakuator.Tests;

public class CadAdapterTests
{
    [Fact]
    public void StaleCommitCannotOverwriteANewerSnapshot()
    {
        var plan = TestData.CleanPlan();
        var context = new InMemoryCadDocumentContext(plan);
        var initial = context.Read();
        Assert.True(context.TryCommit(initial.Revision, plan with { Name = "first" }, "first"));
        Assert.False(context.TryCommit(initial.Revision, plan with { Name = "stale" }, "stale"));
        Assert.Equal("first", context.Read().Plan.Name);
        Assert.Equal(1, context.Read().Revision);
    }

    [Fact]
    public void InvalidDocumentAndChangedIdentityCannotCommit()
    {
        var plan = TestData.CleanPlan();
        var context = new InMemoryCadDocumentContext(plan);
        Assert.Throws<InvalidOperationException>(() => context.TryCommit(0, plan with { Id = "other" }, "replace"));
        var invalid = plan with { Signs = plan.Signs.Add(plan.Signs[0]) };
        Assert.Throws<InvalidDataException>(() => context.TryCommit(0, invalid, "duplicate"));
        Assert.Equal(0, context.Read().Revision);
        Assert.Same(plan, context.Read().Plan);
    }

    [Fact]
    public void NoOpCommitAndCleanHealDoNotCreateUndoEntries()
    {
        var plan = TestData.CleanPlan();
        var context = new InMemoryCadDocumentContext(plan);
        Assert.True(context.TryCommit(0, plan, "no-op"));
        var result = new EvacCommands(context, TestData.Database).FixAll();
        Assert.Empty(result.Changes);
        Assert.Same(plan, result.Plan);
        Assert.Equal(0, context.Read().Revision);
        Assert.False(context.Undo());
    }

    [Fact]
    public void UndoRestoresTheFullPlanAndIncreasesTheRevision()
    {
        var plan = TestData.CleanPlan();
        var context = new InMemoryCadDocumentContext(plan);
        var commands = new EvacCommands(context, TestData.Database);
        commands.InsertSign("F10", new Point2D(10000, 8000), 10, 100);
        Assert.Contains(context.Read().Plan.Legend!.Entries, e => e.Key == "F10");
        Assert.True(context.Undo());
        Assert.Same(plan, context.Read().Plan);
        Assert.Equal(2, context.Read().Revision);
        Assert.False(context.Undo());
    }

    [Fact]
    public void HistoryIsBoundedToOneHundredTransactions()
    {
        var context = new InMemoryCadDocumentContext(TestData.CleanPlan() with { Name = "v0" });
        for (var i = 1; i <= 105; i++)
        {
            var snapshot = context.Read();
            Assert.True(context.TryCommit(snapshot.Revision, snapshot.Plan with { Name = $"v{i}" }, "edit"));
        }
        for (var i = 0; i < 100; i++) Assert.True(context.Undo());
        Assert.False(context.Undo());
        Assert.Equal("v5", context.Read().Plan.Name);
        Assert.Equal(205, context.Read().Revision);
    }

    [Fact]
    public void ThrowingObserverCannotRollBackOrHideTheCommit()
    {
        var context = new InMemoryCadDocumentContext(TestData.CleanPlan());
        var notifications = 0;
        context.Changed += _ => throw new InvalidOperationException("observer failure");
        context.Changed += _ => notifications++;
        Assert.True(context.TryCommit(0, context.Read().Plan with { Name = "committed" }, "edit"));
        Assert.Equal("committed", context.Read().Plan.Name);
        Assert.Single(context.ObserverErrors);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void ConcurrentOptimisticTransactionsDoNotLoseUpdates()
    {
        var context = new InMemoryCadDocumentContext(TestData.CleanPlan());
        var failures = new ConcurrentQueue<Exception>();
        Parallel.For(0, 40, i =>
        {
            try
            {
                while (true)
                {
                    var snapshot = context.Read();
                    var next = snapshot.Plan with { Markers = snapshot.Plan.Markers.Add(new YouAreHereMarker { Id = "concurrent-" + i, Position = new Point2D(i, i) }) };
                    if (context.TryCommit(snapshot.Revision, next, "parallel")) break;
                }
            }
            catch (Exception e) { failures.Enqueue(e); }
        });
        Assert.Empty(failures);
        Assert.Equal(40, context.Read().Revision);
        Assert.Equal(41, context.Read().Plan.Markers.Length);
        Assert.Equal(41, context.Read().Plan.Markers.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void SignInsertionEditingTypeAndDeletionKeepLegendInSync()
    {
        var db = TestData.Database();
        var context = new InMemoryCadDocumentContext(TestData.CleanPlan());
        var commands = new EvacCommands(context, () => db);
        var id = commands.InsertSign("F10", new Point2D(9000, 8000), 12, 100, 30);
        var sign = context.Read().Plan.Signs.Single(s => s.Id == id);
        Assert.Equal(12, sign.HeightMmOnPaper);
        Assert.Contains(context.Read().Plan.Legend!.Entries, e => e.Key == "F10");
        commands.EditSign(id, s => s.Move(new Point2D(7000, 7000)).Rotate(90).Resize(10).ChangeType(db.FindSign("W09")!));
        sign = context.Read().Plan.Signs.Single(s => s.Id == id);
        Assert.Equal("W09", sign.Code);
        Assert.Equal(new Point2D(7000, 7000), sign.InsertionPoint);
        Assert.Equal(90, sign.RotationDegrees);
        Assert.Equal(10, sign.HeightMmOnPaper);
        Assert.Contains(context.Read().Plan.Legend!.Entries, e => e.Key == "W09");
        Assert.DoesNotContain(context.Read().Plan.Legend!.Entries, e => e.Key == "F10");
        commands.DeleteSign(id);
        Assert.DoesNotContain(context.Read().Plan.Signs, s => s.Id == id);
        Assert.DoesNotContain(context.Read().Plan.Legend!.Entries, e => e.Key == "W09");
    }

    [Fact]
    public void InvalidOperationsLeaveTheDocumentUnchanged()
    {
        var db = TestData.Database();
        var context = new InMemoryCadDocumentContext(TestData.CleanPlan());
        var commands = new EvacCommands(context, () => db);
        Assert.Throws<ArgumentException>(() => commands.InsertSign("E98", new Point2D(0, 0), 10, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => commands.InsertSign("F04", new Point2D(0, 0), 0, 100));
        Assert.Throws<KeyNotFoundException>(() => commands.DeleteSign("missing"));
        Assert.Throws<InvalidOperationException>(() => commands.EditSign("fire-1", s => s with { Model = s.Model with { Id = "changed" } }));
        Assert.Equal(0, context.Read().Revision);
    }

    [Fact]
    public void InsertAndEditSecondaryPathUpdateItsLegendAndGeometry()
    {
        var context = new InMemoryCadDocumentContext(TestData.CleanPlan());
        var commands = new EvacCommands(context, TestData.Database);
        var id = commands.InsertPath([new(2000, 1000), new(3000, 1000)], EvacuationPathType.Secondary);
        Assert.Contains(context.Read().Plan.Legend!.Entries, e => e.Key == "PATH:Secondary");
        commands.EditPath(id, p => p.MoveVertex(0, new Point2D(2500, 2000)).ReverseDirection());
        var path = context.Read().Plan.Paths.Single(p => p.Id == id);
        Assert.Equal(PathLineStyle.Dashed, path.LineStyle);
        Assert.Equal(new Point2D(2500, 2000), path.Points[^1]);
    }

    [Fact]
    public void HealingIsOneUndoableTransactionAndLeavesReviewWarnings()
    {
        var original = TestData.CleanPlan() with { Legend = null, Markers = [] };
        var context = new InMemoryCadDocumentContext(original);
        var commands = new EvacCommands(context, TestData.Database);
        var result = commands.FixAll();
        Assert.Equal(0, result.After.ErrorCount);
        Assert.Contains(result.After.Issues, x => x.Severity == IssueSeverity.Warning && x.RequiresHumanReview);
        Assert.Equal(1, context.Read().Revision);
        Assert.True(context.Undo());
        Assert.Same(original, context.Read().Plan);
    }
}
