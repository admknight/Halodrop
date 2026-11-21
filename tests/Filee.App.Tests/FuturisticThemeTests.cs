// Morph change: the Futuristic theme style (FuturisticPalette, ThemeService, Theme page).

using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Filee.App.Services;
using Filee.App.ViewModels.Pages;
using Filee.App.Views.Pages;
using Filee.Core.Localization;
using Filee.Core.Platform;
using Filee.Core.Settings;

namespace Filee.App.Tests;

public class FuturisticThemeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static TheoryData<string> Seeds() =>
        new(ThemeService.Swatches.Concat(ThemeService.Presets.SelectMany(p => new[] { p.LightHex, p.DarkHex }))
            .Concat(["#FFFFFF", "#000000", "#FFFF00", "#22D3EE"]).Distinct());

    [Fact]
    public void The_style_is_saved_by_name()
    {
        var json = JsonSerializer.Serialize(new AppSettings { Theme = new ThemeSettings { Style = ThemeStyle.Futuristic } }, JsonOptions);
        Assert.Contains("\"Futuristic\"", json);
        Assert.Equal(ThemeStyle.Futuristic, JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)!.Theme.Style);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Every_accent_stays_readable_in_light_and_dark(string hex)
    {
        foreach (var dark in new[] { false, true })
            foreach (var signature in new[] { false, true })
            {
                var p = FuturisticPalette.Generate(Color.Parse(hex), dark, signature);
                var what = $"{hex} dark={dark} signature={signature}";
                Assert.True(MaterialYouPalette.ContrastRatio(p.TextPrimary, p.Surface) >= 7, $"{what}: text on surface");
                Assert.True(MaterialYouPalette.ContrastRatio(p.TextSecondary, p.Surface) >= 4.5, $"{what}: secondary text on surface");
                Assert.True(MaterialYouPalette.ContrastRatio(p.TextSecondary, p.SurfaceAlt) >= 4.5, $"{what}: secondary text on alt surface");
                Assert.True(MaterialYouPalette.ContrastRatio(p.OnAccent, p.Accent) >= MaterialYouPalette.MinAccentContrast, $"{what}: text on accent");
                Assert.True(MaterialYouPalette.ContrastRatio(p.AccentStrong, p.AccentFaint) >= 4.5, $"{what}: strong accent on faint accent");
            }
    }

    [Fact]
    public void It_is_dark_navy_with_a_neon_accent_and_a_glow()
    {
        var dark = FuturisticPalette.Generate(Color.Parse(SoftPalette.DefaultAccent), isDark: true, useSignature: true);
        Assert.Equal(Color.Parse(FuturisticPalette.SignatureAccent).ToString(), dark.Accent.ToString());
        Assert.True(MaterialYouPalette.Luminance(dark.Page) < 0.06);
        Assert.True(dark.Page.B > dark.Page.R, "navy, not grey");
        var glows = Enumerable.Range(0, dark.CardShadow.Count).Select(i => dark.CardShadow[i]);
        Assert.Contains(glows, s => s.Blur >= 16 && s.Color.R == dark.Accent.R && s.Color.G == dark.Accent.G);

        // A colour the user chose becomes neon too: saturated and bright on the dark page.
        var (_, s, l) = MaterialYouPalette.RgbToHsl(FuturisticPalette.Generate(Color.Parse("#5B8BE3"), isDark: true, useSignature: false).Accent);
        Assert.True(s >= 0.79 && l > 0.55, $"s={s} l={l}");

        // Monochrome stays colourless.
        var mono = FuturisticPalette.Generate(Color.Parse("#1F1E1D"), isDark: true, useSignature: false);
        Assert.True(MaterialYouPalette.IsMonochrome(mono.Accent) || MaterialYouPalette.Luminance(mono.Accent) > 0.85);
    }

    [AvaloniaFact]
    public void ThemeService_applies_it_with_both_window_classes_and_crisper_corners()
    {
        TestServices.EnsureInitialized("en");
        var themeService = AppHost.Get<ThemeService>();
        var window = new Window();
        try
        {
            themeService.Apply(new ThemeSettings { Style = ThemeStyle.Futuristic, CornerRadius = 20, Mode = ThemeMode.Dark }, "en");
            themeService.ApplyWindowClass(window);

            Assert.True(themeService.IsFuturistic);
            Assert.False(themeService.IsMaterialYou);
            Assert.Contains("futuristic", window.Classes);
            Assert.Contains("material-you", window.Classes);

            var dark = (ResourceDictionary)Application.Current!.Resources.ThemeDictionaries[ThemeVariant.Dark];
            Assert.Equal(Color.Parse(FuturisticPalette.SignatureAccent), ((ISolidColorBrush)dark["AccentBrush"]!).Color);
            Assert.True(Application.Current.Resources.TryGetResource("SoftRadius", null, out var radius));
            Assert.Equal(12, ((CornerRadius)radius!).TopLeft);

            themeService.Apply(new ThemeSettings { Style = ThemeStyle.Soft }, "en");
            themeService.ApplyWindowClass(window);
            Assert.DoesNotContain("futuristic", window.Classes);
            Assert.DoesNotContain("material-you", window.Classes);
        }
        finally
        {
            themeService.Apply(new ThemeSettings(), "en");
        }
    }

    [Fact]
    public void Every_language_names_the_style()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Filee.App", "Assets", "i18n");
        foreach (var file in Directory.GetFiles(folder, "*.json"))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var key in new[] { "theme.style.Futuristic", "theme.style.futuristic_hint" })
                Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty(key).GetString()), $"{Path.GetFileName(file)}: {key}");
        }
    }

    [AvaloniaTheory]
    [InlineData("en", ThemeStyle.Futuristic, ThemeMode.Dark)]
    [InlineData("en", ThemeStyle.Futuristic, ThemeMode.Light)]
    [InlineData("ur", ThemeStyle.Futuristic, ThemeMode.Dark)]
    [InlineData("ur", ThemeStyle.Soft, ThemeMode.Light)]
    public void The_theme_page_renders(string language, ThemeStyle style, ThemeMode mode)
    {
        TestServices.EnsureInitialized(language);
        var service = AppHost.Get<ThemeService>();
        var directory = Directory.CreateTempSubdirectory("morph-futuristic-").FullName;
        var store = new UserDataStore(directory);
        store.Load();
        store.Settings.Theme.Mode = mode;
        var vm = new ThemePageViewModel(store, AppHost.Get<ILocalizer>(), AppHost.Get<IPlatformServices>());
        vm.SetStyleCommand.Execute(style); // as a click on the card does
        var window = new Window { Width = 1100, Height = 760, Content = new ThemePage { DataContext = vm } };
        try
        {
            service.Apply(store.Settings.Theme, language);
            service.ApplyWindowClass(window);
            window.Bind(Window.BackgroundProperty, window.GetResourceObservable("PageBrush"));
            // As MainWindow does: the reading direction comes from the language (SoftPalette.FlowDirectionKey).
            window.Bind(Visual.FlowDirectionProperty, window.GetResourceObservable(SoftPalette.FlowDirectionKey));
            window.Show();
            for (var i = 0; i < 20; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
            using var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            using var stream = File.Create(Path.Combine(TestServices.ScreenshotDirectory, $"theme-{style}-{mode}-{language}.png".ToLowerInvariant()));
            frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            Assert.Equal(language == "ur" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight, ((Control)window.Content!).FlowDirection);
            Assert.Equal(style == ThemeStyle.Futuristic, window.Classes.Contains("futuristic"));
        }
        finally
        {
            window.Close();
            service.Apply(new ThemeSettings(), "en");
            TestServices.EnsureInitialized("en");
            Dispatcher.UIThread.RunJobs();
            Directory.Delete(directory, true);
        }
    }

    [AvaloniaFact]
    public void Choosing_it_shows_the_signature_cyan_and_leaving_it_brings_the_default_back()
    {
        TestServices.EnsureInitialized("en");
        var directory = Directory.CreateTempSubdirectory("morph-futuristic-vm-").FullName;
        try
        {
            var store = new UserDataStore(directory);
            store.Load();
            var vm = new ThemePageViewModel(store, AppHost.Get<ILocalizer>(), AppHost.Get<IPlatformServices>());

            vm.SetStyleCommand.Execute(ThemeStyle.Futuristic);
            Assert.True(vm.IsFuturistic);
            Assert.False(vm.IsSoft);
            Assert.Equal(FuturisticPalette.SignatureAccent, store.Settings.Theme.Accent, ignoreCase: true);

            vm.SetStyleCommand.Execute(ThemeStyle.Soft);
            Assert.Equal(SoftPalette.DefaultAccent, store.Settings.Theme.Accent, ignoreCase: true);

            // A colour the user picked stays, whichever style they move to.
            vm.Accent = Color.Parse("#D85A30");
            vm.SetStyleCommand.Execute(ThemeStyle.Futuristic);
            Assert.Equal("#D85A30", store.Settings.Theme.Accent, ignoreCase: true);
            vm.SetStyleCommand.Execute(ThemeStyle.Soft);
            Assert.Equal("#D85A30", store.Settings.Theme.Accent, ignoreCase: true);
        }
        finally
        {
            AppHost.Get<ThemeService>().Apply(new ThemeSettings(), "en");
            Directory.Delete(directory, true);
        }
    }
}
