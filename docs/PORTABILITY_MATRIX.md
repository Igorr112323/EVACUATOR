# Матрица переносимости и зависимостей

Снимок фактических прогонов и точные SHA — в [индексе доказательств](verification/EVIDENCE_INDEX.md). Здесь «Linux CI» означает GitHub-hosted Ubuntu 24.04 x64, не локальный Debian и не nanoCAD. Локальная песочница Debian не может скачать официальный SDK/NuGet. Колонки Windows и host ниже являются планом, если явно не указано иное.

| Проект | TFM | Прямые сторонние пакеты | Linux CI | Windows отдельно | nanoCAD host |
|---|---|---|---|---|---|
| `Evakuator.Domain` | net10.0 | нет | сборка и xUnit | NOT RUN | совместимость неизвестна |
| `Evakuator.Norms` | net10.0 | нет | сборка и xUnit; watcher с системными событиями | NOT RUN, события файловой системы проверить | неизвестно |
| `Evakuator.Geometry` | net10.0 | нет | сборка, векторные и геометрические тесты | NOT RUN | рендеринг host не реализован |
| `Evakuator.Validation` | net10.0 | нет | сборка, правила, regression/stress | NOT RUN | нормативная и host-проверка не выполнены |
| `Evakuator.CadAdapter` | net10.0 | нет | только in-memory Simulator и договорные тесты | NOT RUN | настоящий bridge отсутствует; `SdkGate` блокирует конфигурацию |
| `Evakuator.CliDemo` | net10.0 | нет | JSON/SVG, положительный/негативный/повторный запуск | NOT RUN | это внешняя CLI, не DWG и не плагин |
| `Evakuator.UI` | net10.0 | Avalonia.Desktop 11.3.18, Avalonia.Themes.Fluent 11.3.18 | сборка и тесты ViewModel; отдельное окно в текущем прогоне не тестировалось | NOT RUN; нужны родные зависимости и GUI | не in-host UI |
| `Evakuator.Tests` | net10.0 | Microsoft.NET.Test.Sdk 17.14.1; xunit 2.9.3; xunit.runner.visualstudio 3.1.5 | runtime xUnit/TRX | NOT RUN | host-тестов нет |
| `tools/Evakuator.SignCatalog` | net10.0 через `Directory.Build.props` | нет | отдельные restore/build/run в CI | NOT RUN | SVG, не DWG-каталог |
| `linux_stand` | Python, не .NET | см. отдельные requirements | исторический отдельный 58-test стенд | NOT RUN | не C# и не nanoCAD |

Все 8 проектов основного решения явно или через общие свойства используют `net10.0`. Дополнительный каталоговый tool — девятый проект вне solution и проверяется отдельно. `global.json` запрашивает SDK `10.0.100` с `latestFeature`: более новый установленный SDK может быть выбран автоматически. В изолированном CI установлен официальный 10.0.400; фактическую выбранную версию всегда проверяйте `dotnet --info`.

## Поставка и восстановление

- Linux CI использует `actions/setup-dotnet`, ставит SDK в отдельный каталог runner и восстанавливает из `https://api.nuget.org/v3/index.json`; `NuGetAudit` намеренно не отключён. Это результат конкретного прогона, а не гарантия отсутствия всех уязвимостей.
- Библиотеки домена/норм/геометрии/правил не имеют прямых внешних `PackageReference`, но solution целиком требует NuGet для Avalonia и xUnit. Транзитивные зависимости и их лицензии должны быть отдельно проверены перед распространением.
- `System.Text.Json`, `ImmutableArray`, XML, файловая система и `FileSystemWatcher` — библиотеки .NET; разные ОС могут по-разному доставлять события и разрешать доступ к файлам. SVG использует `InvariantCulture` и XML writer; актуальные векторные/культурные проверки перечислены в TRX.
- Avalonia имеет платформенно-зависимые runtime assets и GUI-драйверы. Сборка UI на Linux не доказывает запуск окна на Windows или в nanoCAD.
- Не предполагается совместимость `net10.0` с определённой сборкой nanoCAD. Официальные метаданные публичного reference-пакета nanoCAD 26 содержали `net60`, но пакет не был получен и **не является** host или полным MultiCAD SDK. Поиск подходящего TFM/CLR проводится по официальной документации установленного host и совместимого SDK, после чего потребуется отдельная сборка и тесты.
- x64/ARM, пути, локали, права, sandbox, несколько документов, многопоточность и производительность Windows нужно проверять отдельно. Никаких Windows, host, DWG или PDF результатов этот документ не утверждает.
