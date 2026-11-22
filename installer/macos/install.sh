#!/bin/sh
set -eu

source_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd -P)
install_dir="$HOME/Applications/Halodrop.app"
bin_dir="$HOME/.local/bin"
if [ -e "$install_dir" ] || [ -L "$install_dir" ]; then
    printf '%s\n' "An installation already exists at $install_dir. Run uninstall.sh first."
    exit 1
fi
# A halodrop link left by an earlier Halodrop whose app is gone (moved to the Trash instead of uninstall.sh) is replaced.
# Anything else at that path isn't ours and stays.
stale_link=false
if [ -L "$bin_dir/halodrop" ] && [ ! -e "$bin_dir/halodrop" ]; then
    case $(readlink "$bin_dir/halodrop") in
        */Halodrop.app/Contents/MacOS/halodrop-cli) stale_link=true ;;
    esac
fi
if [ "$stale_link" = false ] && { [ -e "$bin_dir/halodrop" ] || [ -L "$bin_dir/halodrop" ]; }; then
    printf '%s\n' "$bin_dir/halodrop already exists. Remove or rename it before installing Halodrop."
    exit 1
fi
test -x "$source_dir/Halodrop.app/Contents/MacOS/Halodrop"
mkdir -p "$HOME/Applications" "$bin_dir"
/usr/bin/ditto "$source_dir/Halodrop.app" "$install_dir"
# ditto keeps the download's quarantine flag: macOS would then start Halodrop from a translocated copy and could stop
# the halodrop command without a word (#316).
/usr/bin/xattr -dr com.apple.quarantine "$install_dir" 2>/dev/null || true
if [ "$stale_link" = true ]; then
    rm -f -- "$bin_dir/halodrop"
    printf '%s\n' "Replaced the halodrop link left by an earlier installation."
fi
ln -s "$install_dir/Contents/MacOS/halodrop-cli" "$bin_dir/halodrop"
printf '%s\n' "Installed $install_dir" "CLI: $bin_dir/halodrop (add $bin_dir to PATH if needed)."
printf '%s\n' 'Open Halodrop from Applications. Grant Accessibility and Finder Automation access when requested.'
printf '%s\n' 'If Halodrop is already listed and switched on under Accessibility, remove it with the - button and add it again with +.'
