#!/usr/bin/env python3
"""Halodrop (formerly Morph): renames Filee to Halodrop in the interface texts of every language (src/Filee.App/Assets/i18n/*.json).

Run after merging a Filee update: only values change, never keys or the file's layout. "filee.sh" (Filee's website)
is kept. Turkish suffixes follow Halodrop's vowel harmony (Filee'nin -> Halodrop'un).
"""
import glob, json, re, sys

TURKISH = {"Filee'nin": "Halodrop'un", "Filee'yi": "Halodrop'u", "Filee'ye": "Halodrop'a", "Filee'de": "Halodrop'ta",
           "Filee'den": "Halodrop'tan", "Filee'yle": "Halodrop'la", "Filee'ı": "Halodrop'u", "Filee'a": "Halodrop'a"}
LINE = re.compile(r'^(\s*"(?:[^"\\]|\\.)*"\s*:\s*)(".*?)(\r?\n?)$', re.S)
KEEP_KEYS = {"about.based_on"}   # the credit to Filee says Filee on purpose


def main(root="src/Filee.App/Assets/i18n"):
    changed = 0
    for path in sorted(glob.glob(f"{root}/*.json")):
        out = []
        for line in open(path, encoding="utf-8", newline=""):
            m = LINE.match(line)
            if m and json.loads(m.group(1).strip().rstrip(":").strip()) not in KEEP_KEYS:
                key, value, nl = m.groups()
                before = value
                if path.endswith("/tr.json"):
                    for a, b in TURKISH.items():
                        value = value.replace(a, b).replace(a.replace("'", "’"), b.replace("'", "’"))
                value = re.sub(r"Filee(?!\.sh)", "Halodrop", value)
                value = re.sub(r"(?<![\w/.])filee(?![\w.-]*\.sh)(?=\b)", "halodrop", value)
                changed += value != before
                line = key + value + nl
            out.append(line)
        open(path, "w", encoding="utf-8", newline="").write("".join(out))
        json.load(open(path, encoding="utf-8"))
    print(f"{changed} text(s) renamed")


if __name__ == "__main__":
    main(*sys.argv[1:])
