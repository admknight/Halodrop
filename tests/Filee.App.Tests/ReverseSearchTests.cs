// Morph change: "Search the web for this picture" reads the results page out of Google Lens's answer, and opens only
// a Google page: whatever else an answer holds is never opened.

using Filee.App.Services;

namespace Filee.App.Tests;

public class ReverseSearchTests
{
    [Fact]
    public void The_results_page_is_read_from_a_meta_refresh()
    {
        const string html = """<html><head><meta http-equiv="refresh" content="0;url=https://lens.google.com/search?ep=gsbubb&amp;hl=en&amp;vsrid=abc123"></head></html>""";
        Assert.Equal("https://lens.google.com/search?ep=gsbubb&hl=en&vsrid=abc123", FileActions.ResultsPage(html)?.AbsoluteUri);
    }

    [Fact]
    public void Script_escaped_ampersands_are_undone()
    {
        const string html = """<script>var u="https://lens.google.com/search?ep=gsbubb&hl=en&vsrid=x9";</script>""";
        Assert.Equal("https://lens.google.com/search?ep=gsbubb&hl=en&vsrid=x9", FileActions.ResultsPage(html)?.AbsoluteUri);
    }

    [Theory]
    [InlineData("<html>Sorry, something went wrong</html>")]
    [InlineData("""<a href="https://evil.example/search?x=1">""")]
    [InlineData("")]
    public void An_answer_without_a_Lens_page_gives_none(string html) => Assert.Null(FileActions.ResultsPage(html));

    [Fact]
    public void The_copy_sent_is_small() => Assert.InRange(FileActions.SearchSide, 512, 1600);
}
