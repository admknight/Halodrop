// Morph change: Morph started as an engine worker (IsolatedStepRunner starts it with WorkerProtocol.Switch). No window,
// no tray, no single-instance lock: it builds the engines the way the app does and serves steps over its standard input
// and output until Morph closes the pipe.

using System.Text;
using Filee.Core.Conversion.Isolation;
using Filee.Core.Settings;
using Filee.Engines;
using Filee.Engines.Infrastructure;

namespace Filee.App.Services;

internal static class EngineWorkerHost
{
    /// <summary>The arguments Morph starts a worker with.</summary>
    public static IReadOnlyList<string> Arguments(string dataDirectory) => [WorkerProtocol.Switch, "--data-dir", dataDirectory];

    public static int Run(string[] args)
    {
        // The real standard output carries the messages; anything printed through Console goes to standard error.
        var messages = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = false };
        Console.SetOut(Console.Error);
        // Explicitly UTF-8: redirected input would otherwise be read in the console's code page on Windows, and file
        // names in Urdu, Korean or Chinese would arrive broken.
        using var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
        try
        {
            var dataDirectory = DataDirectory(args) ?? UserDataStore.DefaultDirectory;
            Filee.Engines.Magick.MagickSecurity.Initialize();
            var store = new UserDataStore(dataDirectory);
            store.Load();
            var settingsFile = Path.Combine(store.Directory, "settings.json");
            var applied = DateTime.MinValue;
            void Refresh()
            {
                // Settings → Engines changes while the worker lives: pick them up before the next step.
                var stamp = File.Exists(settingsFile) ? File.GetLastWriteTimeUtc(settingsFile) : DateTime.MinValue;
                if (stamp == applied)
                    return;
                if (applied != DateTime.MinValue)
                    store.Load();
                applied = stamp;
                EngineEnvironment.OwnCopies = new Dictionary<string, string>(store.Settings.EngineOwnCopies);
                EngineEnvironment.LibreOfficeForSlides = store.Settings.LibreOfficeForSlides;
                EngineEnvironment.LibreOfficeForHwp = store.Settings.LibreOfficeForHwp;
                EngineEnvironment.LibreOfficeForDocuments = store.Settings.LibreOfficeForDocuments;
                EngineEnvironment.LibreOfficeForSheets = store.Settings.LibreOfficeForSheets;
            }
            Refresh();
            var engines = EngineRegistry.CreateAll(new EngineEnvironment(store.Directory));
            return new EngineWorker(engines, messages, Refresh).RunAsync(input).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Engine worker failed: {ex}");
            return 1;
        }
    }

    private static string? DataDirectory(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == "--data-dir")
                return args[i + 1];
        return null;
    }
}
