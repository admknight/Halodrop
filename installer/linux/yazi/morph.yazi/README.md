# Halodrop Yazi Plugin

Yazi plugin to convert selected or hovered files with [Halodrop](https://github.com/KnifeLemon/Filee), or open them directly in the interactive Halodrop Donut UI.

## Features

- Works on multi-file selections or the currently hovered file.
- Fast key menu (`ya.which`) for popular conversion targets:
  - **Images**: WebP, JPEG, PNG, AVIF, Remove Background
  - **Documents**: PDF, OCR to Markdown (`.md`), OCR to Text (`.txt`)
  - **Media**: MP3 (extract audio), MP4 (video compression)
  - **Archives**: ZIP, Extract
  - **Custom Format**: Prompt for any custom extension or preset supported by Halodrop
  - **GUI**: Open directly in the Halodrop Donut
- Non-blocking asynchronous conversion.
- Terminal notifications via Yazi's `ya.notify` and desktop notifications via `notify-send`.

## Installation

1. Copy or symlink this folder to your Yazi plugins directory:

   ```sh
   mkdir -p ~/.config/yazi/plugins
   ln -s "${XDG_DATA_HOME:-$HOME/.local/share}/halodrop/application/yazi/morph.yazi" ~/.config/yazi/plugins/morph.yazi
   ```

2. Add a keybinding in `~/.config/yazi/keymap.toml`:

   ```toml
   [[mgr.prepend_keymap]]
   on   = [ "c", "f" ]
   run  = "plugin halodrop"
   desc = "Convert with Halodrop"
   ```

## Requirements

- Yazi 25.5 or newer (for Yazi 0.3.x or older, use `[[manager.prepend_keymap]]` instead of `[[mgr.prepend_keymap]]`).
- `halodrop` CLI on `$PATH` (installed automatically to `~/.local/bin/halodrop` by `install.sh`).
- Optional: `notify-send` for desktop notifications.
