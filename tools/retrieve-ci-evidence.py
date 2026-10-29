#!/usr/bin/env python3
"""Reconstruct our GitHub Actions evidence from checks annotations; verify before extraction.

Requires the authenticated GitHub CLI. It never follows artifact download redirects and
never writes authentication data. Existing destination directories are not overwritten.
"""
import argparse
import base64
import hashlib
import io
import json
from pathlib import Path
import re
import subprocess
import tarfile


def api(path):
    result = subprocess.run(['gh', 'api', path], check=True, stdout=subprocess.PIPE)
    return json.loads(result.stdout)


def retrieve(repository, run_id, destination):
    if not re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', repository):
        raise ValueError('Invalid repository name')
    run = api(f'repos/{repository}/actions/runs/{run_id}')
    jobs = api(f'repos/{repository}/actions/runs/{run_id}/jobs?per_page=100')['jobs']
    if destination.exists():
        raise FileExistsError(f'Destination already exists: {destination}')
    destination.mkdir(parents=True, mode=0o700)
    manifest = {'repository': repository, 'run_id': run_id, 'run_url': run['html_url'],
                'head_sha': run['head_sha'], 'conclusion': run['conclusion'], 'jobs': {}}
    for job in jobs:
        name = job['name']
        if name not in ('.NET baseline', '.NET current'):
            continue
        label = name.rsplit(' ', 1)[1]
        entries = api(f'repos/{repository}/check-runs/{job["id"]}/annotations?per_page=100')
        hashes = [item['message'] for item in entries if item['title'] == 'EVACUATOR_SHA256']
        parts = sorted((item for item in entries if item['title'].startswith('EVACUATOR_PART_')),
                       key=lambda item: item['title'])
        if len(hashes) != 1 or not parts:
            raise ValueError(f'{name}: missing checksum or parts')
        for index, item in enumerate(parts, 1):
            if item['title'] != f'EVACUATOR_PART_{index:03d}':
                raise ValueError(f'{name}: non-contiguous parts')
        data = base64.b64decode(''.join(item['message'] for item in parts), validate=True)
        digest = hashlib.sha256(data).hexdigest()
        if digest != hashes[0]:
            raise ValueError(f'{name}: checksum mismatch')
        archive = destination / f'{label}-raw.tar.gz'
        archive.write_bytes(data)
        target_root = destination / label
        target_root.mkdir()
        with tarfile.open(fileobj=io.BytesIO(data), mode='r:gz') as tar:
            for member in tar.getmembers():
                relative = Path(member.name)
                if relative.is_absolute() or '..' in relative.parts:
                    raise ValueError(f'{name}: unsafe archive member {member.name}')
                target = target_root / relative
                if member.isdir():
                    target.mkdir(parents=True, exist_ok=True)
                elif member.isfile():
                    target.parent.mkdir(parents=True, exist_ok=True)
                    with tar.extractfile(member) as source, target.open('xb') as dest:
                        dest.write(source.read())
                else:
                    raise ValueError(f'{name}: unsupported archive member {member.name}')
        manifest['jobs'][label] = {'job_id': job['id'], 'conclusion': job['conclusion'],
                                   'parts': len(parts), 'archive_sha256': digest}
        print(f'{label}: {len(parts)} parts; {len(data)} bytes; SHA-256 {digest}; verified')
    if not manifest['jobs']:
        raise ValueError('No matching CI jobs with evidence')
    (destination / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', required=True, help='owner/repository')
    parser.add_argument('--run', type=int, required=True, help='GitHub Actions run ID')
    parser.add_argument('--destination', type=Path, required=True)
    args = parser.parse_args()
    if args.run <= 0:
        parser.error('--run must be positive')
    retrieve(args.repo, args.run, args.destination)


if __name__ == '__main__':
    main()
