using System.Collections.Immutable;
using Evakuator.Norms.Exceptions;
using Evakuator.Norms.Services;

namespace Evakuator.Tests;

public class NormsTests
{
    [Fact]
    public void LoadsAllThirtyUniqueSignsAndAllCategories()
    {
        var db = TestData.Database();
        Assert.Equal(30, db.Signs.Length);
        Assert.Equal(30, db.Signs.Select(s => s.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(5, db.Signs.Select(s => s.Category).Distinct().Count());
        Assert.All(db.Signs, s => Assert.NotEmpty(s.Primitives));
    }

    [Fact]
    public void AmendmentAddsNewCodeWithoutChangingBase()
    {
        var repo = new JsonNormRepository(TestData.NormPath);
        var merged = repo.Load(Path.Combine(TestData.Root, "norms", "sample_custom_amendment.json"));
        Assert.Equal(31, merged.Signs.Length);
        Assert.NotNull(merged.FindSign("E99"));
        Assert.Null(repo.Load().FindSign("E99"));
    }

    [Fact]
    public void DuplicateCodesAreRejected()
    {
        var db = TestData.Database();
        var invalid = db with { Signs = db.Signs.Add(db.Signs[0]) };
        Assert.Contains(new NormDatabaseValidator().Validate(invalid), e => e.Contains("duplicate code", StringComparison.Ordinal));
    }

    [Fact]
    public void OutsideNormalizedPointsAreRejected()
    {
        var db = TestData.Database();
        var sign = db.Signs[0];
        var badPrimitive = sign.Primitives[0] with { Type = "Polygon", Points = [new(-.1, .1), new(.2, .2), new(.4, .4)] };
        var badSign = sign with { Primitives = ImmutableArray.Create(badPrimitive) };
        var errors = new NormDatabaseValidator().Validate(db with { Signs = db.Signs.SetItem(0, badSign) });
        Assert.Contains(errors, e => e.Contains("invalid Polygon points", StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownPrimitiveCannotSilentlyDisappear()
    {
        var db = TestData.Database();
        var sign = db.Signs[0];
        var bad = sign with { Primitives = [sign.Primitives[0] with { Type = "Bezier" }] };
        Assert.Throws<NormDatabaseValidationException>(() => new NormDatabaseValidator().ValidateAndThrow(db with { Signs = db.Signs.SetItem(0, bad) }));
    }

    [Fact]
    public void InvalidHotReloadDoesNotReplaceLastGoodSnapshot()
    {
        var folder = Path.Combine(Path.GetTempPath(), "evac-norm-test-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "norms.json");
            File.Copy(TestData.NormPath, path);
            using var watcher = new HotReloadNormWatcher(new JsonNormRepository(path));
            var before = watcher.Current;
            File.WriteAllText(path, "{broken");
            Assert.False(watcher.ReloadNow());
            Assert.Same(before, watcher.Current);
            File.WriteAllText(path, File.ReadAllText(TestData.NormPath).Replace("0.1-demo", "0.2-demo", StringComparison.Ordinal));
            Assert.True(watcher.ReloadNow());
            Assert.Equal("0.2-demo", watcher.Current.Version);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task FileWatcherReloadsValidChangesWithoutRecompiling()
    {
        var folder = Path.Combine(Path.GetTempPath(), "evac-watch-test-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "norms.json");
            File.Copy(TestData.NormPath, path);
            using var watcher = new HotReloadNormWatcher(new JsonNormRepository(path));
            var observed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            watcher.Reloaded += db => observed.TrySetResult(db.Version);
            await File.WriteAllTextAsync(path, File.ReadAllText(TestData.NormPath).Replace("0.1-demo", "hot-reloaded", StringComparison.Ordinal));
            Assert.Equal("hot-reloaded", await observed.Task.WaitAsync(TimeSpan.FromSeconds(8)));
        }
        finally { Directory.Delete(folder, true); }
    }
}
