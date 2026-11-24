// Halodrop change: the changelog inside the app. CHANGELOG.md is built in (Assets/CHANGELOG.md), so the notes of a
// version show right after an update and offline: each "## <version>" section with its hidden "<!-- update -->" block
// (one-line summaries in the user's language, see ReleaseNotes) and its notes in English, as bullets and paragraphs.

using System.Text.RegularExpressions;
using Avalonia.Platform;
using Filee.App.Controls;

namespace Filee.App.Services;

/// <summary>A block of a version's notes: a bullet, a paragraph or a small heading. Text may hold **bold** runs.</summary>
public sealed record ChangelogBlock(ChangelogBlockKind Kind, string Text);

public enum ChangelogBlockKind
{
    Paragraph,
    Bullet,
    Heading,
}

/// <summary>One version's section of the changelog.</summary>
public sealed record ChangelogEntry(Version Version, ReleaseNote Note, IReadOnlyList<ChangelogBlock> Blocks)
{
    /// <summary>"0.10.0".</summary>
    public string DisplayVersion => Version.ToString(3);
}

public static partial class Changelog
{
    private static IReadOnlyList<ChangelogEntry>? _builtIn;

    /// <summary>The changelog built into Halodrop, newest first; empty when it can't be read.</summary>
    public static IReadOnlyList<ChangelogEntry> BuiltIn => _builtIn ??= Load();

    private static IReadOnlyList<ChangelogEntry> Load()
    {
        try
        {
            using var reader = new StreamReader(AssetLoader.Open(new Uri("avares://Halodrop/Assets/CHANGELOG.md")));
            return Parse(reader.ReadToEnd());
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException or FileNotFoundException)
        {
            return [];
        }
    }

    /// <summary>The versions after <paramref name="after"/> (exclusive; null: all) up to <paramref name="upTo"/>, newest first.</summary>
    public static IReadOnlyList<ChangelogEntry> Between(IEnumerable<ChangelogEntry> entries, Version? after, Version upTo) =>
        entries.Where(e => (after is null || e.Version > after) && e.Version <= upTo).OrderByDescending(e => e.Version).ToList();

    /// <summary>
    /// The sections of a changelog, newest first, up to the first other "## " heading (Filee's history). Text before the
    /// first version (the file's introduction) is left out.
    /// </summary>
    public static IReadOnlyList<ChangelogEntry> Parse(string markdown)
    {
        var entries = new List<ChangelogEntry>();
        var lines = markdown.Replace("\r", "", StringComparison.Ordinal).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var heading = SectionHeading().Match(lines[i]);
            // Halodrop's versions come first; Filee's history follows under a "## Filee …" heading and is left out.
            if (!heading.Success && lines[i].StartsWith("## ", StringComparison.Ordinal))
                break;
            if (!heading.Success || SlideDeck.TryParseVersion(heading.Groups[1].Value) is not { } version)
                continue;
            var end = i + 1;
            while (end < lines.Length && !lines[end].StartsWith("## ", StringComparison.Ordinal))
                end++;
            var section = string.Join('\n', lines[(i + 1)..end]);
            entries.Add(new ChangelogEntry(version, ReleaseNotes.Parse(version.ToString(3), section), Blocks(section)));
            i = end - 1;
        }
        return entries.GroupBy(e => e.Version).Select(g => g.First()).OrderByDescending(e => e.Version).ToList();
    }

    /// <summary>The visible part of a section: comments dropped, list items and paragraphs joined across their lines.</summary>
    private static List<ChangelogBlock> Blocks(string section)
    {
        var text = Comment().Replace(section, "");
        var blocks = new List<ChangelogBlock>();
        ChangelogBlockKind? kind = null;
        var current = new List<string>();

        void Flush()
        {
            if (kind is { } k && current.Count > 0)
                blocks.Add(new ChangelogBlock(k, Inline(string.Join(' ', current))));
            (kind, current) = (null, []);
        }

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd();
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0)
            {
                Flush();
                continue;
            }
            if (trimmed.StartsWith('#'))
            {
                Flush();
                blocks.Add(new ChangelogBlock(ChangelogBlockKind.Heading, Inline(trimmed.TrimStart('#').Trim())));
                continue;
            }
            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                Flush();
                kind = ChangelogBlockKind.Bullet;
                current.Add(trimmed[2..].Trim());
                continue;
            }
            // A line under a list item (indented) goes on with it; anything else starts or continues a paragraph.
            if (kind == ChangelogBlockKind.Bullet && line.StartsWith(' '))
            {
                current.Add(trimmed);
                continue;
            }
            if (kind != ChangelogBlockKind.Paragraph)
            {
                Flush();
                kind = ChangelogBlockKind.Paragraph;
            }
            current.Add(trimmed);
        }
        Flush();
        return blocks;
    }

    /// <summary>Links as their text, code without its backticks; **bold** stays for the window to draw.</summary>
    private static string Inline(string text) => Code().Replace(Link().Replace(text, "$1"), "$1");

    /// <summary>"Plain **bold** plain" as runs: (text, bold).</summary>
    public static IReadOnlyList<(string Text, bool Bold)> Runs(string text)
    {
        var parts = text.Split("**");
        var runs = new List<(string, bool)>();
        for (var i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0)
                runs.Add((parts[i], i % 2 == 1 && i < parts.Length - 1));
        return runs;
    }

    [GeneratedRegex(@"^##\s+v?(\d+\.\d+(?:\.\d+)?)\s*$")]
    private static partial Regex SectionHeading();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]+\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex Code();
}
