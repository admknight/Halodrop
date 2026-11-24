// "What's new": decides once per start what to show, shows it when the user sees Halodrop, and remembers the version so
// it shows once.
//
// Halodrop change: Filee showed slides after an update to a version with slides, and nothing otherwise. Halodrop shows
//   - after a fresh install: its feature tour (the newest slides up to this version), once the first-run engine
//     download window is closed;
//   - after an update: the slides of a version in between when there are any, else the changelog of every version in
//     between (Changelog, built in), so each update says what it brought;
//   - from About: the changelog up to this version, with the tour a click away.
//
// How an update is told from a fresh install: the first-run flag. A start before FirstRunCompleted is a new install.
// After that, WhatsNewSeenVersion says which version's news were seen; null means the settings come from a version
// older than the slides, i.e. it was updated. A version at or below the one seen (same version, downgrade) shows nothing
// and never lowers what was seen. Started by the installer after an update (--whats-new), it is an update whatever the
// settings say: they may have been deleted, or come from a version older than the first-run flag.

using Avalonia.Threading;
using Filee.App.Controls;
using Filee.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Filee.App.Services;

public sealed class WhatsNewService(UserDataStore store, WindowService windows, ILogger<WhatsNewService> log)
{
    private IReadOnlyList<SlideDeck>? _decks;
    private IReadOnlyList<ChangelogEntry>? _changelog;

    /// <summary>The assembly whose resources hold the decks.</summary>
    internal const string Assembly = "Halodrop";

    /// <summary>Every deck Halodrop ships, oldest first.</summary>
    public IReadOnlyList<SlideDeck> Decks => _decks ??= SlideDeck.LoadAll(Assembly);

    /// <summary>Halodrop change: the built-in changelog, newest first (tests set their own).</summary>
    internal IReadOnlyList<ChangelogEntry> ChangelogEntries
    {
        get => _changelog ??= Changelog.BuiltIn;
        set => _changelog = value;
    }

    /// <summary>The version running now (three parts).</summary>
    internal Version CurrentVersion { get; set; } = SlideDeck.TryParseVersion(UpdateService.CurrentVersion) ?? new Version(0, 0, 0);

    /// <summary>The deck waiting for the user to open Halodrop (it started in the background).</summary>
    public SlideDeck? Pending { get; private set; }

    /// <summary>Halodrop change: the notes waiting for the user to open Halodrop.</summary>
    public IReadOnlyList<ChangelogEntry>? PendingNotes { get; private set; }

    /// <summary>The newest deck up to this version: the feature tour. Null when there is none.</summary>
    public SlideDeck? Latest => SlideDeck.Newest(Decks, seen: null, CurrentVersion);

    /// <summary>What a start should do.</summary>
    /// <param name="Show">The deck to show, if any.</param>
    /// <param name="Remember">A version to store as seen right away (nothing to show).</param>
    /// <param name="Notes">Halodrop change: the changelog entries to show, when there are no slides.</param>
    /// <param name="Welcome">Halodrop change: a fresh install's tour, shown after the first-run windows.</param>
    internal readonly record struct Decision(SlideDeck? Show, Version? Remember, IReadOnlyList<ChangelogEntry>? Notes = null,
        bool Welcome = false);

    /// <summary>The rules described at the top of this file, without side effects.</summary>
    /// <param name="updatedFrom">
    /// The version the installer updated from: what was running before, so it decides what is new over a remembered
    /// version (going back to an older version and updating again shows the news again).
    /// </param>
    internal static Decision Decide(string? seenSetting, bool firstRunCompleted, Version current, IEnumerable<SlideDeck> decks,
        bool updated = false, string? updatedFrom = null, IEnumerable<ChangelogEntry>? changelog = null)
    {
        if (!firstRunCompleted && !updated)
        {
            // Halodrop change: a fresh install gets the tour.
            var tour = SlideDeck.Newest(decks, null, current);
            return tour is null ? new Decision(null, current) : new Decision(tour, null, Welcome: true);
        }
        var seen = SlideDeck.TryParseVersion(updatedFrom) ?? SlideDeck.TryParseVersion(seenSetting);
        if (seen is not null && current <= seen)
            return new Decision(null, null);
        var deck = SlideDeck.Newest(decks, seen, current);
        if (deck is not null)
            return new Decision(deck, null);
        // Halodrop change: no slides, so the notes of what changed (this version's alone when nothing was seen before).
        var entries = changelog?.ToList() ?? [];
        var notes = seen is null ? entries.Where(n => n.Version == current).ToList() : Changelog.Between(entries, seen, current);
        return notes.Count == 0 ? new Decision(null, current) : new Decision(null, null, notes);
    }

    /// <summary>
    /// Called once at start-up, before the first run is marked complete. <paramref name="background"/> (started at
    /// sign-in, or after an update without <c>--whats-new</c>) keeps the news until the main window opens.
    /// <paramref name="updated"/>: the installer started Halodrop after an update (<c>--whats-new</c>), from
    /// <paramref name="updatedFrom"/> when it says.
    /// </summary>
    public void CheckAtStartup(bool background, bool updated = false, string? updatedFrom = null)
    {
        Decision decision;
        try
        {
            decision = Decide(store.Settings.WhatsNewSeenVersion, store.Settings.FirstRunCompleted, CurrentVersion, Decks, updated,
                updatedFrom, ChangelogEntries);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.LogWarning(ex, "Could not read what's new");
            return;
        }
        if (decision.Remember is { } remember)
            Remember(remember);
        if (decision.Show is null && decision.Notes is null)
            return;
        log.LogInformation("What's new in {Version} is due ({Seen} seen before)", CurrentVersion.ToString(3),
            store.Settings.WhatsNewSeenVersion ?? "nothing");
        (Pending, PendingNotes) = (decision.Show, decision.Notes);
        if (background)
            windows.MainShown += OnMainShown;
        else
            // After the main window: the news goes on top of it (a fresh install's tour after the engine download too).
            DispatcherTimer.RunOnce(decision.Welcome ? () => windows.AfterEngineSetup(ShowPending) : ShowPending,
                TimeSpan.FromMilliseconds(decision.Welcome ? 1200 : 600));
    }

    private void OnMainShown(object? sender, EventArgs e)
    {
        windows.MainShown -= OnMainShown;
        Dispatcher.UIThread.Post(() => windows.AfterEngineSetup(ShowPending), DispatcherPriority.Background);
    }

    private void ShowPending()
    {
        if (Pending is { } deck)
            Show(deck);
        else if (PendingNotes is { } notes)
            ShowNotes(notes);
    }

    /// <summary>Shows <paramref name="deck"/> (the latest one when null) and remembers it as seen.</summary>
    public void Show(SlideDeck? deck = null)
    {
        deck ??= Latest;
        if (deck is null)
            return;
        (Pending, PendingNotes) = (null, null);
        windows.ShowWhatsNew(deck);
        Remember(CurrentVersion > deck.Version ? CurrentVersion : deck.Version);
    }

    /// <summary>
    /// Halodrop change: shows the notes of <paramref name="entries"/>, or of every version up to this one when null (About),
    /// and remembers this version as seen.
    /// </summary>
    public void ShowNotes(IReadOnlyList<ChangelogEntry>? entries = null)
    {
        entries ??= Changelog.Between(ChangelogEntries, null, CurrentVersion);
        (Pending, PendingNotes) = (null, null);
        if (entries.Count == 0)
        {
            Show();
            return;
        }
        windows.ShowChangelog(entries, Latest is null ? null : () => Show());
        Remember(CurrentVersion);
    }

    /// <summary>Stores <paramref name="version"/> as seen, unless a newer one already is.</summary>
    private void Remember(Version version)
    {
        var seen = SlideDeck.TryParseVersion(store.Settings.WhatsNewSeenVersion);
        if (seen is not null && seen >= version)
            return;
        store.Settings.WhatsNewSeenVersion = version.ToString(3);
        store.SaveSettings();
    }
}
