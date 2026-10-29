# Индекс доказательств — Environment Gate

> **Актуальный индекс RESCUE-AND-VERIFY-01, 29.10.2026.** Исторический раздел ниже сохранён для контекста; его «текущий статус», SHA и утверждения о несохранённых исходниках относятся к 28.10. Актуальная ветка — `arena/9335e807-evacuator`; см. также [полный отчёт](rescue_20261029/REPORT.md).
>
> C# тесты реально выполнялись на GitHub-hosted Ubuntu, а не в локальной песочнице. Проверка на Windows и в nanoCAD не выполнялась. Python `linux_stand` и его исторические 58 тестов — отдельный проект и в C# счётчики не входят.

## Первичные доказательства RESCUE

| Артефакт | Что проверено и границы |
|---|---|
| [preservation.txt](rescue_20261029/preservation.txt), [git-before.txt](rescue_20261029/git-before.txt), [candidates](rescue_20261029/preservation-candidates.txt) | Аварийный архив с `.git`, исходное состояние, четыре побайтовых preservation-коммита, первый проверенный push. Архив хранится вне Git; эвристический скан не гарантирует отсутствия чувствительных данных. |
| [Локальный baseline](rescue_20261029/baseline/), [официальные попытки](rescue_20261029/sdk-official-attempts.txt), [сырые логи](rescue_20261029/local-raw-logs.tar.gz) | На Debian нет `dotnet`; команды вернули 127, официальные сетевые запросы — 35. Это не неудачные C# тесты, они там не стартовали. |
| [GitHub Linux baseline](rescue_20261029/github-baseline-source.txt), [build](rescue_20261029/github-baseline/build.log), [test](rescue_20261029/github-baseline/test.log), [TRX](rescue_20261029/github-baseline/trx/results.trx), [полный сырой архив](rescue_20261029/github-baseline-40833191162-raw.tar.gz) | Сохранённый исходный SHA `d07566376b333d9dd107bb5bb0f7065fdb087a7d`: Release 0 ошибок/0 предупреждений и 134/134 xUnit, не host. Запуск с SDK 10.0.403 выбранным roll-forward, официальный NuGet. |
| [Красный regression run](rescue_20261029/regression-red/README.txt), [log](rescue_20261029/regression-red/test.log), [TRX](rescue_20261029/regression-red/trx/results.trx) | До исправлений 134 passed и три действительных fail (разворот линии, две культурно-зависимые диагностики). Сохранены исходный SHA, raw archive и хеши. |
| [Красный тест легенды](rescue_20261029/legend-red/README.txt), [log](rescue_20261029/legend-red/test.log), [TRX](rescue_20261029/legend-red/trx/results.trx) | До исправления метка прямоугольного носителя пересекала область знака; 145 passed / 1 failed. Исправление сделано отдельным коммитом. |
| [Linux green run](rescue_20261029/github-final-source.txt), [статусы](rescue_20261029/github-final/statuses.txt), [build](rescue_20261029/github-final/build.log), [test](rescue_20261029/github-final/test.log), [TRX](rescue_20261029/github-final/trx/results.trx), [raw archive](rescue_20261029/github-final-40834536893-raw.tar.gz) | SHA исходного снимка `edbff4201c087b11ed20c583d8956bac75bd8910`: 146/146, 0 пропусков, Release 0/0, CLI и каталог. Позднейший снимок может отличаться документацией и инфраструктурой; это не nanoCAD и не Windows. |
| [Учебный каталог](sign_catalog.svg) | 30 записей, сгенерированных C# production-рендерером; SHA-256 `1512afeaabf5159606be509b6d72a20a66527fa5d3390aefcc693d45c849e5fc`. Явные предупреждения не являются нормативной сверкой или DWG-каталогом. |
| [Аудит переносимого ядра](rescue_20261029/portable-audit.md) | Исходники, найденные и исправленные дефекты, тесты и оставшиеся риски; не нормативная или security-экспертиза. |
| [Windows handoff](../WINDOWS_HANDOFF.md), [H01–H25](../WINDOWS_HOST_TEST_RUNBOOK.md), [портируемость](../PORTABILITY_MATRIX.md) | Подготовленные инструкции; Windows и все реальные host-сценарии остаются NOT RUN/BLOCKED. |

## Исторический Environment Gate 28.10.2026

> **Актуализация 28.10.2026, 18:47 UTC:** Git-объект `9e9ffb04…`, названный ниже историческим снимком, в текущей локальной базе отсутствует. Файлы исходников доступны в рабочем дереве; состояние не является чистой Git-копией. Новые журналы и контрольные суммы: [повторный Environment Gate](product_recheck_20261028/REPORT.md).
>
> Доказательства повторной проверки зафиксированы отдельно в локальном коммите `1c2b96f4c551ae72be681c9ed5933c41a9a58a78`; пользовательские исходники в него не включены. Push и release не выполнялись.

Дата: 28.10.2026. Статус: **`BLOCKED`**. Здесь приведены только существующие файлы и реально выполненные проверки. Отсутствующий артефакт помечен как отсутствующий, пустых DWG/PDF/host-логов нет. Снимок исходного прототипа: `9e9ffb04e8660fb3d780e9370862b06ec721e879`.

## Имеющиеся доказательства и пределы

| Файл | Что подтверждает | Чего не подтверждает |
|---|---|---|
| [baseline/environment.txt](baseline/environment.txt) | Фактическая ОС, архитектура, PATH/display, результаты ограниченного поиска host/SDK/нормативных файлов/DWG и отдельных поисков с кодами возврата. | Не доказывает отсутствия файлов вне проверенных путей и шаблонов; не определяет версию, лицензию или совместимость nanoCAD и SDK; не является host-тестом. |
| [baseline/dotnet-attempts.txt](baseline/dotnet-attempts.txt) | Реальные четыре вызова baseline и вызов preflight, каждый с кодом 127 из-за отсутствия `dotnet` или `pwsh`. | Не является успешно выполненной сборкой, тестом, audit или результатом preflight. |
| [baseline/static-audit.txt](baseline/static-audit.txt), [baseline/static-audit.py](baseline/static-audit.py) | Повторяемая **статическая** проверка сохранённого снимка: 6 JSON, 14 XML-документов, 30 различных учебных кодов и 30 меток проверки, ссылки 8 проектов, ограниченный буквальный поиск заглушек и suppressions. Скрипт сверяет SHA-256 входов и работает по Git-снимку либо по соответствующим манифесту файлам. | Не выполняет JSON Schema, семантическую нормативную сверку, компиляцию, тесты, XAML или отрисовку; текстовый поиск не гарантирует отсутствия всех способов пропуска тестов или скрытия предупреждений. |
| [baseline/git-initial.txt](baseline/git-initial.txt), [baseline/git-objects.txt](baseline/git-objects.txt) | Исходные ветка/HEAD/status, отсутствие локального объекта `68e8cde`, результат read-only запроса GitHub и SHA снимка сохранения. | Не восстанавливает прежний объект и не утверждает, что два коммита идентичны. |
| [baseline/preexisting-sha256.txt](baseline/preexisting-sha256.txt), [baseline/preservation-hashes.txt](baseline/preservation-hashes.txt) | Хеши 94 исходных файлов и их проверка сразу после сохранения снимка. | Текущий `CURATOR_REPORT.md` намеренно обновлён после проверки: манифест относится к исходному снимку, а не ко всем текущим файлам отчёта. |
| [baseline/CURATOR_REPORT_previous.md](baseline/CURATOR_REPORT_previous.md) | Неизменённая копия предыдущего отчёта; ранее сообщённые результаты и контекст сторонних offline-источников. | Утверждения о 134 тестах, GUI и сборке — исторические; в текущей среде они не воспроизведены и не являются host-приёмкой. |
| [../../src/Evakuator.CadAdapter/NanoCad/SdkGate.cs](../../src/Evakuator.CadAdapter/NanoCad/SdkGate.cs) | В исходнике намеренно блокируется сборка с `NANOCAD_SDK` до реализации официального bridge. | Не является SDK, сборкой плагина или проверкой API. |
| [../../norms/gost_12_4_026_2015.json](../../norms/gost_12_4_026_2015.json), [../../docs/GOST_VERIFICATION_CHECKLIST.md](../../docs/GOST_VERIFICATION_CHECKLIST.md) | Версия `0.1-demo`, явное предупреждение и список необходимых проверок. | Не являются официальным ГОСТ и не подтверждают правильность норм, знаков или безопасность. |
| [../../docs/demo/](../../docs/demo/) | Ранее сохранённые SVG и снимки **отдельного** окна Avalonia из прототипа. | Не являются изображениями из nanoCAD, DWG, печатью, текущим GUI-тестом или доказательством исполнения H01–H25. |

## Соответствие 14 пунктам задания

Статус означает текущую продуктовую приёмку, а не наличие исходного кода. Подробная матрица — [CURATOR_REPORT.md](../../CURATOR_REPORT.md), раздел 3.

| № | Требование | Существующее доказательство или его отсутствие | Что не доказано |
|---|---|---|---|
| 1 | Плагин в nanoCAD | [environment](baseline/environment.txt), [SDK gate](../../src/Evakuator.CadAdapter/NanoCad/SdkGate.cs) | Реальная сборка и загрузка. |
| 2 | 30 знаков и каталог | [static audit](baseline/static-audit.txt), [учебные данные](../../norms/gost_12_4_026_2015.json) | Нормативная сверка и работа в host. |
| 3 | DWG-объект знака | Нет host/DWG evidence; [environment](baseline/environment.txt). | Сериализация, отрисовка, grips, трансформации и повторное открытие. |
| 4 | DWG-объект пути | Нет host/DWG evidence; [environment](baseline/environment.txt). | Стили, редактирование, стрелки, сериализация в host. |
| 5 | Рамка, шапка, текст, маркер | Исходники и [static audit](baseline/static-audit.txt). | Поведение в nanoCAD и соответствие нормам. |
| 6 | Динамическая легенда | Исходники; [environment](baseline/environment.txt). | Синхронизация с объектами реального DWG и undo/redo. |
| 7 | R01–R07 и инспектор | Исходники; [чек-лист](../../docs/GOST_VERIFICATION_CHECKLIST.md). | Проверка на DWG, навигация, подсветка, нормативная достоверность. |
| 8 | AutoFix и одна host undo-транзакция | Код симуляции; host отсутствует по [environment](baseline/environment.txt). | Безопасность применений и host-атомарность/undo. |
| 9 | Hot reload/last-known-good | Исходник watcher; [dotnet attempts](baseline/dotnet-attempts.txt). | Работа в host и повторяемый текущий тест. |
| 10 | Команды и UI в nanoCAD | [SDK gate](../../src/Evakuator.CadAdapter/NanoCad/SdkGate.cs); host отсутствует. | Регистрация, вызов, визуализация в host. |
| 11 | Установка, удаление, ribbon/help/About | [описание шаблонов](../../packaging/README.md). | Рабочий установщик, чистая установка и удаление; шаблоны не являются пакетом. |
| 12 | Демонстрационный DWG и PDF | [environment](baseline/environment.txt); host-DWG/PDF отсутствуют. | Демосценарий и штатная печать. |
| 13 | Unit- и host-тесты | [dotnet attempts](baseline/dotnet-attempts.txt), матрица ниже. | Текущий запуск unit и выполнение H01–H25. |
| 14 | Evidence Pack | Этот индекс и реально существующие baseline-файлы. | Финальная приёмка: host-логи, DWG, PDF, пакет и результаты отсутствуют. |

## H01–H25: отсутствующие host-доказательства

Все 25 сценариев — **`BLOCKED`**, не выполнены; [ограниченный поиск среды](baseline/environment.txt) объясняет причину, но не заменяет ни один тест. Ниже перечислены требуемые доказательства, которых сейчас нет.

| ID | Требуемые отсутствующие доказательства |
|---|---|
| H01 | Загрузка: скриншот и host log. |
| H02 | Команды: скриншот/журнал выполнения. |
| H03 | Вставка 30 знаков: DWG-каталог и отчёт. |
| H04 | Grips/move/rotate/scale: до/после и свойства. |
| H05 | Знак save/close/reopen: DWG и round-trip log. |
| H06 | Путь: DWG и до/после редактирования/стрелок. |
| H07 | Путь: DWG и round-trip log. |
| H08 | Рамка, шапка, текст, маркер: DWG и скриншот. |
| H09 | Легенда после insert/delete/change: до/после. |
| H10 | Легенда после undo/redo: до/после. |
| H11 | Проверка bad DWG: отчёт и скриншот инспектора. |
| H12 | Подсветка и переход: скриншот. |
| H13 | AutoFix и повторная проверка: diff и отчёт после. |
| H14 | Одна undo/redo-транзакция AutoFix: до/после. |
| H15 | Неразрешимый маршрут: отчёт с сохранённой ошибкой. |
| H16 | Повреждённая нормбаза: ошибка и last-known-good в host. |
| H17 | Валидный hot reload: новая версия в host без пересборки. |
| H18 | Два документа: доказательство изоляции результатов. |
| H19 | Внешнее редактирование: повторная проверка изменений. |
| H20 | Повторное открытие и полная проверка: отчёт. |
| H21 | Установка вне build: install log и скриншот. |
| H22 | Удаление/повторная установка: лог. |
| H23 | Штатная печать: реальный PDF и настройки. |
| H24 | Офлайн-help и About: скриншоты. |
| H25 | DWG без плагина: задокументированный результат. |

Установочного пакета, host-сборки, DWG и PDF нет; их пути не подменяются демонстрационными файлами. При появлении разрешённой среды каждый результат следует добавлять с версией host/SDK, датой, точным SHA и ограничениями.
