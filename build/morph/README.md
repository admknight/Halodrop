# Morph's tools for taking in Filee updates

Morph is built on [Filee](https://github.com/KnifeLemon/Filee) (MIT). The repository keeps Filee's history, so a
Filee release merges in like any branch. Internal names stay `Filee.*` so merges stay small; what people see says
Morph. Every place Morph differs from Filee is marked `Morph change:` in the code.

## Taking in a Filee release

```bash
git remote add upstream https://github.com/KnifeLemon/Filee.git   # once
git fetch upstream
git switch -c filee-update origin/main
git merge upstream/main                                             # resolve conflicts at the "Morph change:" marks
python3 build/morph/rebrand_i18n.py                                 # new texts in every language
python3 build/morph/rebrand_code_literals.py src tests              # new "Filee" strings in code
python3 build/morph/rebrand_code_strings.py src tests               # ... in $"…" and """…""" strings
dotnet build Filee.slnx && dotnet test Filee.slnx                   # MorphBrandingTests catches what slipped through
```

The code scripts keep type and resource names (`Filee.App.…`), Filee's website (`filee.sh`) and comments. Check
their changes with `git diff` before committing: a new text key or stored id that contains "filee" must keep its
name (the branding test lists keys that no longer exist).
