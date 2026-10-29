using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Evakuator.CadAdapter.Simulation;
using Evakuator.Domain.Serialization;
using Evakuator.Norms.Services;
using Evakuator.UI.ViewModels;

namespace Evakuator.UI;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var watcher = new HotReloadNormWatcher(new JsonNormRepository(AppPaths.Norms));
            var context = new InMemoryCadDocumentContext(PlanJson.Load(AppPaths.Plan));
            var vm = new MainPaletteViewModel(context, () => watcher.Current, action => Dispatcher.UIThread.Post(action), watcher);
            var window = new MainWindow { DataContext = vm };
            window.Closed += (_, _) => { vm.Dispose(); watcher.Dispose(); };
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
