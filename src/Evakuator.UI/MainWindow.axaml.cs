using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Evakuator.UI;

public sealed partial class MainWindow : Window
{
    public MainWindow() => AvaloniaXamlLoader.Load(this);
}
