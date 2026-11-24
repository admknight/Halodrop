#!/usr/bin/env bash
# Halodrop: the checks before anything goes to main. Two stages:
#
#   1. Here:  build/morph/check-before-push.sh          build, format, tests quoting changed texts, Core + CLI tests
#             build/morph/check-before-push.sh --full   also the app's UI tests (about an hour on 2 cores)
#   2. On GitHub, on Windows: push the commit to a check/<topic> branch (git push origin HEAD:check/<topic>), wait
#      for its CI run (build, installer, every test project, 4 UI parts) to pass, and only then
#      git push origin HEAD:main. Engines, Platform and Setup tests need Windows, so stage 2 is where they count.
#
# Push to check/ only when this ends with "ALL CHECKS PASSED".
#
# Tests that can only fail in a Linux sandbox (running as root, tools Windows CI has) are listed in
# build/morph/sandbox-known-failures.txt (a line ending in * covers a whole class: no network, no Windows); any other
# failure stops the push. A change to a text in
# src/Filee.App/Assets/i18n/en.json is also checked against tests that quote the old text.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}" PATH="$HOME/.dotnet:$PATH"
known="build/morph/sandbox-known-failures.txt"
log="$(mktemp -d)"
problems=0

step() { printf '\n== %s\n' "$1"; }
fail() { printf 'FAIL: %s\n' "$1"; problems=$((problems + 1)); }

step "Build (Release)"
dotnet build Filee.slnx -c Release -v q -nologo >"$log/build.txt" 2>&1 || { grep -E " error " "$log/build.txt" | sort -u | head -20; fail "build"; }

step "Format"
for p in $(git ls-files 'src/*.csproj' 'tests/*.csproj'); do
  dotnet format "$p" --verify-no-changes --no-restore --severity warn >/dev/null 2>&1 || fail "format: $p"
done

step "Tests that still quote changed texts"
if git rev-parse -q --verify origin/main >/dev/null; then
  python3 - <<'EOF' || problems=$((problems + 1))
import json, re, subprocess, sys
old = json.loads(subprocess.check_output(["git", "show", "origin/main:src/Filee.App/Assets/i18n/en.json"]))
new = json.load(open("src/Filee.App/Assets/i18n/en.json", encoding="utf-8"))
gone = {v for k, v in old.items() if k in new and new[k] != v and len(v) > 8}
bad = []
for f in subprocess.check_output(["git", "ls-files", "tests/*.cs"]).decode().split():
    for i, line in enumerate(open(f, encoding="utf-8", errors="ignore"), 1):
        for m in re.finditer(r'"((?:[^"\\]|\\.){9,})"', line):
            if m.group(1) in gone:
                bad.append(f"{f}:{i}: {m.group(1)}")
print("\n".join(bad) or "none")
sys.exit(1 if bad else 0)
EOF
fi

step "Tests"
projects="tests/Filee.Core.Tests/Filee.Core.Tests.csproj tests/Filee.Cli.Tests/Filee.Cli.Tests.csproj"
[ "${1:-}" = "--full" ] && projects="$projects tests/Filee.App.Tests/Filee.App.Tests.csproj"
for p in $projects; do
  name="$(basename "$p" .csproj)"
  echo "-- $name"
  dotnet test "$p" -c Release --no-build -nologo >"$log/$name.txt" 2>&1
  grep -E "^\s+Failed [A-Za-z]" "$log/$name.txt" | sed -E 's/^\s+Failed //; s/ \[[^]]*\]$//; s/\(.*//' | sort -u >"$log/$name.failed"
  grep -E "Passed!|Failed!" "$log/$name.txt" | tail -1
  while read -r test; do
    [ -z "$test" ] && continue
    if grep -qxF "$test" "$known" 2>/dev/null || grep -E '\*$' "$known" 2>/dev/null | sed 's/\*$//' | while read -r p; do [[ "$test" == "$p"* ]] && echo y; done | grep -q y
    then echo "   (sandbox only) $test"; else fail "$test"; fi
  done <"$log/$name.failed"
  grep -qE "Passed!|Failed!" "$log/$name.txt" || fail "$name did not finish"
done

echo
if [ "$problems" -eq 0 ]; then echo "ALL CHECKS PASSED (now push to check/<topic> and wait for its CI before main)"; else echo "$problems PROBLEM(S): do not push (logs in $log)"; exit 1; fi
