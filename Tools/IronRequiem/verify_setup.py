"""Verify setup against the original snapshot without modifying Unity assets."""
import argparse
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path


def read_json(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def hash_file(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest().upper()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--baseline', type=Path)
    args = parser.parse_args()
    root = args.root.resolve()
    baseline_path = args.baseline or sorted((root / 'Tools/IronRequiem/Backups').glob('*/baseline.json'))[-1]
    baseline = read_json(baseline_path)
    def compare(entry):
        path = root / entry['path']
        if not path.exists():
            return {'path': entry['path'], 'status': 'missing'}
        actual = hash_file(path)
        if actual != entry['sha256']:
            return {'path': entry['path'], 'status': 'changed', 'before': entry['sha256'], 'after': actual}
        return None
    with ThreadPoolExecutor(max_workers=8) as pool:
        differences = [item for item in pool.map(compare, baseline) if item]
    before_manifest = read_json(baseline_path.parent / 'Packages/manifest.json')['dependencies']
    manifest = read_json(root / 'Packages/manifest.json')['dependencies']
    before_lock = read_json(baseline_path.parent / 'Packages/packages-lock.json')['dependencies']
    lock = read_json(root / 'Packages/packages-lock.json')['dependencies']
    audit_path = root / 'Tools/IronRequiem/Reports/unity-audit.json'
    audit = read_json(audit_path) if audit_path.exists() else {}
    added = {name: version for name, version in manifest.items() if name not in before_manifest}
    changed_existing = {name: {'before': version, 'after': manifest.get(name)}
                        for name, version in before_manifest.items() if manifest.get(name) != version}
    registered = {p['name']: p['version'] for p in audit.get('packages', [])}
    added_lock = {name: info['version'] for name, info in lock.items() if name not in before_lock}
    changed_lock = {name: {'before': info['version'], 'after': lock.get(name, {}).get('version')}
                    for name, info in before_lock.items() if lock.get(name, {}).get('version') != info['version']}
    checks = {
        'original_assets_unchanged': not any(d['path'].startswith('Assets/') for d in differences),
        'no_original_files_deleted': not any(d['status'] == 'missing' for d in differences),
        'editor_version_preserved': audit.get('unity') == '6000.5.5f1',
        'urp_active': 'UniversalRenderPipelineAsset' in audit.get('pipeline', ''),
        'existing_direct_dependencies_preserved': not changed_existing,
        'existing_locked_versions_preserved': not changed_lock,
        'new_packages_stable': all('-' not in version for version in added_lock.values()),
        'new_packages_resolved_and_registered': bool(added) and all(
            lock.get(name, {}).get('version') == version == registered.get(name) for name, version in added.items()),
        'no_captured_compilation_errors': 'compilationErrors' in audit and not audit['compilationErrors'],
        'main_scene_clean_and_no_missing_scripts': any(s['path'] == 'Assets/FPS/Scenes/MainScene.unity' and
            not s['dirty'] and s['missingScripts'] == 0 for s in audit.get('scenes', [])),
        'dotween_loaded': audit.get('dotweenVersion') == '1.3.045',
        'dotween_modules_compiled': audit.get('dotweenModulesAssembly', False),
        'dotween_settings_created': bool(audit.get('dotweenSettingsPath')) and (root / audit['dotweenSettingsPath']).is_file(),
    }
    report = {
        'utc': datetime.now(timezone.utc).isoformat(), 'baseline': str(baseline_path),
        'original_file_count': len(baseline), 'original_asset_file_count': sum(e['path'].startswith('Assets/') for e in baseline),
        'differences': differences, 'added_direct_dependencies': added, 'added_locked_dependencies': added_lock,
        'changed_existing_dependencies': changed_existing, 'changed_existing_locked_versions': changed_lock,
        'checks': checks, 'all_checks_pass': all(checks.values()),
        'limits': 'Verifies imported/compiled state and file preservation; does not simulate player controls or gameplay.'}
    output = root / 'Tools/IronRequiem/Reports/setup-verification.json'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf-8')
    print(json.dumps(report, indent=2, ensure_ascii=False))
    return 0 if report['all_checks_pass'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
