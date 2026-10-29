#!/usr/bin/env python3
"""Read-only static inspection of the preserved baseline; not a build or unit test."""
from collections import Counter
from pathlib import Path
import hashlib
import json
import re
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
BASELINE = '9e9ffb04e8660fb3d780e9370862b06ec721e879'

def pairs(items):
    result = {}
    for key, value in items:
        if key in result:
            raise ValueError(f'duplicate JSON key: {key}')
        result[key] = value
    return result

def reject_constant(value):
    raise ValueError(f'non-finite JSON number: {value}')

manifest = {}
for line in (ROOT / 'docs/verification/baseline/preexisting-sha256.txt').read_text().splitlines():
    digest, path = line.split('  ', 1)
    manifest[path] = digest
tracked = sorted(manifest)
commit_available = subprocess.run(
    ['git', 'cat-file', '-e', f'{BASELINE}^{{commit}}'], cwd=ROOT,
    stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False,
).returncode == 0

# Prefer the immutable commit; fall back to manifest-verified workspace inputs if
# the checkout was restored without local Git objects. Never inspect changed inputs
# silently as if they were the preserved baseline.
def read_snapshot(path):
    if commit_available:
        data = subprocess.check_output(['git', 'show', f'{BASELINE}:{path}'], cwd=ROOT)
    else:
        data = (ROOT / path).read_bytes()
    if hashlib.sha256(data).hexdigest() != manifest[path]:
        raise ValueError(f'Baseline input changed or corrupt: {path}')
    return data

print(f'Baseline commit: {BASELINE}')
print('Baseline input source:', 'Git object' if commit_available else 'manifest-verified workspace')
print(f'Tracked files in baseline: {len(tracked)}')
json_files = [p for p in tracked if p.endswith('.json')]
parsed = {}
for p in json_files:
    parsed[p] = json.loads(read_snapshot(p), object_pairs_hook=pairs, parse_constant=reject_constant)
    print(f'JSON syntax, duplicate keys, finite numbers: OK: {p}')
print(f'JSON files checked: {len(json_files)} (not a schema or semantic validation)')

xml_files = [p for p in tracked if p.endswith(('.csproj', '.axaml', '.xml', '.svg'))]
for p in xml_files:
    ET.fromstring(read_snapshot(p))
    print(f'XML well-formed: OK: {p}')
print(f'XML files checked: {len(xml_files)} (not a XAML/CAD build or rendering test)')

norms = parsed['norms/gost_12_4_026_2015.json']
signs = norms['signs']
codes = [s['code'] for s in signs]
markers = sum('[ПРОВЕРИТЬ ПО ТЕКСТУ ГОСТ]' in s.get('gostClauseRef', '') for s in signs)
assert len(signs) == 30 and len(set(codes)) == 30
assert markers == 30
print(f'Demo normbase version: {norms["version"]}; signs={len(signs)}; unique codes={len(set(codes))}; verification markers={markers}')
print('Categories (self-reported by demo JSON):', dict(sorted(Counter(s['category'] for s in signs).items())))
print('Demo codes:', ', '.join(codes))
print('This count does not establish official correctness of codes, shapes, colours, sizes, or legal compliance.')

projects = [p for p in tracked if p.endswith('.csproj')]
project_paths = {Path(p).as_posix() for p in projects}
graph = {}
for p in projects:
    tree = ET.fromstring(read_snapshot(p))
    refs = []
    for ref in tree.iter('ProjectReference'):
        resolved = (ROOT / Path(p).parent / ref.attrib['Include'].replace('\\', '/')).resolve()
        refs.append(resolved.relative_to(ROOT).as_posix())
    assert all(ref in project_paths for ref in refs), f'unresolved project reference: {p}: {refs}'
    graph[p] = refs
    print(f'Project references: {p}: {", ".join(refs) if refs else "none"}')
core_names = ('Evakuator.Domain', 'Evakuator.Norms', 'Evakuator.Geometry', 'Evakuator.Validation')
for project in graph:
    if not any(project.startswith(f'src/{name}/') for name in core_names):
        continue
    seen, todo = set(), list(graph[project])
    while todo:
        item = todo.pop()
        if item in seen:
            continue
        seen.add(item)
        assert not (item.startswith('src/Evakuator.CadAdapter/') or item.startswith('src/Evakuator.UI/')), f'boundary violation: {project} -> {item}'
        todo.extend(graph[item])
print('Core -> UI/CadAdapter project-reference boundary: OK (static project graph only)')
print(f'Projects checked: {len(projects)}')

patterns = {
    'test Skip=': re.compile(r'\bSkip\s*='),
    'NotImplementedException': re.compile(r'\bNotImplementedException\b'),
    'TODO/FIXME': re.compile(r'\b(?:TODO|FIXME)\b'),
    'conditional compiler error/warning': re.compile(r'^\s*#\s*(?:error|warning)\b'),
    'warning suppressions': re.compile(r'#\s*pragma\s+warning|\bSuppressMessage\b|<\s*(?:NoWarn|WarningsNotAsErrors)\b'),
}
hits = {name: [] for name in patterns}
scan = [p for p in tracked if p.endswith(('.cs', '.csproj', '.props', '.targets'))]
for p in scan:
    text = read_snapshot(p).decode('utf-8-sig')
    for line_no, line in enumerate(text.splitlines(), 1):
        for name, regex in patterns.items():
            if regex.search(line):
                hits[name].append(f'{p}:{line_no}: {line.strip()}')
for name, entries in hits.items():
    print(f'Literal scan {name}: {len(entries)} hit(s)')
    for entry in entries:
        print('  ', entry)
print('Literal search cannot establish runtime test execution, exhaustiveness, or absence of other skip/suppression mechanisms.')
print('Static inspection completed; dotnet, SDK/host, rendering, normative and security audits were not run here.')
