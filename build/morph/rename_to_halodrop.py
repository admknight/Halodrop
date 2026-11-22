#!/usr/bin/env python3
"""Halodrop: the one-time rename of Morph to Halodrop (0.8.0).

What people see, the program's files and its folders say Halodrop. Kept on purpose (internal, or stored by 0.7.0):
  - code identifiers and comments ("Morph change:" marks the fork's changes, MorphBrandingTests, Filee.Core.Morph...)
  - all-caps environment variables (MORPH_DATA_DIR, MORPH_WORKER_TESTS...)
  - build metadata keys read by the app (MorphRelease, MorphExpected*, MorphUpdatePublicKey, MorphSetupPublish)
  - stored ids (morph-default, template.morph_default, morph-example-group, morph_token...)
  - the Explorer menu package's identity (Morph.ExplorerMenu, CN=Morph: its family name is a hash of them),
    MorphExplorerMenu.dll/.msix, MorphCdr, the icon files (morph.ico/.png/.svg), the portable marker morph.portable,
    Linux and macOS ids (morph.desktop, app.morph), temporary-file suffixes (.morph-…)

Usage: python3 build/morph/rename_to_halodrop.py
"""
import json, os, re, sys

# A standalone word: not part of an identifier, file name, path, key or id.
WORD = re.compile(r'(?<![\w.\-/:#=;@\\])Morph(?![\w.\-:/#\\])')
WORD_LOWER = re.compile(r'(?<![\w.\-/:#=;@\\$%])morph(?![\w.\-:/#_\\])')
# Names of files and folders that change with the program (whole matches only).
FILES = [
    ('avares://Morph.Setup/', 'avares://Halodrop.Setup/'), ('avares://Morph/', 'avares://Halodrop/'),
    ('Morph.Setup.exe', 'Halodrop.Setup.exe'), ('"Morph.Setup"', '"Halodrop.Setup"'), ("'Morph.Setup'", "'Halodrop.Setup'"),
    ('Morph.exe', 'Halodrop.exe'), ('Morph.lnk', 'Halodrop.lnk'), ('morph-cli', 'halodrop-cli'), ('morph.cmd', 'halodrop.cmd'),
    ('Morph-practice', 'Halodrop-practice'), ('Morph-Setup', 'Halodrop-Setup'), ('Morph-Uninstall', 'Halodrop-Uninstall'),
    ('^Morph-(', '^Halodrop-('), ('Morph-<version>', 'Halodrop-<version>'), ('Morph-$version', 'Halodrop-$version'),
    ('Morph-$Version', 'Halodrop-$Version'), ('Morph-${version}', 'Halodrop-${version}'), ('Morph-*', 'Halodrop-*'),
    ('Morph-{', 'Halodrop-{'), ('Morph-0.', 'Halodrop-0.'), ('morph-{DateTime', 'halodrop-{DateTime'), ('morph-*.log', 'halodrop-*.log'),
    ('morph-msiexec', 'halodrop-msiexec'), ('Morph/{', 'Halodrop/{'), ('fonts:Morph', 'fonts:Halodrop'),
    ('admknight/Morph', 'admknight/Halodrop'), ('<Application>Morph<', '<Application>Halodrop<'), ('Name=Morph', 'Name=Halodrop'),
    ('Morph Setup', 'Halodrop Setup'),
]
KEEP = re.compile(r'Morph change|MorphBranding|MorphDeveloper|MorphMigrations|MorphSchema')


def rename_text(text):
    for a, b in FILES:
        text = text.replace(a, b)
    text = WORD.sub('Halodrop', text)
    return WORD_LOWER.sub('halodrop', text)


def csharp(src):
    """Renames inside string literals only (regular, verbatim, interpolated, raw); code and comments stay."""
    out, i, n = [], 0, len(src)
    while i < n:
        c = src[i]
        if src.startswith('//', i):
            j = src.find('\n', i); j = n if j < 0 else j
            out.append(src[i:j]); i = j; continue
        if src.startswith('/*', i):
            j = src.find('*/', i + 2); j = n if j < 0 else j + 2
            out.append(src[i:j]); i = j; continue
        if c == "'":
            m = re.match(r"'(?:\\.|[^'\\\n])*'", src[i:])
            if m:
                out.append(m.group(0)); i += len(m.group(0)); continue
        m = re.match(r'(\$*)(@?)("""+)', src[i:])
        if m:
            quotes = m.group(3)
            start = i + len(m.group(0))
            j = src.find(quotes, start); j = n if j < 0 else j
            out.append(m.group(0) + rename_text(src[start:j]) + src[j:j + len(quotes)]); i = j + len(quotes); continue
        m = re.match(r'(\$*@|@\$*)"', src[i:])
        if m:   # verbatim: "" is a quote
            start = i + len(m.group(0)); j = start
            while j < n and not (src[j] == '"' and src[j + 1:j + 2] != '"'):
                j += 2 if src[j] == '"' else 1
            out.append(m.group(0) + rename_text(src[start:j]) + '"'); i = j + 1; continue
        m = re.match(r'(\$*)"', src[i:])
        if m:
            start = i + len(m.group(0)); j = start
            while j < n and src[j] != '"' and src[j] != '\n':
                j += 2 if src[j] == '\\' else 1
            out.append(m.group(0) + rename_text(src[start:j]) + src[j:j + 1]); i = j + 1; continue
        out.append(c); i += 1
    return ''.join(out)


def markup(src):
    """axaml/xml: everything but comments."""
    parts = re.split(r'(<!--.*?-->)', src, flags=re.S)
    return ''.join(p if p.startswith('<!--') else rename_text(p) for p in parts)


def text_file(src):
    """Scripts, workflows, docs: lines that aren't comments, and comments too except "Morph change" marks."""
    return ''.join(line if KEEP.search(line) else rename_text(line) for line in src.splitlines(keepends=True))


def i18n(root):
    line_re = re.compile(r'^(\s*"(?:[^"\\]|\\.)*"\s*:\s*)(".*?)(\r?\n?)$', re.S)
    for name in sorted(os.listdir(root)):
        path = os.path.join(root, name)
        out = []
        for line in open(path, encoding='utf-8', newline=''):
            m = line_re.match(line)
            if m:
                line = m.group(1) + rename_text(m.group(2)) + m.group(3)
            out.append(line)
        new = ''.join(out)
        json.loads(new)
        open(path, 'w', encoding='utf-8', newline='').write(new)


def walk(roots, exts, fn):
    changed = 0
    for root0 in roots:
        for root, dirs, files in os.walk(root0):
            dirs[:] = [d for d in dirs if d not in {'obj', 'bin', '.cache', 'morph', 'node_modules', 'i18n'}]
            for f in files:
                if not f.endswith(exts):
                    continue
                p = os.path.join(root, f)
                old = open(p, encoding='utf-8-sig' if f.endswith('.cs') else 'utf-8', newline='').read()
                bom = open(p, 'rb').read(3) == b'\xef\xbb\xbf'
                new = fn(old)
                if new != old:
                    with open(p, 'w', encoding='utf-8-sig' if bom else 'utf-8', newline='') as fh:
                        fh.write(new)
                    changed += 1
    return changed


def main():
    i18n('src/Filee.App/Assets/i18n')
    n = walk(['src', 'tests'], ('.cs',), csharp)
    n += walk(['src', 'tests'], ('.axaml', '.manifest', '.plist', '.xml'), markup)
    n += walk(['build', 'installer', '.github', 'docs'], ('.ps1', '.sh', '.py', '.yml', '.json', '.md', '.txt', '.plist',
                                                           '.desktop', '.strings', '.js', '.toml', '.lua', '.conf'), text_file)
    print(f'{n} files renamed')


if __name__ == '__main__':
    main()
