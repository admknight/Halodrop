// Morph change: what people see says Morph, never Filee (Morph is built on Filee, credited in the notices). These catch
// what a Filee merge brings back in: new texts in the 23 languages, new strings in the code, links to Filee's servers.
// After a merge, build/morph/README.md says how to rename the new parts.

using System.Text.Json;
using System.Text.RegularExpressions;
using Filee.App.Services;

namespace Filee.App.Tests;

public sealed partial class MorphBrandingTests
{
    // Names that keep "Filee" on purpose: internal types and resources, Filee's website and repositories, text keys and
    // stored ids (their values say Morph), test fonts, and interpolation holes ({Filee.Core.…}).
    [GeneratedRegex(@"filee\.sh|KnifeLemon|^(using:|clr-namespace:)|^Filee\.(App|Core|Engines|Cli|Setup|Platform)\b|Filee\.slnx|FileeTest|vm:FileeConnect|^Filee-Presets|\{Filee\.")]
    private static partial Regex AllowedInCode();

    private static readonly HashSet<string> AllowedKeys = new(StringComparer.Ordinal)
    {
        "preset.filee_connect", "{DynamicResource engines.install_filee_copy}",
    };

    [GeneratedRegex("\"((?:[^\"\\\\\\n]|\\\\.)*)\"")]
    private static partial Regex StringLiteral();

    // The name as written (Filee, filee, FILEE), not letters that happen to spell it ("fileExplorer", "MoveFileEx").
    private static bool SaysFilee(string text) =>
        text.Contains("Filee", StringComparison.Ordinal) || text.Contains("filee", StringComparison.Ordinal)
        || text.Contains("FILEE", StringComparison.Ordinal);

    private static string RepositoryRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Filee.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Run the tests from inside the repository.");
    }

    [Fact]
    public void No_interface_text_in_any_language_says_Filee()
    {
        var found = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(RepositoryRoot(), "src", "Filee.App", "Assets", "i18n"), "*.json"))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var entry in json.RootElement.EnumerateObject())
            {
                var text = entry.Value.ValueKind == JsonValueKind.String ? entry.Value.GetString()! : "";
                if (SaysFilee(Regex.Replace(text, @"filee\.sh", "")))
                    found.Add($"{Path.GetFileName(file)}: {entry.Name}");
            }
        }
        Assert.Empty(found);
    }

    // Halodrop change: Halodrop was called Morph up to 0.7.0; no interface text says Morph any more.
    [Fact]
    public void No_interface_text_in_any_language_says_Morph()
    {
        var found = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(RepositoryRoot(), "src", "Filee.App", "Assets", "i18n"), "*.json"))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var entry in json.RootElement.EnumerateObject())
            {
                var text = entry.Value.ValueKind == JsonValueKind.String ? entry.Value.GetString()! : "";
                if (Regex.IsMatch(text, "(?<![A-Za-z])[Mm]orph(?![A-Za-z])"))
                    found.Add($"{Path.GetFileName(file)}: {entry.Name}");
            }
        }
        Assert.Empty(found);
    }

    [Fact]
    public void No_string_in_the_code_says_Filee()
    {
        var found = new List<string>();
        var src = Path.Combine(RepositoryRoot(), "src");
        foreach (var file in Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".axaml", StringComparison.Ordinal))
                                 && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                                 && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            var number = 0;
            foreach (var line in File.ReadLines(file))
            {
                number++;
                var code = line.TrimStart();
                if (code.StartsWith("//", StringComparison.Ordinal) || code.StartsWith("<!--", StringComparison.Ordinal) || code.StartsWith('*'))
                    continue;
                foreach (Match literal in StringLiteral().Matches(line))
                {
                    var value = literal.Groups[1].Value;
                    if (SaysFilee(value) && !AllowedInCode().IsMatch(value) && !AllowedKeys.Contains(value))
                        found.Add($"{Path.GetRelativePath(src, file)}:{number}: {value}");
                }
            }
        }
        Assert.Empty(found);
    }

    [Fact]
    public void Updates_and_links_point_at_Morph_not_Filee()
    {
        Assert.DoesNotContain("KnifeLemon", UpdateService.RepositoryUrl, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("https://github.com/", UpdateService.RepositoryUrl, StringComparison.OrdinalIgnoreCase);
        Assert.True(UpdateService.HasReleaseRepository);
        Assert.StartsWith("https://github.com/", MorphDeveloper.Url, StringComparison.Ordinal);
    }
}
