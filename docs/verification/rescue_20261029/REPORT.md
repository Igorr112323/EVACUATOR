# RESCUE-AND-VERIFY-01 — отчёт о сохранении и Linux-проверке

Дата: 29.10.2026. Результаты ниже привязаны к указанным снимкам, а не подразумеваются для каждого будущего коммита. Самоссылочный SHA финального коммита нельзя вписать в его собственное содержимое; текущий HEAD и remote проверяются командами в конце отчёта.

## Статусы и ограничения

| Статус | Значение |
|---|---|
| `SOURCE_SECURED` | Исходное рабочее дерево и `.git` сохранены в проверенном аварийном архиве; все 149 исходных файлов сохранены побайтово в отдельных Git-коммитах и ветка отправлена на GitHub. |
| `LINUX_CORE_VERIFIED` | **GitHub-hosted Ubuntu 24.04 x64**, конкретные SHA и логи ниже: официальный SDK/NuGet, Release build, xUnit/TRX, CLI, контракт Simulator, stress и каталог. Локальная песочница Debian SDK не получила. |
| `WINDOWS_HANDOFF_READY` | Подготовлены исходники, границы и [инструкция передачи](../../WINDOWS_HANDOFF.md), не готовый Windows-плагин. Windows не запускалась. |
| `WINDOWS_HOST_VERIFIED` / `PRODUCT_COMPLETE` | **Не присваиваются.** Реального nanoCAD host/SDK bridge, DWG и PDF нет; H01–H25 и сверка ГОСТ не выполнены. Учебные правила и процент не удостоверяют соответствие или безопасность. |

## R0–R2: сохранение

- На входе: HEAD `9bd03ab9753877b75f5598da0b1315252f7ab7eb`, ветка `arena/9335e807-evacuator`; в HEAD был только `README.md`, на диске — изменённый README и 148 untracked-файлов. [Полный статус](git-before.txt) и [распределение по группам](preservation-candidates.txt).
- До изменения репозитория создан `/home/user/EVACUATOR-rescue-20261029T0654Z.tar.gz`, **470924 байта**, SHA-256 `d1854ba1d6eaf8982db2b79d659dfd2d19584ed38c158b0567b0581d0f015a62`. Архив содержит весь каталог, включая `.git`, скрытые, изменённые и untracked-файлы. `gzip -t` и `tar -tzf` прошли; при создании сверены 180 обычных файлов из 243 элементов архива с деревом по SHA-256, расхождений и пропусков 0. [Проверка и ограничения](preservation.txt).
- Проверены имена и высокоточные шаблоны секретов: совпадений не найдено. Это эвристика, **не гарантия** отсутствия конфиденциальных данных в полном архиве; особенно внимательно обращаться с `.git/config`. Архив не публиковался в Git.
- Отдельные preservation-коммиты без изменения первоначального содержимого:
  - `97a57081e84e36117a662eda862029f8f652e49c` — C# solution, нормы, samples, tools;
  - `a3e63a4427bd3e4b5b1ac58fa39f7c47aeb431c5` — README, документация, demo, packaging;
  - `f92eb9efea2d5ed0331d443a87fa4f8cbfa73292` — независимый `linux_stand`;
  - `d07566376b333d9dd107bb5bb0f7065fdb087a7d` — исторические отчёты и логи.
- Индекс каждого коммита сверялся с архивом, проверялись staged diff и `git diff --cached --check`. Группы 1, 2 и 4 прошли check; группа 3 вернула 2 из-за пустой строки в конце **исторического** `linux_stand/logs/r01-negative.log`. Содержимое сохранено без подмены; [исключение записано](preservation-whitespace.txt). Новые журналы, где было необходимо, сохранены сырыми в архиве, а в читаемых копиях нормализованы только конечные пробелы.
- Обычный push текущей ветки выполнен без переписывания истории. Удалённый preservation SHA `d07566376b333d9dd107bb5bb0f7065fdb087a7d` подтверждён `git ls-remote`; дальнейшие коммиты также отправлялись в ту же ветку. Ни release, ни pull request не создавались. Прежние недоступные объекты Git не объявлены восстановленными.

## SDK, baseline и runtime

1. **Локальная Debian-песочница:** `dotnet` отсутствует; оригинальные попытки `--info`, restore, build, test и CLI вернули 127. Загрузка официального `dot.net`, `builds.dotnet.microsoft.com` и индекса `api.nuget.org` завершилась curl 35; сторонние зеркала SDK и NuGet не использовались. [Команды, вывод и коды](sdk-official-attempts.txt), [baseline](baseline/), [сырые логи](local-raw-logs.tar.gz). Никакой локальный TRX не выдаётся за реальный.
2. **Независимый baseline исходного C# снимка `d075663…`:** [GitHub run 40833191162](https://github.com/Igorr112323/EVACUATOR/actions/runs/40833191162), Ubuntu 24.04; из-за `global.json` реально выбран SDK **10.0.403** из образа runner, хотя setup-dotnet установил 10.0.400. Официальный NuGet restore 0; Release build 0 ошибок и 0 предупреждений; xUnit **134 passed, 0 failed, 0 skipped**. [Исходный build](github-baseline/build.log), [test](github-baseline/test.log), [TRX](github-baseline/trx/results.trx) и [raw](github-baseline-40833191162-raw.tar.gz). Утверждение о прежних 134 теперь дополнительно воспроизведено здесь; не следует смешивать его с историческим пересказом или отдельными Python 58.
3. **Проверка исправленного C# снимка `edbff4201c087b11ed20c583d8956bac75bd8910`:** [GitHub run 40834536893](https://github.com/Igorr112323/EVACUATOR/actions/runs/40834536893), официальный SDK **10.0.400** в отдельном `$RUNNER_TEMP/evacuator-dotnet`; restore только `api.nuget.org`, аудит NuGet скриптом не отключался. Release build 0 ошибок/0 предупреждений; xUnit **146 passed, 0 failed, 0 skipped**. [SDK](github-final/dotnet-info.log), [restore](github-final/restore.log), [build](github-final/build.log), [test](github-final/test.log), [TRX](github-final/trx/results.trx), [все статусы](github-final/statuses.txt), [raw archive](github-final-40834536893-raw.tar.gz).
4. Исходные логи и TRX получены из GitHub Checks API частями и собраны с проверкой SHA-256, поскольку редирект GitHub artifact storage из локальной песочницы недоступен. Сценарий доступен в `tools/retrieve-ci-evidence.py`; GitHub artifacts хранятся ограниченное время. Первоначальная ошибка синтаксиса workflow и обрезание первых аннотаций также [зафиксированы](github-ci-bootstrap.txt), [зафиксированы отдельно](github-ci-first-run.txt), а не выданы за тесты.

Указанные в [официальных метаданных Microsoft](https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.11/release.json) SHA-512 и URL SDK 10.0.400 записаны в `sdk-official-attempts.txt`; сам скачанный в CI архив отдельно на SHA-512 не измерялся. SDK/TFM прототипа не подтверждают совместимость с nanoCAD.

## Реальные исправления и тесты

| Дефект | Red до исправления | Исправление | Green |
|---|---|---|---|
| Смежные обратные коллинеарные сегменты маршрута не считались пересечением. | `ef8f3ab29f4493a683650965ed4bb345876313e6`, [TRX/log: 3 fail](regression-red/) | `4906514a3970de8609c96d5e3d5c0ceefa9f89b2` | [146 passed](github-final/trx/results.trx) |
| Сообщения R01 и R06 менялись при `en-US`/`ru-RU`. | тот же red: десятичный разделитель и пробел перед `%` | `7b15499442c85bf1969f9a608b31b6a70aa62528` | те же регрессии прошли |
| Код в SVG-легенде касался 2:1 носителя: начало текста x=120, граница знака x=121. | `edabd14d5a58b84dff1f6aa94c4e7588ee2c3185`, [TRX/log: 1 fail](legend-red/) | `f9bf7527eb461878cb9f93905f3fe0f7f542c14c` | геометрический regression и повторная визуальная проверка |

Тесты не удалялись и не ослаблялись. Нормативные коды, значения, TFM, `global.json` и `SdkGate` ради получения зелёного результата не менялись.

`CadDocumentContractTests` — шесть повторно используемых проверок снапшота, stale commit, невалидного изменения, no-op, undo и уведомлений; green относится к `InMemoryCadDocumentContext`. Будущему адаптеру нужен отдельный host harness с изоляцией документа и проверенным TFM/потоками; host-реализации сейчас нет. Stress: 200 знаков, два повторных heal и второй идемпотентный проход, детерминированные JSON/отчёт/SVG; для конкретного запуска TRX содержит время и SHA SVG. Это не предел производительности и не доказательство безопасности.

## CLI и каталог

В green run положительный сценарий вернул 0; неизвестная опция и JSON без координаты `y` вернули ожидаемый 1, причём для повреждённого плана выходной каталог не создан. Повторный запуск на `demo_after_plan.json` не изменил план; оба комплекта SVG и JSON синтаксически проверены. В учебном сценарии после heal осталось 0 ошибок семи учебных правил, но есть предупреждения и ручные действия; результат не является заключением по ГОСТ. Сами файлы: [до/после и легенда](github-final/cli-first/), [повтор](github-final/cli-second/).

[sign_catalog.svg](../sign_catalog.svg) создан инструментом `tools/Evakuator.SignCatalog` через `SignCatalogExporter` → `SvgPreviewExporter.ExportLegend` → production `SignGeometryGenerator`, не ручной перерисовкой. 30 кодов и видимое предупреждение проверены, SHA-256 `1512afeaabf5159606be509b6d72a20a66527fa5d3390aefcc693d45c849e5fc`; [генерация и проверка](github-final/catalog-check.log). Растеризация для визуальной проверки была вспомогательной и не является официальным эталоном.

## Что остаётся заблокированным

- Реальная версия nanoCAD/SDK/CLR и совместимый TFM, bridge, команды, grips, сериализация, транзакции, многодокументность и host UI `[VERIFY_IN_NANOCAD_SDK]`.
- Все [H01–H25](../../WINDOWS_HOST_TEST_RUNBOOK.md), установка/удаление, истинные DWG и PDF, Windows build/run — **NOT RUN/BLOCKED**.
- Официальные применимые нормативные тексты, профессиональная сверка 30 знаков и R01–R07, безопасность конкретного здания и итоговый продукт.
- Отдельный аудит транзитивных лицензий/уязвимостей для поставки; текущий restore не является исчерпывающим security audit. ОС-специфичные watcher-события, крайние координаты и худшая сложность требуют дальнейшей проверки.

## Проверка воспроизводимости и ветки

Workflow на GitHub выкачивает репозиторий и через `git archive` экспортирует точный baseline либо commit запуска в новый каталог, восстанавливая NuGet в отдельный каталог. Это не использует незакоммиченные файлы. Для проверки текущего remote и наличия ключевых файлов:

```sh
git status --short --branch
git rev-parse HEAD
git ls-remote --heads origin refs/heads/arena/9335e807-evacuator
git ls-tree -r --name-only HEAD -- Evakuator.sln src tests norms samples docs linux_stand
```

[Матрица переносимости](../../PORTABILITY_MATRIX.md) · [Архитектура](../../ARCHITECTURE.md) · [Windows handoff](../../WINDOWS_HANDOFF.md) · [Индекс доказательств](../EVIDENCE_INDEX.md).
