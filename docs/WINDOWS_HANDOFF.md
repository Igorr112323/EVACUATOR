# Windows handoff: что можно передать, а что ещё не готово

**Статус handoff: документация и переносимый C#-прототип подготовлены. Windows не проверялась; рабочего nanoCAD-плагина, установщика, DWG и PDF нет.** `WINDOWS_HANDOFF_READY` относится только к передаче исходников и плана работ, не к сборке/загрузке в host. Нормативные утверждения и изображения остаются `[ПРОВЕРИТЬ ПО ТЕКСТУ ГОСТ]`; не использовать результаты для реальной эвакуации.

## 1. Получить и зафиксировать снимок

В чистом каталоге клонировать текущую сессионную ветку и записать точный результат `git rev-parse HEAD`; сверить его с финальным отчётом и [индексом доказательств](verification/EVIDENCE_INDEX.md). До передачи результатов удалить из логов персональные пути и другие чувствительные сведения. Не отправлять лицензии, ключи, пароли или проприетарные SDK-файлы в Git.

```powershell
git clone --single-branch --branch arena/9335e807-evacuator https://github.com/Igorr112323/EVACUATOR.git
Set-Location EVACUATOR
git rev-parse HEAD
git status --short --branch
```

Установить **официальный** .NET SDK 10 согласно Microsoft. `global.json` разрешает выбрать более новый feature SDK внутри 10.x: сохранить `dotnet --info`, а не предполагать версию. Доступен официальный NuGet `https://api.nuget.org/v3/index.json`. Ничего из этого не подтверждает совместимость плагина с nanoCAD.

## 2. Проверка автономного C# на Windows — ещё не выполнена

Пример команд из корня решения; прерывать процесс при ненулевом `$LASTEXITCODE`, а логи/TRX сохранять вне релизного пакета. `artifacts` игнорируется Git. При работе с PowerShell и pipe код выхода проверяется сразу после запуска `dotnet`.

```powershell
New-Item -ItemType Directory -Force artifacts\windows-evidence | Out-Null
dotnet --info 2>&1 | Tee-Object artifacts\windows-evidence\dotnet-info.log
if ($LASTEXITCODE -ne 0) { throw "dotnet --info failed: $LASTEXITCODE" }
dotnet restore Evakuator.sln --source https://api.nuget.org/v3/index.json 2>&1 | Tee-Object artifacts\windows-evidence\restore.log
if ($LASTEXITCODE -ne 0) { throw "restore failed: $LASTEXITCODE" }
dotnet build Evakuator.sln -c Release --no-restore 2>&1 | Tee-Object artifacts\windows-evidence\build.log
if ($LASTEXITCODE -ne 0) { throw "build failed: $LASTEXITCODE" }
dotnet test tests\Evakuator.Tests\Evakuator.Tests.csproj -c Release --no-restore --no-build --logger 'trx;LogFileName=windows.trx' --results-directory artifacts\windows-evidence\trx 2>&1 | Tee-Object artifacts\windows-evidence\test.log
if ($LASTEXITCODE -ne 0) { throw "tests failed: $LASTEXITCODE" }
dotnet run --project src\Evakuator.CliDemo -c Release --no-restore --no-build -- --norms norms\gost_12_4_026_2015.json --plan samples\bad_bazaar_plan.json --output-dir artifacts\windows-demo 2>&1 | Tee-Object artifacts\windows-evidence\cli.log
if ($LASTEXITCODE -ne 0) { throw "CLI failed: $LASTEXITCODE" }
```

Для учебного каталога отдельно восстановить и собрать проект `tools\Evakuator.SignCatalog\Evakuator.SignCatalog.csproj`, затем запустить его с двумя аргументами: путь JSON базы и путь выходного SVG. Перед запуском отдельного окна Avalonia обеспечить графическую среду и проверить её зависимости; это не UI nanoCAD. Windows-прогоны, наличие инфраструктуры или ожидаемый результат здесь не утверждаются.

## 3. Реальный host — самостоятельный заблокированный этап

1. Подтвердить установленную и лицензированную версию nanoCAD, её ОС/разрядность и официальный SDK/документацию именно этой сборки. Не скачивать проприетарный SDK из посторонних зеркал. Платформа nanoCAD 26 — исследованный кандидат, не проверенное окружение.
2. По официальным примерам установить реальную CLR/TFM-совместимость. `net10.0` в этом прототипе не является доказательством совместимости. Публичный пакет `nanoCAD.Platform.NET` 26 с `net60` — reference-only и не заменяет host или полный MultiCAD SDK.
3. Не убирать `SdkGate.cs` ради успешной сборки. Сначала спроектировать и реализовать настоящий bridge, регистрацию, объекты, сериализацию, транзакции, grips и UI **по официальному SDK** `[VERIFY_IN_NANOCAD_SDK]`. Текущий Simulator не является реализацией этих возможностей.
4. Повторно использовать независимые Domain/Norms/Geometry/Validation и абстрактные `CadDocumentContractTests`, если подтверждена совместимость TFM. Реализовать host harness с новым документом на каждый тест, его корректным закрытием и согласованием с моделью потоков host. При несовместимости сначала спроектировать и протестировать подходящий target/границу; не обещать загрузку текущего net10-кода.
5. Выполнить [H01–H25](WINDOWS_HOST_TEST_RUNBOOK.md), сохранить реальные логи, тестовые DWG/PDF и точные SHA. Нельзя заменять их SVG, DXF, скриншотами автономного окна или Python-стендом.

## 4. Нормы и безопасность

Официальные применимые издания и изменения должны быть получены законно и проверены профильным специалистом. База `0.1-demo`, 30 схематичных знаков и правила R01–R07 не удостоверены. Не изменяйте нормативные сведения по памяти и не выдавайте учебный процент за соответствие, сертификацию или безопасность. Перед возможной поставкой отдельно нужны проверка лицензий/зависимостей, безопасность, реальный установщик и host-испытания.
