#!/bin/sh
set -eu

install_dir="$HOME/Applications/Halodrop.app"
if [ -L "$install_dir" ] || [ ! -d "$install_dir" ]; then
    printf '%s\n' "No ordinary Halodrop app bundle was found at $install_dir."
    exit 1
fi
resolved_parent=$(CDPATH= cd -- "$HOME/Applications" && pwd -P)
resolved_app=$(CDPATH= cd -- "$install_dir" && pwd -P)
if [ "$resolved_app" != "$resolved_parent/Halodrop.app" ]; then
    printf '%s\n' 'Refusing to remove an app outside the expected Applications directory.'
    exit 1
fi
bundle_id=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$install_dir/Contents/Info.plist")
if [ "$bundle_id" != 'app.morph' ]; then
    printf '%s\n' 'The bundle is not Halodrop.'
    exit 1
fi
"$install_dir/Contents/MacOS/Halodrop" --uninstall-cleanup
if [ -L "$HOME/.local/bin/halodrop" ] && [ "$(readlink "$HOME/.local/bin/halodrop")" = "$install_dir/Contents/MacOS/halodrop-cli" ]; then
    rm -- "$HOME/.local/bin/halodrop"
fi
rm -rf -- "$install_dir"
printf '%s\n' 'Halodrop was removed. Your settings and conversion results were kept.'
