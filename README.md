<div align="center">

<img src="https://capsule-render.vercel.app/api?type=waving&color=gradient&customColorList=12,14,18&height=185&section=header&text=Morph&fontSize=62&fontColor=ffffff&animation=twinkling&fontAlignY=35&desc=Drag%2C%20drop%2C%20converted.&descSize=18&descAlignY=57" width="100%" alt="Morph: Drag, drop, converted.">

<img src="src/Filee.App/Assets/Icons/morph.png" width="96" alt="Morph icon">

**Your files. Your format. Right at your cursor.**

[![CI](https://github.com/admknight/Morph/actions/workflows/ci.yml/badge.svg)](https://github.com/admknight/Morph/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-43A7FF.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/Windows-10%2F11%20x64%20%7C%20ARM64-2376d1.svg)](#editions)
[![Platform: Linux](https://img.shields.io/badge/Linux-x64%20%7C%20ARM64-2376d1.svg)](#editions)
[![Platform: macOS](https://img.shields.io/badge/macOS-13%2B-2376d1.svg)](#editions)
[![Languages](https://img.shields.io/badge/languages-24%20incl.%20Urdu-43A7FF)](#make-it-yours)
[![Status: Pre-release](https://img.shields.io/badge/status-pre--release%200.7.0-43A7FF)](#editions)

[**Windows builds**](https://github.com/admknight/Morph/actions/workflows/ci.yml) · [**Features**](#features-at-a-glance) · [**Real screenshots**](#real-app-screenshots) · [**Documentation**](docs/ARCHITECTURE.md) · [**Security**](SECURITY.md) · [**Issues**](https://github.com/admknight/Morph/issues)

</div>

---

## What is Morph?

**Morph** is a free, open-source file converter you use by dragging. Hold <kbd>Ctrl</kbd> (<kbd>Option</kbd> on a Mac),
drag files from Explorer, Finder, the desktop or a browser, and a ring of choices, the **donut**, opens at your cursor.
Let go on one and the new file appears next to the original, which is never changed. Everything happens on your own
computer.

The name says what it does: files **morph** from one form into another, without opening an app.

> [!IMPORTANT]
> **Current status: 0.7.0, pre-release.** Every change is built and tested on GitHub (Windows x64 and ARM64, Linux and
> macOS packages); the newest builds are on the [Actions page](https://github.com/admknight/Morph/actions/workflows/ci.yml).
> No public release is published yet, and the Windows builds are **unsigned**, so Windows SmartScreen will warn on first
> start. Share, print, the Explorer Gallery / VS Code selection and the Google Lens upload pass their automated checks
> but haven't been tried on a physical PC yet.

## Features at a glance

| Area | What Morph offers |
| :--- | :--- |
| **Convert** | About 270 formats offline: pictures (RAW, HEIC, AVIF too), PDF, Word, Excel, PowerPoint, HWP, **CorelDRAW** (built in, no LibreOffice), e-books, archives, fonts, CAD, 3D models, audio and video |
| **Tools** | Crop, rotate, resize, compress to a size, remove a background, trace to SVG, **social sizes** (square, story, thumbnail, cropped, never squashed), **normalize volume**, cut clips, merge, OCR, metadata, upscale |
| **Hide** | Pixelate, blur or black-box parts of a picture; **Find faces** and **Find text** frame them for you to check before saving |
| **File actions** | Copy path or name, show in folder, open a terminal there, **share**, **print**, **keep on shelf** (a floating shelf to drag files out later) |
| **Picture actions** | Copy the text in a picture (OCR), **colour palette**, set as wallpaper, **search the web** for the picture |
| **Your own actions** | **Run a program** with the files: pick an installed app (*Open in Photoshop*, *Open in VS Code*) or write a command |
| **Layouts** | One donut per file type, front and back, up to 12 slices a side; **templates** to switch whole layouts |
| **Safety** | Engines run in separate workers with **memory and time limits**; a crash fails only its own file; unfinished work **resumes** after a power cut |
| **Updates** | Checked against GitHub, signature-verified, **installed while Morph isn't in use** (can be turned off) |

### Private by design

Morph needs **no account** and sends **no telemetry**. Conversions, face finding and text finding all run locally.
The only things that touch the network are ones you choose: **Search the web** sends a small copy of the picture
(without its location or camera data) to Google Lens, and that action's name says so; optional engines download once
from their official sites, each checked against a pinned SHA-256.

## Real app screenshots

These are **unedited screenshots of Morph 0.7.0**, rendered by the project's own UI tests (`tests/Filee.App.Tests`)
from the same code as the builds. They are **not mockups**. Colours, fonts and window sizes vary with your system
and the look you pick.

| Home with the guided first run | The donut: formats in front | Flip it: tools on the back |
|:---:|:---:|:---:|
| <img src="docs/media/morph-home.png" width="270" alt="Home page, step 2 of 4 of the guided first run, with a practice picture and an Open the donut button"> | <img src="docs/media/morph-donut-front.png" width="270" alt="The front of the donut for three JPGs: PNG, JPG, WEBP, GIF, PDF, Merge PDF and Trace"> | <img src="docs/media/morph-donut-back.png" width="270" alt="The back of the donut: Crop, Rotate, Cut out, Merge images, Compress image, Half size, Edit pictures and One ZIP"> |

| A PDF donut with a group open | Hide: blur names and messages | Futuristic look |
|:---:|:---:|:---:|
| <img src="docs/media/morph-donut-pdf.png" width="270" alt="The dark donut for a PDF with the Edit group opened into Crop, Left, Right, Pages, Split and Add"> | <img src="docs/media/morph-hide.png" width="270" alt="The Hide window with a name and a message blurred on a chat screenshot, and the toolbar with Find faces and Find text"> | <img src="docs/media/morph-futuristic.png" width="270" alt="The Theme page in the Futuristic look, with a cyan accent and a donut preview"> |

| Keep on shelf | Colour palette | Urdu, right to left |
|:---:|:---:|:---:|
| <img src="docs/media/morph-shelf.png" width="230" alt="The floating shelf holding three pictures with thumbnails and a PDF, with drag-all, donut and close buttons"> | <img src="docs/media/morph-palette.png" width="250" alt="The card the colour palette leaves: six swatches with their hex codes, copied"> | <img src="docs/media/morph-urdu.png" width="270" alt="The Theme page in Urdu, right to left, in a Nastaliq font"> |

<details>
<summary>More screenshots: the tool windows and the Settings pages</summary>

| Crop | Straighten a photo of a page | Cut a clip |
|:---:|:---:|:---:|
| <img src="docs/media/morph-crop.png" width="270" alt="The crop window with a frame on a landscape picture and its size"> | <img src="docs/media/morph-perspective.png" width="270" alt="Perspective crop: four corners dragged onto a tilted page"> | <img src="docs/media/morph-clip.png" width="270" alt="The clip window: a video timeline with the kept part, waveform and playhead"> |

| Annotate | A background for screenshots | Merge pictures |
|:---:|:---:|:---:|
| <img src="docs/media/morph-annotate.png" width="270" alt="The annotate window with a highlight, a box, an arrow and a label"> | <img src="docs/media/morph-background.png" width="270" alt="A screenshot set on a gradient background with padding, corners and shadow controls"> | <img src="docs/media/morph-merge-images.png" width="270" alt="Choosing the order and layout to merge pictures into one"> |

| Edit or remove metadata | Rearrange PDF pages | Choose the compression |
|:---:|:---:|:---:|
| <img src="docs/media/morph-metadata.png" width="270" alt="The metadata editor with location, camera and date fields"> | <img src="docs/media/morph-pdf-pages.png" width="270" alt="The PDF page editor with page thumbnails to reorder, rotate, delete and extract"> | <img src="docs/media/morph-compression.png" width="270" alt="Choosing the compression of a picture, with the estimated size"> |

| Home and recent conversions | Donut toolbar settings | Presets |
|:---:|:---:|:---:|
| <img src="docs/media/morph-page-home.png" width="270" alt="Home page with recent conversions and their Open buttons"> | <img src="docs/media/morph-page-toolbar.png" width="270" alt="Settings, Donut toolbar: the donuts per file type, their slices and the available presets"> | <img src="docs/media/morph-page-presets.png" width="270" alt="Settings, Presets: a preset's target, size, image and output options"> |

| Shortcuts | Watch folders | Engines |
|:---:|:---:|:---:|
| <img src="docs/media/morph-page-shortcuts.png" width="270" alt="Settings, Shortcuts: the Ctrl drag and the keyboard shortcut"> | <img src="docs/media/morph-page-watch.png" width="270" alt="Settings, Watch folders: a folder whose new files are converted"> | <img src="docs/media/morph-page-engines.png" width="270" alt="Settings, Engines: the optional engines with their status"> |

| General | About | Futuristic, light |
|:---:|:---:|:---:|
| <img src="docs/media/morph-page-general.png" width="270" alt="Settings, General: language, start with the computer and the Explorer menu"> | <img src="docs/media/morph-page-about.png" width="270" alt="The About page with the version, maintainer and update settings"> | <img src="docs/media/morph-futuristic-light.png" width="270" alt="The Theme page in the Futuristic look, light"> |

</details>

## Editions

| Edition | What you get | Status |
| --- | --- | --- |
| **Windows Setup EXE** | Per-user install (no admin rights), Start menu, Explorer menu "Convert with Morph", the `morph` command | 0.7.0 pre-release (unsigned) |
| **Windows portable ZIP** | Unzip and run `Morph.exe`; data stays beside the app | 0.7.0 pre-release (unsigned) |
| **Linux x64 / ARM64** | Unpack and run `./install.sh` (no root) | Pre-release; drag gestures need X11 |
| **macOS 13+** | `./install.sh` or drag to Applications | Pre-release; not notarized |
| **Source** | .NET 10 / Avalonia app, engines, tests and build scripts | MIT-licensed |

**Get a build now:** open the latest green run on the [Actions page](https://github.com/admknight/Morph/actions/workflows/ci.yml)
and download `windows-arm64-package` or a `portable-*` artifact (GitHub asks you to sign in for artifacts). Release
downloads will appear under [Releases](https://github.com/admknight/Morph/releases) once 0.7.0 is published.

### Basic requirements

- **Windows 10 or 11** (x64 or ARM64), **Ubuntu 22.04+** or a similar Linux (x64 or ARM64), or **macOS 13+**
- Nothing else to install: no .NET, Office or LibreOffice needed for the built-in formats
- Video, audio, e-books and some rarer formats use optional engines, downloaded once in **Settings → Engines**

### First run

1. Install or unzip Morph and start it. On Windows, if SmartScreen appears, click **More info → Run anyway**.
2. The **Home** page walks you through four short steps with a **practice picture** you convert yourself.
3. Now try it with your own files: hold <kbd>Ctrl</kbd>, drag a file, and let go on a format.
4. Press <kbd>Tab</kbd> (or roll the mouse wheel) to flip the donut to its back for tools and file actions.
5. Make it yours in **Settings → Toolbar**: pick a **template**, drag slices in and out, save your own layout.

No dragging? Select files and press the shortcut, right-click and choose **Convert with Morph**, or drop them on Home.

## Make it yours

- **Templates:** a template is a whole set of donuts and their size. Start with **Morph default** (bigger, with the
  new tools placed) or **Classic** (Filee's original layout), then save your own. Switching first keeps your current
  donuts as a template, so no layout is ever lost.
- **Presets:** size, quality, pages, output folder and file names; **watch folders** convert whatever lands in them.
- **Three looks:** Morph Soft, Material You and **Futuristic** (neon accent on deep navy), each light or dark.
- **24 languages**, including right-to-left Arabic, Persian and **Urdu** in a Nastaliq font.

## How Morph works

```text
Ctrl + drag  /  shortcut  /  "Convert with Morph"  /  Home drop zone
                              │
                              ▼
            Donut (Avalonia UI) ── only choices that fit the files
                              │
                              ▼
            Job queue + route planner ── cheapest engine path
                              │
          ┌───────────────────┼─────────────────────────┐
          ▼                   ▼                         ▼
   Engine workers       Bundled programs          Optional engines
   (ImageMagick,        (rhwp, 7-Zip,             (FFmpeg, LibreOffice,
    PDFium, HWP,         MorphCdr / libcdr)        Pandoc, Tesseract…)
    office, fonts…)      own process,              downloaded once,
   memory + time         time limit                SHA-256 checked
   limits, crash-safe
          └───────────────────┼─────────────────────────┘
                              ▼
            Result next to the original (never overwritten)
```

## Build and test

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/admknight/Morph.git
cd Morph
dotnet run --project src/Filee.App          # the app
dotnet run --project src/Filee.Cli -- help  # the morph command line
dotnet test Filee.slnx                      # the tests
```

On every push, [GitHub Actions](https://github.com/admknight/Morph/actions/workflows/ci.yml) checks formatting, builds,
and runs the full test suite on Windows x64 and ARM64, then packages Windows, Linux and macOS builds. The CorelDRAW
reader is compiled there from SHA-256-pinned sources (`build/build-cdr-reader.ps1`).

### Repository map

```text
src/Filee.App/        The app: donut, windows, pages, services, 24 languages
src/Filee.Core/       Presets, profiles, templates, job queue, route planner, worker isolation
src/Filee.Engines/    Converters (images, PDF, office, HWP, CAD, fonts, media, CorelDRAW, face/text finders)
src/Filee.Platform.*  Windows, Linux and macOS integration (selection, menus, share, clipboard)
src/Filee.Setup/      Morph's own installer
src/Morph.CdrReader/  MorphCdr, the CorelDRAW reader (libcdr)
tests/                Core, engine, app (UI, screenshots), platform and setup tests
build/                Packaging, engine fetching, signing, CI helpers
docs/                 Architecture, engines, converters, languages, themes, portable mode
```

The project folders keep Filee's names (`src/Filee.App`, `Filee.slnx`) so Filee's improvements keep merging in;
everything you see says Morph. [build/morph](build/morph/README.md) explains how a Filee release is taken in.

## Security, attribution and licensing

Security problems: please follow [SECURITY.md](SECURITY.md) rather than opening a public issue.

**Morph** is MIT-licensed, maintained by **Adam Knight (@admknight)**. It is built on
[Filee](https://github.com/KnifeLemon/Filee) by Myeonggu Kim (KnifeLemon) and the Filee contributors (MIT), and takes
ideas and some code from [OrbitDrop](https://github.com/i3sam/OrbitDrop) by i3sam (MIT): file and picture actions,
print, share, shelf, social sizes, normalize, palette, web search, face/text finding, the CorelDRAW reader, worker
limits, idle updates and the Gallery / VS Code selection. Face finding uses
[UltraFace](https://github.com/Linzaer/Ultra-Light-Fast-Generic-Face-Detector-1MB) (MIT); CorelDRAW files are read with
libcdr and librevenge (MPL 2.0). Every third-party component keeps its own licence; see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Contributions: [CONTRIBUTING.md](CONTRIBUTING.md) · Changes: [CHANGELOG.md](CHANGELOG.md)

<!-- sponsors: src/Filee.App/Assets/Sponsors/sponsors.json -->

---

<div align="center">

**Maintained by [Adam Knight (@admknight)](https://github.com/admknight)**  
[Portfolio](https://admknight.github.io/) · [Source](https://github.com/admknight/Morph) · [Actions](https://github.com/admknight/Morph/actions) · [Issue tracker](https://github.com/admknight/Morph/issues)

</div>
