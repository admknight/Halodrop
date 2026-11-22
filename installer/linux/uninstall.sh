#!/bin/sh
set -eu

data_dir=${XDG_DATA_HOME:-"$HOME/.local/share"}
install_dir="$data_dir/halodrop/application"
desktop_file="$data_dir/applications/app.morph.desktop"
gnome_extension_id="morph-donut@admknight.github.io"
gnome_extension_dir="$data_dir/gnome-shell/extensions/$gnome_extension_id"
python3 - "$data_dir" "$install_dir" <<'PY'
import pathlib
import sys

root, target = map(pathlib.Path, sys.argv[1:])
expected = root.resolve() / 'halodrop' / 'application'
if target.is_symlink() or target.resolve() != expected:
    raise SystemExit('Refusing to remove an installation outside its expected directory.')
if (target / '.morph-package').read_text(encoding='utf-8').strip() != 'app.morph':
    raise SystemExit('The directory is not an installation created by Halodrop.')
PY
"$install_dir/Halodrop" --uninstall-cleanup
if [ -f "$gnome_extension_dir/.morph-package" ] && [ "$(cat "$gnome_extension_dir/.morph-package")" = 'app.morph' ]; then
    if command -v gnome-extensions >/dev/null 2>&1; then
        gnome-extensions disable "$gnome_extension_id" >/dev/null 2>&1 || true
    fi
    rm -rf -- "$gnome_extension_dir"
fi
if [ -L "$HOME/.local/bin/halodrop" ] && [ "$(readlink "$HOME/.local/bin/halodrop")" = "$install_dir/halodrop-cli" ]; then
    rm -- "$HOME/.local/bin/halodrop"
fi
if [ -f "$desktop_file" ] && grep -q '^X-Morph-Managed=true$' "$desktop_file"; then
    rm -- "$desktop_file"
fi
rm -rf -- "$install_dir"
printf '%s\n' 'Halodrop was removed. Your settings and conversion results were kept.'
