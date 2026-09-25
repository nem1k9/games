#!/usr/bin/env python3
"""Create missing Unity .meta files under Assets/ with deterministic GUIDs.

Unity needs a .meta next to every asset/folder. We generate them ourselves so that
GUIDs are stable across machines (derived from the asset path) and so that the scene
and build settings can reference assets by GUID before Unity ever opens the project.

Usage: python3 tools/gen_meta.py [--check]
"""
import hashlib
import os
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
ASSETS = os.path.join(ROOT, 'Assets')


def guid_for(rel_path: str) -> str:
    return hashlib.md5(('sneaky-gnomes:' + rel_path.replace('\\', '/')).encode()).hexdigest()


def meta_for(rel: str, is_dir: bool) -> str:
    g = guid_for(rel)
    head = f'fileFormatVersion: 2\nguid: {g}\n'
    if is_dir:
        return head + 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    ext = os.path.splitext(rel)[1].lower()
    if ext == '.cs':
        return head + ('MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n'
                       '  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    if ext == '.asmdef':
        return head + 'AssemblyDefinitionImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    if ext == '.dll':
        return head + ('PluginImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  iconMap: {}\n  executionOrder: {}\n'
                       '  defineConstraints: []\n  isPreloaded: 0\n  isOverridable: 0\n  isExplicitlyReferenced: 0\n'
                       '  validateReferences: 1\n  platformData:\n  - first:\n      Any: \n    second:\n      enabled: 1\n'
                       '      settings: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    if ext in ('.json', '.txt', '.bytes', '.md'):
        return head + 'TextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    if ext == '.mat':
        return head + 'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    if ext == '.unity':
        return head + 'DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    # FBX / PNG / WAV: minimal meta, our AssetPostprocessor configures the importer.
    return head


def main() -> int:
    check = '--check' in sys.argv
    missing = []
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        dirnames[:] = [d for d in dirnames if not d.startswith('.')]
        rel_dir = os.path.relpath(dirpath, ROOT)
        entries = [(d, True) for d in dirnames] + [(f, False) for f in filenames if not f.endswith('.meta') and not f.startswith('.')]
        for name, is_dir in entries:
            rel = os.path.join(rel_dir, name).replace('\\', '/')
            meta_path = os.path.join(ROOT, rel + '.meta')
            if not os.path.exists(meta_path):
                missing.append(rel)
                if not check:
                    with open(meta_path, 'w', newline='\n') as f:
                        f.write(meta_for(rel, is_dir))
    # remove orphan metas
    orphans = []
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        for f in filenames:
            if f.endswith('.meta'):
                target = os.path.join(dirpath, f[:-5])
                if not os.path.exists(target):
                    orphans.append(os.path.relpath(target, ROOT))
                    if not check:
                        os.remove(os.path.join(dirpath, f))
    if check and (missing or orphans):
        print('Missing metas:', missing)
        print('Orphan metas:', orphans)
        return 1
    print(f'metas created: {len(missing)}, orphans removed: {len(orphans)}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
