#!/usr/bin/env python3
"""Halodrop: the rename that follows a Filee merge, on what the merge brought in only.

Runs the four rename scripts (rebrand_i18n, rebrand_code_literals, rebrand_code_strings, rebrand_packaging) over the
whole tree, then puts back every line they changed that the merge didn't bring in. So text Filee added or changed is
renamed (Filee -> Halodrop), and everything Halodrop already had stays exactly as it was: internal names kept on
purpose (Filee.slnx, preset.filee_connect, test fonts, the Explorer menu package's identity...) can't be touched again,
however the scripts' rules grow.

The scripts change lines in place (never add or remove one), so a line keeps its number and is compared one to one.

Usage, after `git merge upstream/main` (conflicts resolved, merge committed or not):
    python3 build/morph/rebrand_after_merge.py --since <Halodrop's main before the merge>
    git diff            # the renames, all on lines the merge brought in
--since defaults to the merge commit's first parent (HEAD^1), or HEAD while a merge is still uncommitted.
"""
import argparse, difflib, os, re, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPTS = [("rebrand_i18n.py", []), ("rebrand_code_literals.py", ["src", "tests"]),
           ("rebrand_code_strings.py", ["src", "tests"]), ("rebrand_packaging.py", ["installer", "build"])]


def git(*args):
    return subprocess.check_output(["git", *args], text=True, encoding="utf-8")


def added_lines(since, path, text):
    """Line numbers (1-based) of `text` (the file before the scripts ran) that aren't in `since`: what the merge
    brought in. None when the file is new since then (all of it came with the merge)."""
    try:
        base = subprocess.check_output(["git", "show", f"{since}:{path}"], text=True, encoding="utf-8",
                                       stderr=subprocess.DEVNULL).splitlines()
    except subprocess.CalledProcessError:
        return None
    out = set()
    for tag, _, _, j1, j2 in difflib.SequenceMatcher(None, [line.rstrip("\r") for line in base],
                                                                [line.rstrip("\r") for line in text.splitlines()], autojunk=False).get_opcodes():
        if tag in ("replace", "insert"):
            out.update(range(j1 + 1, j2 + 1))
    return out


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--since", help="Halodrop's main before the merge (default: HEAD^1, or HEAD mid-merge)")
    args = parser.parse_args()
    root = git("rev-parse", "--show-toplevel").strip()
    os.chdir(root)
    since = args.since
    if not since:
        merging = os.path.exists(os.path.join(git("rev-parse", "--git-dir").strip(), "MERGE_HEAD"))
        since = "HEAD" if merging else "HEAD^1"

    # Snapshot of every tracked text file before the scripts run (they only touch tracked files).
    before = {}
    for path in git("ls-files").splitlines():
        if os.path.isfile(path):
            try:
                before[path] = open(path, encoding="utf-8", newline="").read()
            except UnicodeDecodeError:
                pass

    for script, script_args in SCRIPTS:
        subprocess.check_call([sys.executable, os.path.join(HERE, script), *script_args])

    renamed = reverted = 0
    for path, old in before.items():
        new = open(path, encoding="utf-8", newline="").read()
        if new == old:
            continue
        old_lines, new_lines = old.splitlines(keepends=True), new.splitlines(keepends=True)
        if len(old_lines) != len(new_lines):
            sys.exit(f"{path}: a rename script added or removed lines; check it by hand")
        merged = added_lines(since, path, old)
        result = []
        for number, (a, b) in enumerate(zip(old_lines, new_lines), start=1):
            if a != b and (merged is None or number not in merged):
                result.append(a); reverted += 1          # Halodrop's own line: leave it as it was
            else:
                result.append(b); renamed += a != b
        with open(path, "w", encoding="utf-8", newline="") as handle:
            handle.write("".join(result))
    print(f"renamed {renamed} line(s) the merge brought in; left {reverted} of Halodrop's own lines as they were")


if __name__ == "__main__":
    main()
