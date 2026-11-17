#!/usr/bin/env python3
"""Morph: renames Filee to Morph in the interface texts of every language (src/Filee.App/Assets/i18n/*.json).

Run after merging a Filee update: only values change, never keys or the file's layout. "filee.sh" (Filee's website)
is kept. Turkish suffixes follow Morph's vowel harmony (Filee'nin -> Morph'un).
"""
import glob, json, re, sys

TURKISH = {"Filee'nin": "Morph'un", "Filee'yi": "Morph'u", "Filee'ye": "Morph'a", "Filee'de": "Morph'ta",
           "Filee'den": "Morph'tan", "Filee'yle": "Morph'la", "Filee'ı": "Morph'u", "Filee'a": "Morph'a"}
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
                        value = value.replace(a, b)
                value = re.sub(r"Filee(?!\.sh)", "Morph", value)
                value = re.sub(r"(?<![\w/.])filee(?![\w.-]*\.sh)(?=\b)", "morph", value)
                changed += value != before
                line = key + value + nl
            out.append(line)
        open(path, "w", encoding="utf-8", newline="").write("".join(out))
        json.load(open(path, encoding="utf-8"))
    print(f"{changed} text(s) renamed")


if __name__ == "__main__":
    main(*sys.argv[1:])
