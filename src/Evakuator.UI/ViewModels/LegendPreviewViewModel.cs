using System.Collections.ObjectModel;
using Evakuator.Domain.Entities;
using Evakuator.Geometry;
using Evakuator.Norms.Models;

namespace Evakuator.UI.ViewModels;

public sealed class LegendPreviewViewModel
{
    public ObservableCollection<LegendEntry> Entries { get; } = [];
    public void Refresh(EvacuationPlan plan, NormDatabaseDto db)
    {
        Entries.Clear();
        foreach (var entry in new LegendLayoutEngine().Build(plan, db).Entries) Entries.Add(entry);
    }
}
