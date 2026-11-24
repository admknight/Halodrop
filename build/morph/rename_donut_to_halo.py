#!/usr/bin/env python3
"""Halodrop calls Filee's "donut" the halo. Renames the word in every language's texts.

Filee's own word for the ring (donut, Donut, ドーナツ, 도넛, ڈونٹ, …) becomes the halo word of that language. Languages
whose texts already call it a ring or circular menu (Ringmenü, pierścień, halka, цагираг, …) keep that word; only the
few places that say "donut" there take the ring word too. Keys and settings ids keep "donut" (they aren't shown).

Run on the i18n files (rebrand_after_merge.py runs it for you, on merged lines only):
    python3 build/morph/rename_donut_to_halo.py src/Filee.App/Assets/i18n
"""

import json
import re
import sys
from pathlib import Path

EXTENSION = [(r"Halodrop donut at the cursor", "Halodrop at the cursor"), (r"Filee donut at the cursor", "Halodrop at the cursor")]

W = r"(?<![\w-])"   # word start (not inside another word or a key)
E = r"(?![\w-])"    # word end

RULES = {
    "en": [(W + r"Donuts" + E, "Halos"), (W + r"donuts" + E, "halos"), (W + r"Donut" + E, "Halo"), (W + r"donut" + E, "halo")],
    "de": [(W + r"Donuts" + E, "Halos"), (W + r"Donut" + E, "Halo")],
    "fr": [(W + r"[Dd]onuts" + E, "halos"), (W + r"[Dd]onut" + E, "halo")],
    "pt-BR": [(W + r"[Dd]onuts" + E, "halos"), (W + r"[Dd]onut" + E, "halo")],
    "pt-PT": [(W + r"[Dd]onuts" + E, "halos"), (W + r"[Dd]onut" + E, "halo")],
    "id": [(W + r"[Dd]onut" + E, "halo"), (W + r"[Dd]onat" + E, "halo")],
    "es": [
        (W + r"La dona" + E, "El halo"), (W + r"la dona" + E, "el halo"), (W + r"Las donas" + E, "Los halos"),
        (W + r"las donas" + E, "los halos"), (W + r"una dona" + E, "un halo"), (W + r"Mis donas" + E, "Mis halos"),
        (W + r"donas" + E, "halos"), (W + r"dona" + E, "halo"), (W + r"[Dd]onuts" + E, "halos"), (W + r"[Dd]onut" + E, "halo"),
    ],
    "it": [
        (W + r"Il donut" + E, "L'alone"), (W + r"il donut" + E, "l'alone"), (W + r"del donut" + E, "dell'alone"),
        (W + r"dal donut" + E, "dall'alone"), (W + r"nel donut" + E, "nell'alone"), (W + r"sul donut" + E, "sull'alone"),
        (W + r"al donut" + E, "all'alone"), (W + r"I donut" + E, "Gli aloni"), (W + r"i donut" + E, "gli aloni"),
        (W + r"dei donut" + E, "degli aloni"), (W + r"ai donut" + E, "agli aloni"), (W + r"nei donut" + E, "negli aloni"),
        (W + r"sui donut" + E, "sugli aloni"), (W + r"dai donut" + E, "dagli aloni"), (W + r"barre donut" + E, "barre ad alone"),
        (W + r"barra donut" + E, "barra ad alone"), (W + r"Barra donut" + E, "Barra ad alone"),
        (W + r"Le mie ciambelle" + E, "I miei aloni"), (W + r"della ciambella" + E, "dell'alone"),
        (W + r"alla ciambella" + E, "all'alone"), (W + r"nella ciambella" + E, "nell'alone"), (W + r"sulla ciambella" + E, "sull'alone"),
        (W + r"dalla ciambella" + E, "dall'alone"), (W + r"questa ciambella" + E, "questo alone"), (W + r"La ciambella" + E, "L'alone"), (W + r"la ciambella" + E, "l'alone"),
        (W + r"Le ciambelle" + E, "Gli aloni"), (W + r"le ciambelle" + E, "gli aloni"), (W + r"delle ciambelle" + E, "degli aloni"),
        (W + r"una ciambella" + E, "un alone"), (W + r"ciambelle" + E, "aloni"), (W + r"ciambella" + E, "alone"),
        (W + r"Donut" + E, "Alone"), (W + r"donut" + E, "alone"),
        # alone is masculine where ciambella was feminine
        (r"(aloni[^.“\"]*?) salvate", r"\1 salvati"), (r"L'intera alone", "L'intero alone"),
    ],
    "ru": [(r"Пончик", "Ореол"), (r"пончик", "ореол")],   # пончика → ореола, пончиком → ореолом: same endings
    "mn": [(r"Пончик", "Цагираг"), (r"пончик", "цагираг")],
    "pl": [(W + r"pączek" + E, "pierścień"), (W + r"Pączek" + E, "Pierścień"), (W + r"donuta" + E, "pierścienia"),
           (W + r"[Dd]onut" + E, "pierścień")],
    "tr": [(W + r"simit" + E, "halka"), (W + r"[Dd]onut" + E, "halka")],
    "vi": [(r"chiếc bánh donut", "vòng Halo"), (r"bánh donut", "vòng Halo"), (W + r"[Dd]onut" + E, "vòng Halo")],
    "zh-CN": [(r"甜甜圈", "光环")],
    "zh-TW": [(r"甜甜圈", "光環")],
    "ja": [(r"ドーナツ", "ヘイロー")],
    "ko": [
        # 도넛 ends in a consonant, 헤일로 in a vowel: the particles change with it.
        (r"도넛이에요", "헤일로예요"), (r"도넛이나", "헤일로나"), (r"도넛이", "헤일로가"), (r"도넛을", "헤일로를"),
        (r"도넛은", "헤일로는"), (r"도넛으로", "헤일로로"), (r"도넛과", "헤일로와"), (r"도넛", "헤일로"),
    ],
    # Bengali also wrote Filee's name in its own script (ফাইলি); Halodrop stays in Latin letters, so its endings take a hyphen.
    "bn-BD": [
        (r"ফাইলি-?র(?![\u0980-\u09FF])", "Halodrop-এর"), (r"ফাইলি-?কে", "Halodrop-কে"), (r"ফাইলি-?তে", "Halodrop-এ"), (r"ফাইলি", "Halodrop"),
        (r"ডোনাটের", "হেলোর"), (r"ডোনাটে", "হেলোতে"), (r"ডোনাট", "হেলো"),
    ],
    "th": [(r"โดนัท", "ฮาโล")],
    "hi": [(r"डोनट्स", "हेलो"), (r"डोनट", "हेलो")],
    "ne": [(r"डोनटहरू", "हेलोहरू"), (r"डोनट", "हेलो")],
    "fa": [(r"دونات", "هاله")],
    "ar": [(r"الدونات", "الهالة"), (r"دونات", "هالة")],
    # ہالہ takes its oblique form (ہالے) before a postposition, like any noun in ہ.
    "ur": [(r"ڈونٹس", "ہالے"), (r"ڈونٹ(?=\s+(?:کا|کی|کے|کو|پر|میں|سے|نے|تک)(?!\w))", "ہالے"), (r"ڈونٹ", "ہالہ")],
}


def rename(language: str, text: str) -> str:
    for pattern, replacement in EXTENSION + RULES.get(language, []):
        text = re.sub(pattern, replacement, text)
    return text


def rename_file(path: Path) -> int:
    language = path.stem
    lines = path.read_text(encoding="utf-8").split("\n")
    changed = 0
    for i, line in enumerate(lines):
        m = re.match(r'(\s*"[^"]+":\s*)(".*")(,?\s*)$', line)
        if not m:
            continue
        value = json.loads(m.group(2))
        renamed = rename(language, value)
        if renamed != value:
            lines[i] = m.group(1) + json.dumps(renamed, ensure_ascii=False) + m.group(3)
            changed += 1
    path.write_text("\n".join(lines), encoding="utf-8")
    return changed


if __name__ == "__main__":
    folder = Path(sys.argv[1] if len(sys.argv) > 1 else "src/Filee.App/Assets/i18n")
    total = sum(rename_file(p) for p in sorted(folder.glob("*.json")))
    print(f"renamed the donut in {total} text(s)")
