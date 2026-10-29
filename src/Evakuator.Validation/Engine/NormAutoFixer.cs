using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Evakuator.Domain.Entities;
using Evakuator.Domain.Enums;
using Evakuator.Domain.Serialization;
using Evakuator.Domain.ValueObjects;
using Evakuator.Geometry;
using Evakuator.Norms.Models;
using Evakuator.Validation.Abstractions;
using Evakuator.Validation.Rules;

namespace Evakuator.Validation.Engine;

/// <summary>Produces a new plan snapshot and an auditable diff; never mutates its input.</summary>
public sealed class NormAutoFixer
{
    private readonly NormController controller;
    private readonly SignGeometryGenerator geometry = new();
    public NormAutoFixer(NormController? controller = null) => this.controller = controller ?? new NormController();

    public AutoFixResult HealAll(EvacuationPlan plan, NormDatabaseDto database)
    {
        var before = controller.Validate(plan, database);
        var changes = ImmutableArray.CreateBuilder<FixDiffEntry>();
        var manual = ImmutableArray.CreateBuilder<string>();
        var current = NormalizeSizes(plan, database, changes, manual);
        current = ResolveCollisions(current, plan, database, changes, manual);
        current = FixPaths(current, database, changes, manual);
        current = EnsureMarker(current, changes);
        current = FixHeader(current, database, changes);
        current = SyncLegend(current, database, changes);
        // A semantically unchanged plan must remain the same snapshot (no spurious CAD undo/revision).
        if (changes.Count == 0) current = plan;
        var after = controller.Validate(current, database);
        foreach (var issue in after.Issues.Where(i => i.Severity == IssueSeverity.Error || i.RequiresHumanReview))
            manual.Add($"{issue.RuleId} ({issue.ObjectId}): {issue.Message}");
        foreach (var change in changes.Where(c => c.RequiresHumanReview))
            manual.Add($"{change.RuleId} ({change.ObjectId}, {change.Field}): {change.Reason}");
        return new AutoFixResult(current, before, after, changes.ToImmutable(), manual.Distinct(StringComparer.Ordinal).ToImmutableArray());
    }

    private static EvacuationPlan NormalizeSizes(EvacuationPlan plan, NormDatabaseDto db,
        ImmutableArray<FixDiffEntry>.Builder changes, ImmutableArray<string>.Builder manual)
    {
        var definitions = plan.Signs.Select(s => db.FindSign(s.Code)).Where(d => d is not null).ToArray();
        if (definitions.Length == 0) return plan;
        var knownSigns = plan.Signs.Where(s => db.FindSign(s.Code) is not null).ToArray();
        var allInRange = knownSigns.All(s => s.HeightMmOnPaper >= db.FindSign(s.Code)!.MinHeightMm &&
            s.HeightMmOnPaper <= db.FindSign(s.Code)!.MaxHeightMm);
        var uniform = knownSigns.Max(s => s.HeightMmOnPaper) - knownSigns.Min(s => s.HeightMmOnPaper) <= db.Parameters.UniformToleranceMm;
        if (allInRange && uniform) return plan;
        var minimum = definitions.Max(d => d!.MinHeightMm);
        var maximum = definitions.Min(d => d!.MaxHeightMm);
        var hasCommonSize = minimum <= maximum;
        if (!hasCommonSize) manual.Add("R02: диапазоны размеров знаков не пересекаются; общий модуль выбрать автоматически невозможно.");
        var target = hasCommonSize ? Math.Clamp(db.Parameters.DefaultHeightMm, minimum, maximum) : 0;
        var result = plan.Signs.ToBuilder();
        for (var i = 0; i < result.Count; i++)
        {
            var sign = result[i];
            var def = db.FindSign(sign.Code);
            if (def is null) continue;
            var desired = hasCommonSize ? target : Math.Clamp(sign.HeightMmOnPaper, def.MinHeightMm, def.MaxHeightMm);
            if (!double.IsFinite(desired * sign.DrawingScale)) { manual.Add($"R01 ({sign.Id}): переполнение размера в модели."); continue; }
            if (Math.Abs(sign.HeightMmOnPaper - desired) > 1e-9)
            {
                changes.Add(new("R01/R02", sign.Id, "heightMmOnPaper", F(sign.HeightMmOnPaper), F(desired), false,
                    "Размер приведён к общему настраиваемому модулю; пороги не верифицированы по ГОСТ."));
                sign = sign.WithPaperHeight(desired);
            }
            if (hasCommonSize && !sign.IsSyncedWithGroupScale)
            {
                changes.Add(new("R02", sign.Id, "isSyncedWithGroupScale", "false", "true", false, "Включена синхронизация модуля."));
                sign = sign with { IsSyncedWithGroupScale = true };
            }
            result[i] = sign;
        }
        return plan with { Signs = result.ToImmutable() };
    }

    private EvacuationPlan ResolveCollisions(EvacuationPlan plan, EvacuationPlan originalPlan, NormDatabaseDto db,
        ImmutableArray<FixDiffEntry>.Builder changes, ImmutableArray<string>.Builder manual)
    {
        var originals = originalPlan.Signs.ToDictionary(s => s.Id, StringComparer.Ordinal);
        bool OriginallyIdentical(EvacuationSign a, EvacuationSign b)
        {
            var x = originals[a.Id]; var y = originals[b.Id];
            return x.Code == y.Code && x.InsertionPoint.DistanceTo(y.InsertionPoint) <= db.Parameters.DuplicateToleranceMm &&
                Math.Abs(x.ModelHeight - y.ModelHeight) < 1e-8 && Math.Abs(x.DrawingScale - y.DrawingScale) < 1e-8 &&
                Math.Abs(x.RotationDegrees - y.RotationDegrees) < 1e-8 && x.Name == y.Name && x.Category == y.Category &&
                x.IsSyncedWithGroupScale == y.IsSyncedWithGroupScale && x.NeedsReview == y.NeedsReview && x.ReviewNote == y.ReviewNote;
        }
        var placed = new List<EvacuationSign>();
        var plane = new BoundingBox2D(0, 0, plan.Width, plan.Height);
        foreach (var original in plan.Signs)
        {
            var sign = original;
            var duplicate = placed.FirstOrDefault(other => other.Code == sign.Code &&
                other.InsertionPoint.DistanceTo(sign.InsertionPoint) <= db.Parameters.DuplicateToleranceMm &&
                Math.Abs(other.ModelHeight - sign.ModelHeight) < 1e-8 &&
                Math.Abs(other.DrawingScale - sign.DrawingScale) < 1e-8 &&
                Math.Abs(other.RotationDegrees - sign.RotationDegrees) < 1e-8 &&
                other.Name == sign.Name && other.Category == sign.Category &&
                other.IsSyncedWithGroupScale == sign.IsSyncedWithGroupScale &&
                other.NeedsReview == sign.NeedsReview && other.ReviewNote == sign.ReviewNote && OriginallyIdentical(other, sign));
            if (duplicate is not null)
            {
                changes.Add(new("R06", sign.Id, "sign", Json(sign), "(удалён)", true,
                    $"Совпадает по коду, положению, размеру, масштабу и повороту с {duplicate.Id}; проверьте удаление."));
                continue;
            }
            if (sign.ModelHeight <= 0 || db.FindSign(sign.Code) is null) { placed.Add(sign); continue; }
            bool Fits(EvacuationSign candidate)
            {
                var box = geometry.Bounds(candidate, db);
                return plane.Contains(box) && !placed.Any(other => other.ModelHeight > 0 && db.FindSign(other.Code) is not null &&
                    ((other.Code == candidate.Code && other.InsertionPoint.DistanceTo(candidate.InsertionPoint) <= db.Parameters.DuplicateToleranceMm) ||
                    box.OverlapOfSmaller(geometry.Bounds(other, db)) > db.Parameters.OverlapThreshold));
            }
            if (!Fits(sign))
            {
                // Search by increasing squared distance, including both axes and diagonals.
                EvacuationSign? best = null;
                var bestDistanceSquared = int.MaxValue;
                for (var radius = 1; radius <= db.Parameters.MaxOffsetSteps && radius * radius <= bestDistanceSquared; radius++)
                    for (var dx = -radius; dx <= radius; dx++)
                        for (var dy = -radius; dy <= radius; dy++)
                        {
                            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                            var distanceSquared = dx * dx + dy * dy;
                            if (distanceSquared >= bestDistanceSquared) continue;
                            var candidate = sign with { InsertionPoint = sign.InsertionPoint.Translate(dx * db.Parameters.OffsetStepMm, dy * db.Parameters.OffsetStepMm) };
                            if (Fits(candidate)) { best = candidate; bestDistanceSquared = distanceSquared; }
                        }
                if (best is null) manual.Add($"R06 ({sign.Id}): в пределах сетки не найдено допустимое положение.");
                else
                {
                    var review = "Положение сдвинуто автоматически; проверьте стены, двери, видимость и фактическое место установки.";
                    changes.Add(new("R06", sign.Id, "insertionPoint", Json(sign.InsertionPoint), Json(best.InsertionPoint), true, review));
                    sign = best with { NeedsReview = true, ReviewNote = review };
                }
            }
            placed.Add(sign);
        }
        return plan with { Signs = placed.ToImmutableArray() };
    }

    private static EvacuationPlan FixPaths(EvacuationPlan plan, NormDatabaseDto db,
        ImmutableArray<FixDiffEntry>.Builder changes, ImmutableArray<string>.Builder manual)
    {
        var result = plan.Paths.ToBuilder();
        for (var i = 0; i < result.Count; i++)
        {
            var path = result[i];
            var cleaned = PolylineMath.RemoveConsecutiveDuplicates(path.Points);
            if (!cleaned.SequenceEqual(path.Points) && cleaned.Length >= 2)
            {
                changes.Add(new("R03", path.Id, "points", Json(path.Points), Json(cleaned), true, "Удалены только последовательные совпадающие вершины."));
                path = path with { Points = cleaned, NeedsReview = true, ReviewNote = "Проверьте геометрию пути после удаления совпадающих вершин." };
            }
            else if (cleaned.Length < 2)
                manual.Add($"R03 ({path.Id}): удаление дубликатов оставит менее двух вершин; исходные точки сохранены.");
            if (!path.HasDirectionArrow)
            {
                const string reviewDirection = "Направление стрелок принято по порядку вершин; проектировщик обязан проверить направление к выходу.";
                changes.Add(new("R03", path.Id, "hasDirectionArrow", "false", "true", true, reviewDirection));
                path = path with { HasDirectionArrow = true, NeedsReview = true, ReviewNote = reviewDirection };
            }
            var style = path.Type == EvacuationPathType.Primary ? PathLineStyle.Solid : PathLineStyle.Dashed;
            if (path.LineStyle != style)
            {
                changes.Add(new("R03", path.Id, "lineStyle", path.LineStyle.ToString(), style.ToString(), false, "Тип линии приведён к настройке категории."));
                path = path with { LineStyle = style };
            }
            if (!string.Equals(path.Color, db.Parameters.PathColor, StringComparison.OrdinalIgnoreCase))
            {
                changes.Add(new("R03", path.Id, "color", path.Color, db.Parameters.PathColor, false, "Установлен цвет пути из конфигурации."));
                path = path with { Color = db.Parameters.PathColor };
            }
            if (Math.Abs(path.LineWeightMmOnPaper - db.Parameters.PathLineWeightMm) > 1e-6)
            {
                changes.Add(new("R03", path.Id, "lineWeightMmOnPaper", F(path.LineWeightMmOnPaper), F(db.Parameters.PathLineWeightMm), false, "Установлена толщина из конфигурации."));
                path = path with { LineWeightMmOnPaper = db.Parameters.PathLineWeightMm };
            }
            if (path.Points.Length >= 2)
            {
                var exits = plan.Signs.Where(s => db.FindSign(s.Code)?.IsExit == true)
                    .OrderBy(s => s.InsertionPoint.DistanceTo(path.Points[^1])).ToArray();
                if (exits.Length > 0)
                {
                    var exit = exits[0];
                    var distance = exit.InsertionPoint.DistanceTo(path.Points[^1]);
                    if (distance > db.Parameters.SnapToleranceMm && distance <= db.Parameters.MaxAutoSnapDistanceMm)
                    {
                        var snapped = PolylineMath.RemoveConsecutiveDuplicates(path.Points.SetItem(path.Points.Length - 1, exit.InsertionPoint));
                        if (snapped.Length >= 2 && snapped.All(p => p.X >= 0 && p.Y >= 0 && p.X <= plan.Width && p.Y <= plan.Height) &&
                            !PolylineMath.HasDegenerateSegments(snapped) && !PolylineMath.HasSelfIntersection(snapped))
                        {
                            var review = "Конец пути приближен к знаку выхода; проверьте фактическую дверь и отсутствие стен/препятствий.";
                            changes.Add(new("R03", path.Id, "points", Json(path.Points), Json(snapped), true, review));
                            path = path with { Points = snapped, NeedsReview = true, ReviewNote = review };
                        }
                        else manual.Add($"R03 ({path.Id}): привязка создаёт вырождение или самопересечение; маршрут не изменён.");
                    }
                    else if (distance > db.Parameters.MaxAutoSnapDistanceMm)
                        manual.Add($"R03 ({path.Id}): ближайший знак выхода слишком далеко для автоматической привязки.");
                }
                else manual.Add($"R03 ({path.Id}): нет известного знака выхода; маршрут нельзя достроить автоматически.");
            }
            if (path.Points.Length < 2 || PolylineMath.HasSelfIntersection(path.Points))
                manual.Add($"R03 ({path.Id}): самопересечение или недостаток вершин требуют ручного проектирования пути.");
            result[i] = path;
        }
        return plan with { Paths = result.ToImmutable() };
    }

    private static EvacuationPlan EnsureMarker(EvacuationPlan plan, ImmutableArray<FixDiffEntry>.Builder changes)
    {
        if (!plan.Markers.IsEmpty) return plan;
        var starts = plan.Paths.Where(p => !p.Points.IsEmpty).Select(p => p.Points[0]).ToArray();
        var position = starts.Length > 0 ? new Point2D(starts.Average(p => p.X), starts.Average(p => p.Y)) : new Point2D(plan.Width / 2, plan.Height / 2);
        var ids = plan.Signs.Select(s => s.Id).Concat(plan.Paths.Select(p => p.Id)).ToHashSet(StringComparer.Ordinal);
        var id = "here-auto";
        for (var n = 2; ids.Contains(id); n++) id = $"here-auto-{n}";
        var marker = new YouAreHereMarker { Id = id, Position = position, NeedsReview = true, Note = "Предположительное место: проектировщик обязан проверить точное положение точки." };
        changes.Add(new("R04", marker.Id, "marker", "(отсутствует)", Json(marker), true, marker.Note));
        return plan with { Markers = [marker] };
    }

    private static EvacuationPlan FixHeader(EvacuationPlan plan, NormDatabaseDto db, ImmutableArray<FixDiffEntry>.Builder changes)
    {
        var header = plan.Header ?? new PlanHeader();
        static string Append(string? previous, string addition) => string.IsNullOrWhiteSpace(previous) ? addition : previous + "\n" + addition;
        if (!R07_MandatoryTextAndHeaderRule.HasApproval(header)) header = header with { Approval = Append(header.Approval, db.FireInstructionTemplate.Approval) };
        if (!R07_MandatoryTextAndHeaderRule.HasTitle(header)) header = header with { Title = Append(header.Title, db.FireInstructionTemplate.Title) };
        if (header != plan.Header)
            changes.Add(new("R07", plan.Id, "header", Json(plan.Header), Json(header), true, "Шаблон шапки; утверждение руководителем не выполняется автоматически."));
        var text = plan.FireInstructions;
        if (!R07_MandatoryTextAndHeaderRule.HasInstructions(text))
        {
            var updated = Append(text, db.FireInstructionTemplate.Instructions);
            changes.Add(new("R07", plan.Id, "fireInstructions", text, updated, true, "Добавлен шаблон; проверьте его для конкретного объекта."));
            text = updated;
        }
        return plan with { Header = header, FireInstructions = text };
    }

    private static EvacuationPlan SyncLegend(EvacuationPlan plan, NormDatabaseDto db, ImmutableArray<FixDiffEntry>.Builder changes)
    {
        var expected = new LegendLayoutEngine().Build(plan, db);
        if (plan.Legend is not null && plan.Legend.Entries.SequenceEqual(expected.Entries)) return plan;
        changes.Add(new("R05", plan.Id, "legend", Json(plan.Legend), Json(expected), false, "Легенда перестроена из фактического состава плана."));
        return plan with { Legend = expected };
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, PlanJson.Options);
    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
