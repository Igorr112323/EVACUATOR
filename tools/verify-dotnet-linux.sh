#!/usr/bin/env bash
# Run against an explicit, already-exported source tree. Never deletes output.
# The NuGet source is the official nuget.org endpoint; audit is not disabled.
set -uo pipefail

if [ "$#" -ne 2 ]; then
  printf 'Usage: bash %s SOURCE_DIRECTORY EVIDENCE_DIRECTORY\n' "$0" >&2
  exit 64
fi
source_dir=$(cd "$1" && pwd) || exit 64
mkdir -p "$2" || exit 1
out=$(cd "$2" && pwd) || exit 1
if [ -n "$(find "$out" -mindepth 1 -maxdepth 1 ! -name source-commit.txt -print -quit)" ]; then
  printf 'Refusing to overwrite existing evidence in %s\n' "$out" >&2
  exit 73
fi
cd "$source_dir" || exit 1
status=0
: > "$out/statuses.txt"

capture() {
  local name="$1" expected="$2" actual
  shift 2
  {
    printf '$'
    printf ' %q' "$@"
    printf '\n'
    "$@"
    actual=$?
    printf 'exit=%s\n' "$actual"
    printf '%s=%s (expected %s)\n' "$name" "$actual" "$expected" >> "$out/statuses.txt"
    if [ "$actual" -ne "$expected" ]; then status=1; fi
  } > "$out/$name.log" 2>&1
}

{
  printf 'source-directory=%s\n' "$source_dir"
  printf 'source-commit=%s\n' "${EVAC_SOURCE_COMMIT:-unknown}"
  printf 'utc='; date -u +'%Y-%m-%dT%H:%M:%SZ'
  printf 'platform='; uname -a
  printf 'nuget-source=https://api.nuget.org/v3/index.json\n'
  printf 'NuGetAudit is not disabled by this script.\n'
} > "$out/environment.txt"

capture dotnet-info 0 dotnet --info
capture restore 0 dotnet restore Evakuator.sln --source https://api.nuget.org/v3/index.json
capture build 0 dotnet build Evakuator.sln -c Release --no-restore
capture test 0 dotnet test tests/Evakuator.Tests/Evakuator.Tests.csproj -c Release --no-restore --no-build --logger 'trx;LogFileName=results.trx' --results-directory "$out/trx"

if [ -f src/Evakuator.CliDemo/bin/Release/net10.0/Evakuator.CliDemo.dll ]; then
  capture cli-positive 0 dotnet run --project src/Evakuator.CliDemo -c Release --no-restore --no-build -- --norms norms/gost_12_4_026_2015.json --plan samples/bad_bazaar_plan.json --output-dir "$out/cli-first"
  capture cli-negative 1 dotnet run --project src/Evakuator.CliDemo -c Release --no-restore --no-build -- --unknown-option
  if ! grep -q 'Ошибка демонстрации:' "$out/cli-negative.log"; then
    printf 'cli-negative-message=FAIL (expected application diagnostic)\n' >> "$out/statuses.txt"
    status=1
  else
    printf 'cli-negative-message=PASS\n' >> "$out/statuses.txt"
  fi
  capture cli-malformed-prepare 0 python3 - samples/reference_clean_plan.json "$out/malformed-plan.json" <<'PY'
import json
import sys
with open(sys.argv[1], encoding='utf-8') as f:
    plan = json.load(f)
del plan['signs'][0]['insertionPoint']['y']
with open(sys.argv[2], 'w', encoding='utf-8') as f:
    json.dump(plan, f, ensure_ascii=False, indent=2)
PY
  capture cli-malformed 1 dotnet run --project src/Evakuator.CliDemo -c Release --no-restore --no-build -- --norms norms/gost_12_4_026_2015.json --plan "$out/malformed-plan.json" --output-dir "$out/cli-invalid-output"
  if ! grep -q 'Ошибка демонстрации:' "$out/cli-malformed.log" || [ -e "$out/cli-invalid-output" ]; then
    printf 'cli-malformed-check=FAIL (diagnostic missing or output was created)\n' >> "$out/statuses.txt"
    status=1
  else
    printf 'cli-malformed-check=PASS\n' >> "$out/statuses.txt"
  fi
  if [ -f "$out/cli-first/demo_after_plan.json" ]; then
    capture cli-second 0 dotnet run --project src/Evakuator.CliDemo -c Release --no-restore --no-build -- --norms norms/gost_12_4_026_2015.json --plan "$out/cli-first/demo_after_plan.json" --output-dir "$out/cli-second"
    capture cli-artifact-check 0 python3 - "$out" <<'PY'
import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

out = Path(sys.argv[1])
first, second = out / 'cli-first', out / 'cli-second'
for directory in (first, second):
    for name in ('demo_before.svg', 'demo_after.svg', 'legend_table.svg'):
        root = ET.parse(directory / name).getroot()
        if root.tag != '{http://www.w3.org/2000/svg}svg':
            raise ValueError(f'{directory / name} is not SVG')
    for name in ('validation_before.json', 'validation_after.json', 'healing_diff.json', 'demo_after_plan.json'):
        with (directory / name).open(encoding='utf-8') as f:
            json.load(f)
with (first / 'demo_after_plan.json').open(encoding='utf-8') as f:
    first_plan = json.load(f)
with (second / 'demo_after_plan.json').open(encoding='utf-8') as f:
    second_plan = json.load(f)
with (second / 'healing_diff.json').open(encoding='utf-8') as f:
    changes = json.load(f)['changes']
if first_plan != second_plan or changes:
    raise ValueError('Second healing pass changed the resulting plan')
print('PASS: both runs produced parseable JSON and SVG; second pass did not change the plan')
PY
  else
    printf 'cli-second=BLOCKED (first run did not produce an after plan)\n' >> "$out/statuses.txt"
    printf 'cli-artifact-check=BLOCKED\n' >> "$out/statuses.txt"
    status=1
  fi
else
  printf 'cli-positive=BLOCKED (Release CLI binary missing)\ncli-negative=BLOCKED\ncli-second=BLOCKED\ncli-artifact-check=BLOCKED\n' >> "$out/statuses.txt"
  status=1
fi

catalog_project=tools/Evakuator.SignCatalog/Evakuator.SignCatalog.csproj
if [ -f "$catalog_project" ]; then
  capture catalog-restore 0 dotnet restore "$catalog_project" --source https://api.nuget.org/v3/index.json
  capture catalog-build 0 dotnet build "$catalog_project" -c Release --no-restore
  if [ -f tools/Evakuator.SignCatalog/bin/Release/net10.0/Evakuator.SignCatalog.dll ]; then
    capture catalog-generate 0 dotnet run --project "$catalog_project" -c Release --no-restore --no-build -- norms/gost_12_4_026_2015.json "$out/sign_catalog.svg"
    capture catalog-check 0 python3 - "$out/sign_catalog.svg" norms/gost_12_4_026_2015.json <<'PY'
import hashlib
import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

path = Path(sys.argv[1])
with open(sys.argv[2], encoding='utf-8') as f:
    signs = json.load(f)['signs']
root = ET.parse(path).getroot()
namespace = '{http://www.w3.org/2000/svg}'
if root.tag != namespace + 'svg' or len(signs) != 30:
    raise ValueError('Expected SVG with the 30-sign demo database')
texts = [''.join(element.itertext()) for element in root.iter(namespace + 'text')]
for sign in signs:
    if texts.count(sign['code']) != 1 or '[ПРОВЕРИТЬ ПО ТЕКСТУ ГОСТ]' not in sign['gostClauseRef']:
        raise ValueError('Missing catalog code or verification marker: ' + sign['code'])
if not any('[ПРОВЕРИТЬ ПО ТЕКСТУ ГОСТ]' in text for text in texts):
    raise ValueError('Missing visible warning')
if any(element.tag in (namespace + 'image', namespace + 'script') for element in root.iter()):
    raise ValueError('Unexpected image or script')
print('PASS: 30 labelled vector entries and visible demo/ГОСТ warning')
print('sha256=' + hashlib.sha256(path.read_bytes()).hexdigest())
PY
    if [ -f docs/verification/sign_catalog.svg ]; then
      capture catalog-reference-compare 0 cmp docs/verification/sign_catalog.svg "$out/sign_catalog.svg"
    else
      printf 'catalog-reference=NOT_PRESENT_IN_THIS_SNAPSHOT\n' >> "$out/statuses.txt"
    fi
  else
    printf 'catalog-generate=BLOCKED (catalog executable missing)\ncatalog-check=BLOCKED\n' >> "$out/statuses.txt"
    status=1
  fi
else
  printf 'catalog=NOT_PRESENT_IN_THIS_SNAPSHOT\n' >> "$out/statuses.txt"
fi

printf '%s\n' "$status" > "$out/overall-exit.txt"
cat "$out/statuses.txt"
exit "$status"
