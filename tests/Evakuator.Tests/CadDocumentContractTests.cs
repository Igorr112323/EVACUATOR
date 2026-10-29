using System.Text.Json;
using Evakuator.CadAdapter.Contracts;
using Evakuator.CadAdapter.Simulation;
using Evakuator.Domain.Entities;
using Evakuator.Domain.Serialization;

namespace Evakuator.Tests;

/// <summary>
/// Portable behavioural contract. A future host harness can derive from this class and
/// create a fresh, isolated document with no prior undo entries for each test.
/// These tests do not exercise a nanoCAD host merely by running the simulator subclass.
/// </summary>
public abstract class CadDocumentContractTests : IDisposable
{
    private readonly List<ICadDocumentContext> documents = [];

    protected abstract ICadDocumentContext CreateFreshDocument(EvacuationPlan initial);

    protected virtual void CloseDocument(ICadDocumentContext document)
    {
        if (document is IDisposable disposable) disposable.Dispose();
    }

    private ICadDocumentContext Create(EvacuationPlan initial)
    {
        var document = CreateFreshDocument(initial);
        documents.Add(document);
        return document;
    }

    private static string Content(EvacuationPlan plan) => JsonSerializer.Serialize(plan, PlanJson.Options);

    [Fact]
    public void FreshDocumentReadsBackTheEntireInitialSnapshot()
    {
        var initial = TestData.CleanPlan();
        var context = Create(initial);
        Assert.Equal(Content(initial), Content(context.Read().Plan));
        Assert.False(context.Undo());
    }

    [Fact]
    public void StaleWriteCannotOverwriteANewerCommittedSnapshot()
    {
        var context = Create(TestData.CleanPlan());
        var before = context.Read();
        var oldContent = Content(before.Plan);
        Assert.True(context.TryCommit(before.Revision, before.Plan with { Name = "committed" }, "edit"));
        var current = context.Read();
        Assert.Equal(oldContent, Content(before.Plan));
        Assert.NotEqual(before.Revision, current.Revision);
        Assert.False(context.TryCommit(before.Revision, before.Plan with { Name = "stale" }, "stale edit"));
        Assert.Equal(current.Revision, context.Read().Revision);
        Assert.Equal(Content(current.Plan), Content(context.Read().Plan));
    }

    [Fact]
    public void InvalidUpdateAndChangedIdentityLeaveTheDocumentUnchanged()
    {
        var context = Create(TestData.CleanPlan());
        var before = context.Read();
        var notifications = new List<CadSnapshot>();
        context.Changed += notifications.Add;
        var invalid = before.Plan with { Signs = before.Plan.Signs.Add(before.Plan.Signs[0]) };
        Assert.Throws<InvalidDataException>(() => context.TryCommit(before.Revision, invalid, "duplicate"));
        Assert.Throws<InvalidOperationException>(() => context.TryCommit(before.Revision, before.Plan with { Id = "different" }, "replace"));
        Assert.Throws<ArgumentException>(() => context.TryCommit(before.Revision, before.Plan, " "));
        Assert.Equal(before.Revision, context.Read().Revision);
        Assert.Equal(Content(before.Plan), Content(context.Read().Plan));
        Assert.Empty(notifications);
        Assert.False(context.Undo());
    }

    [Fact]
    public void NoOpDoesNotChangeRevisionNotifyOrCreateUndoHistory()
    {
        var context = Create(TestData.CleanPlan());
        var before = context.Read();
        var notifications = new List<CadSnapshot>();
        context.Changed += notifications.Add;
        Assert.True(context.TryCommit(before.Revision, before.Plan, "no-op"));
        Assert.Equal(before.Revision, context.Read().Revision);
        Assert.Empty(notifications);
        Assert.False(context.Undo());
    }

    [Fact]
    public void UndoRestoresContentButDoesNotReuseOldRevisionTokens()
    {
        var context = Create(TestData.CleanPlan());
        var before = context.Read();
        Assert.True(context.TryCommit(before.Revision, before.Plan with { Name = "updated" }, "edit"));
        var after = context.Read();
        Assert.True(context.Undo());
        var undone = context.Read();
        Assert.Equal(Content(before.Plan), Content(undone.Plan));
        Assert.NotEqual(before.Revision, undone.Revision);
        Assert.NotEqual(after.Revision, undone.Revision);
        Assert.False(context.TryCommit(before.Revision, before.Plan with { Name = "obsolete" }, "stale"));
        Assert.False(context.Undo());
    }

    [Fact]
    public void ObserversSeeTheCommittedSnapshotAndCannotPreventOtherObservers()
    {
        var context = Create(TestData.CleanPlan());
        var before = context.Read();
        var observed = new List<CadSnapshot>();
        context.Changed += _ => throw new InvalidOperationException("observer failure");
        context.Changed += observed.Add;
        Assert.True(context.TryCommit(before.Revision, before.Plan with { Name = "updated" }, "edit"));
        var notice = Assert.Single(observed);
        Assert.Equal(context.Read().Revision, notice.Revision);
        Assert.Equal(Content(context.Read().Plan), Content(notice.Plan));
    }

    public void Dispose()
    {
        foreach (var document in documents) CloseDocument(document);
    }
}

public sealed class InMemoryCadDocumentContractTests : CadDocumentContractTests
{
    protected override ICadDocumentContext CreateFreshDocument(EvacuationPlan initial) => new InMemoryCadDocumentContext(initial);
}
