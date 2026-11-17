#!/usr/bin/env python3
"""Morph: renames Filee to Morph in the installers and the build scripts (installer/, build/).

Kept: project paths and names (src/Filee.App, Filee.App.csproj, Filee.slnx, ...), the Explorer menu's source files
(FileeExplorerMenu.c/.rc/.def), Filee's website and repositories, and this folder's own tools.
"""
import os, re, sys

SHIELD = [
    r"src[/\\]Filee\.[A-Za-z.]+", r"Filee\.(App|Core|Engines|Cli|Setup|Platform(\.[A-Za-z]+)?|ExplorerMenu)(\.csproj|\.Tests)?\b",
    r"Filee\.slnx", r"FileeExplorerMenu\.(c|rc|def)\b", r"tests[/\\]Filee\.[A-Za-z.]+",
    r"https?://(dl\.)?filee\.sh[^\s\"'<>)]*", r"filee\.sh\b(?!-)", r"KnifeLemon/Filee[A-Za-z-]*", r"github\.com/KnifeLemon[^\s\"'<>)]*",
]
SPECIAL = [("filee-donut@filee.sh", "morph-donut@admknight.github.io"), ("com.filee.app", "app.morph"),
           ("filee-cli", "morph-cli"), ("FileeExplorerMenu.dll", "MorphExplorerMenu.dll"),
           ("FileeExplorerMenu.msix", "MorphExplorerMenu.msix")]
SKIP_DIRS = {"morph", "obj", "bin"}
TEXT = (".sh", ".txt", ".ps1", ".py", ".js", ".json", ".lua", ".md", ".conf", ".plist", ".strings", ".desktop", ".toml", ".yml")


def rename(text):
    for a, b in SPECIAL:
        text = text.replace(a, b)
    keep = []
    def hide(m):
        keep.append(m.group(0))
        return f"\u0000{len(keep) - 1}\u0000"
    for pattern in SHIELD:
        text = re.sub(pattern, hide, text)
    text = text.replace("Filee", "Morph").replace("filee", "morph").replace("FILEE", "MORPH")
    return re.sub("\u0000(\\d+)\u0000", lambda m: keep[int(m.group(1))], text)


def main(*roots):
    changed = 0
    for root0 in roots or ("installer", "build"):
        for root, dirs, files in os.walk(root0):
            dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
            for name in files:
                if not name.endswith(TEXT):
                    continue
                path = os.path.join(root, name)
                old = open(path, encoding="utf-8", newline="").read()
                new = rename(old)
                if new != old:
                    open(path, "w", encoding="utf-8", newline="").write(new)
                    changed += 1
    print(f"{changed} file(s) renamed")


if __name__ == "__main__":
    main(*sys.argv[1:])
