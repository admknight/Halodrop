# Halodrop's tools for taking in Filee updates

Halodrop (called Morph up to 0.7.0) is built on [Filee](https://github.com/KnifeLemon/Filee) (MIT). The repository
keeps Filee's history, so a Filee release merges in like any branch. Internal names stay `Filee.*` (and some `Morph*`)
so merges stay small; what people see says Halodrop. Every place Halodrop differs from Filee is marked
`Morph change:` or `Halodrop change:` in the code.

## Taking in a Filee release

```bash
git remote add upstream https://github.com/KnifeLemon/Filee.git   # once
git fetch upstream
git switch -c filee-sync origin/main
git merge upstream/main                                             # resolve conflicts at the "change:" marks
python3 build/morph/rebrand_after_merge.py --since origin/main      # Filee -> Halodrop, on merged lines only
git diff                                                            # check the renames
dotnet build Filee.slnx && dotnet test Filee.slnx                   # MorphBrandingTests catches what slipped through
```

`rebrand_after_merge.py` runs the rename scripts (texts in every language, code strings, interpolated and raw
strings, installers and build scripts, and Filee's "donut" to Halodrop's halo in every language) and then puts back every line they changed that the merge didn't bring in. So
only what Filee added or changed is renamed; Halodrop's own lines, including internal names kept on purpose
(`Filee.slnx`, `preset.filee_connect`, test fonts, the Explorer menu package's identity), stay exactly as they are.
Run on a tree with nothing merged it changes nothing. Don't run the four scripts on their own: they rename the whole
tree.

The scripts keep type and resource names (`Filee.App.…`), Filee's website (`filee.sh`) and comments. A new text key
or stored id that contains "filee" must keep its name (the branding test lists keys that no longer exist).

### Files Halodrop leaves out

A merge may bring these back (as a "deleted by us" conflict); keep them deleted:

- `docs/readme/` – Filee's translated READMEs. Halodrop has one README, in English.
- `tests/Filee.App.Tests/UpdateServiceMirrorTests.cs` – Filee's download mirror, which Halodrop never uses
  (`MorphUpdateMirrorTests` checks that instead).

### Text Halodrop adds

New keys Halodrop adds to every language (`guide.*`, `resume.*`, `error.engine_crashed`, `theme.style.Futuristic`…) are in
all 24 files; when Filee adds a language, copy them from `en.json` and translate them too.

### Filee's "What's new" slides

Filee's decks (`Assets/WhatsNew/2.x`) stay in the tree so merges stay small, but the app loads only decks whose
`slides.json` says `"app": "halodrop"`. A new Filee deck therefore never shows; to show one of its features, add a slide
to Halodrop's own deck and record it with `WhatsNewMedia` (it renders the clips headlessly).

### Before anything goes to main

1. `build/morph/check-before-push.sh` must end with "ALL CHECKS PASSED" (build, format, tests that still quote a
   changed text, Core and CLI tests; `--full` adds the app's UI tests). Failures that only happen in a Linux sandbox
   are listed in `sandbox-known-failures.txt`.
2. `git push origin HEAD:check/<topic>` runs the full CI on Windows. Only when it passes: `git push origin HEAD:main`,
   then delete the check branch.

### Linux and macOS in releases

The repository variable `LINUX_MACOS` (Settings → Secrets and variables → Actions → Variables) set to `true` makes the
release build and test the Linux (x64, ARM64) and macOS (Apple Silicon, Intel) packages with `portable.yml` and publish
them with the Windows files; every SHA256SUMS file gets its `.sig`. The release then takes up to about three hours
(Intel Macs are slow) and stops if one of them fails. To try the packages without releasing, run "Linux and macOS
packages" from Actions on a `check/` branch. Leave `OTHER_PLATFORMS` unset: it switches to Filee's draft release, which
needs Filee's signing PC. The macOS packages are ad-hoc signed and not notarized (no Apple Developer ID).
