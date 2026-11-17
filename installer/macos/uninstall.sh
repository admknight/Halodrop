#!/bin/sh
set -eu

install_dir="$HOME/Applications/Morph.app"
if [ -L "$install_dir" ] || [ ! -d "$install_dir" ]; then
    printf '%s\n' "No ordinary Morph app bundle was found at $install_dir."
    exit 1
fi
resolved_parent=$(CDPATH= cd -- "$HOME/Applications" && pwd -P)
resolved_app=$(CDPATH= cd -- "$install_dir" && pwd -P)
if [ "$resolved_app" != "$resolved_parent/Morph.app" ]; then
    printf '%s\n' 'Refusing to remove an app outside the expected Applications directory.'
    exit 1
fi
bundle_id=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$install_dir/Contents/Info.plist")
if [ "$bundle_id" != 'app.morph' ]; then
    printf '%s\n' 'The bundle is not Morph.'
    exit 1
fi
"$install_dir/Contents/MacOS/Morph" --uninstall-cleanup
if [ -L "$HOME/.local/bin/morph" ] && [ "$(readlink "$HOME/.local/bin/morph")" = "$install_dir/Contents/MacOS/morph-cli" ]; then
    rm -- "$HOME/.local/bin/morph"
fi
rm -rf -- "$install_dir"
printf '%s\n' 'Morph was removed. Your settings and conversion results were kept.'
