// Morph change: Settings → Toolbar → Templates. Using a template puts its donuts and size in place and the list
// follows; the donuts before are kept as a template of the user's; saving and deleting touch only the user's own.

using Avalonia.Headless.XUnit;
using Filee.App.Services;
using Filee.App.ViewModels.Pages;
using Filee.Core.Conversion;
using Filee.Core.Morph;
using Filee.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace Filee.App.Tests;

public class LayoutTemplatePageTests
{
    [AvaloniaFact]
    public void Using_a_template_changes_the_donuts_and_size_and_keeps_the_old_ones()
    {
        TestServices.EnsureInitialized("en");
        var store = AppHost.Get<UserDataStore>();
        var presets = store.Presets.ToList();
        var profiles = store.Profiles.Select(p => p.Clone()).ToList();
        var radius = store.Settings.Donut.OuterRadius;
        var own = store.OwnTemplates.ToList();
        try
        {
            using var vm = new ToolbarPageViewModel(store, AppHost.Get<Filee.Core.Localization.ILocalizer>(), AppHost.Get<WindowService>(),
                AppHost.Get<PresetAvailability>(), AppHost.Get<ConverterCatalog>(), NullLogger<ToolbarPageViewModel>.Instance);
            Assert.Equal(["Halodrop default", "Classic"], vm.Templates.Take(2).Select(t => t.Name));
            Assert.False(vm.CanDeleteTemplate); // a built-in one is selected

            store.Profiles[0].PresetIds.Reverse();
            vm.ApplyTemplate(LayoutTemplates.MorphDefault());

            Assert.Equal(LayoutTemplates.MorphRadius, vm.OuterRadius);
            Assert.Equal(store.Profiles.Count, vm.Profiles.Count);
            Assert.Same(store.Profiles[0], vm.Profiles[0].Profile);
            Assert.Contains("kept as", vm.TemplateMessage);
            Assert.Equal(LayoutTemplates.MorphDefaultId, vm.SelectedTemplate!.Template.Id);
            var kept = vm.Templates.Last();
            Assert.False(kept.Template.IsBuiltIn);

            vm.SelectedTemplate = kept;
            Assert.True(vm.CanDeleteTemplate);
            vm.NewTemplateName = "Work";
            Assert.True(vm.SaveTemplateCommand.CanExecute(null));
            vm.SaveTemplateCommand.Execute(null);
            Assert.Equal("Work", vm.SelectedTemplate!.Name);
            Assert.Equal("", vm.NewTemplateName);
        }
        finally
        {
            store.OwnTemplates.Clear();
            store.OwnTemplates.AddRange(own);
            store.SaveTemplates();
            store.Presets.Clear();
            store.Presets.AddRange(presets);
            store.Profiles.Clear();
            store.Profiles.AddRange(profiles);
            store.Settings.Donut.OuterRadius = radius;
            store.SaveLibrary();
            store.SaveSettings();
        }
    }
}
