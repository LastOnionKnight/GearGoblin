// Plugin.cs
//
// v0.4.6 — adds the /goblininfo slash command, which dumps the current
// StatusPanelInjector diagnostic snapshot to chat in a copy-paste-friendly
// format. Same payload is also offered via a button on the Diagnostics tab.
// Bug reports become "paste me your /goblininfo" instead of "send me a
// screenshot of the Character window and your /xllog."
//
// Lineage:
//   v0.4.0  Plugin scaffolding + StatusPanelInjector wiring
//   v0.4.1  /goblinexport command + GearsetExporter
//   v0.4.6  /goblininfo command + BuildGoblinInfoString() (this release)

using System;
using System.Text;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using GearGoblin.Services;
using GearGoblin.UI;
using Dalamud.Bindings.ImGui;
using Microsoft.Extensions.DependencyInjection;

namespace GearGoblin;

public sealed class Plugin : IDalamudPlugin
{
    // v0.4.7.1 "Brand Convergence": the user-facing name is now "Tonberry Tactics",
    // matching the website. We deliberately keep the *internal* identifiers
    // (csproj InternalName, namespace, WindowSystem name) as "GearGoblin" so
    // existing user configs and saved window state survive. Full code-namespace
    // rename is scoped to v0.5.0 with the Core refactor.
    public string Name => "Tonberry Tactics";

    // v0.4.7.1: /tt* are the new primary commands. /goblin* remain as
    // deprecated aliases through v0.5.x and will be removed at v1.0 (migration
    // strategy C from the v0.4.8 product Q&A: graceful staged transition).
    private const string CommandName       = "/tt";
    private const string ExportCommandName = "/ttexport";
    private const string InfoCommandName   = "/ttinfo";
    private const string ImportCommandName = "/ttimport";


    [Obsolete("Use ConfigService.Current for reads. Configuration property will be removed by end of Phase 1.")]
    public Configuration Configuration { get; }
    public IConfigurationService ConfigService { get; }
    public WindowSystem  WindowSystem  { get; } = new("GearGoblin");
    public IServiceProvider Provider { get; }

    public IInventoryReader Inventory { get; }
    public IGearsetExporter  Exporter  { get; }   // v0.4.1
    public IGearsetImporter  Importer  { get; }   // v0.4.7 (scaffold; full body next session)

    // v0.4.0: native injection into the CharacterStatus addon.
    public IStatusPanelInjector StatusPanel { get; }

    // v0.4.7.1: brand artwork loaded from Assets/ at startup.
    public BrandResources Brand { get; }

    // v0.6.0: custom font handles via IFontAtlas Phase 2.
    public Theme.FontAtlasManager Fonts { get; }

    private readonly MainWindow mainWindow;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        // Inject Dalamud services into the static container.
        pluginInterface.Create<DalamudServices>();

        // Load or create config.
        Configuration = DalamudServices.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(DalamudServices.PluginInterface);

        // Build the DI container.
        Provider = ServiceContainer.CreateProvider(this);

        // Services.
        Inventory   = Provider.GetRequiredService<IInventoryReader>();
        Exporter    = Provider.GetRequiredService<IGearsetExporter>();                       // v0.4.1
        Importer    = Provider.GetRequiredService<IGearsetImporter>();                            // v0.4.7 (scaffold)
        StatusPanel = Provider.GetRequiredService<IStatusPanelInjector>();
        ConfigService = Provider.GetRequiredService<IConfigurationService>();
        Brand       = new BrandResources();                                 // v0.4.7.1
        Fonts       = new Theme.FontAtlasManager(pluginInterface);          // v0.6.0

        // UI.
        mainWindow = new MainWindow(this);
        WindowSystem.AddWindow(mainWindow);

        DalamudServices.PluginInterface.UiBuilder.Draw         += DrawUI;
        DalamudServices.PluginInterface.UiBuilder.OpenConfigUi += ToggleMain;
        DalamudServices.PluginInterface.UiBuilder.OpenMainUi   += ToggleMain;

        // Commands — primary set (/tt*).
        DalamudServices.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Tonberry Tactics. Usage: /tt"
        });
        DalamudServices.CommandManager.AddHandler(ExportCommandName, new CommandInfo(OnExportCommand)
        {
            HelpMessage = "Export your equipped gearset to clipboard for use in the Tonberry Tactics website."
        });
        DalamudServices.CommandManager.AddHandler(InfoCommandName, new CommandInfo(OnInfoCommand)
        {
            HelpMessage = "Print Tonberry Tactics diagnostics to chat. Useful for bug reports."
        });
        DalamudServices.CommandManager.AddHandler(ImportCommandName, new CommandInfo(OnImportCommand)
        {
            HelpMessage = "Import a GG-PLAN:v1: plan string from clipboard. Pair with /ttexport."
        });


        DalamudServices.Log.Info($"Tonberry Tactics (formerly GearGoblin) v{GetType().Assembly.GetName().Version} loaded.");
    }

    public void Dispose()
    {
        // Tear down in reverse construction order. StatusPanel must dispose
        // before the WindowSystem so its click-handler unregistration runs
        // while Dalamud's services are still alive. Fonts disposes before
        // the WindowSystem too so MainWindow can't draw stale handles.
        StatusPanel?.Dispose();
        Fonts?.Dispose();                                                   // v0.6.0
        Brand?.Dispose();                                                   // v0.4.7.1

        DalamudServices.PluginInterface.UiBuilder.Draw         -= DrawUI;
        DalamudServices.PluginInterface.UiBuilder.OpenConfigUi -= ToggleMain;
        DalamudServices.PluginInterface.UiBuilder.OpenMainUi   -= ToggleMain;

        // Primary commands.
        DalamudServices.CommandManager.RemoveHandler(CommandName);
        DalamudServices.CommandManager.RemoveHandler(ExportCommandName);
        DalamudServices.CommandManager.RemoveHandler(InfoCommandName);
        DalamudServices.CommandManager.RemoveHandler(ImportCommandName);

        WindowSystem.RemoveAllWindows();
        mainWindow.Dispose();
    }

    private void OnCommand(string command, string args) => ToggleMain();

    /// <summary>
    /// v0.4.1: /goblinexport command. Serializes the currently-equipped
    /// gearset (job, level, items, melded materia) to a base64-encoded JSON
    /// string and copies it to the system clipboard for use in the Tonberry
    /// Tactics web app at tonberrytactics.pages.dev. Pure read; doesn't open
    /// any UI.
    /// </summary>
    private void OnExportCommand(string command, string args) => Exporter.ExportToClipboard();

    /// <summary>
    /// /ttinfo command. Builds the StatusPanelInjector diagnostic snapshot,
    /// copies it to the system clipboard, and opens the standalone Tonberry
    /// Tactics window so the Diagnostics tab is one click away.
    ///
    /// v0.6.5.1 redesign — the previous implementation printed the diagnostic
    /// block to chat line-by-line via a foreach over <c>info.Split('\n')</c>.
    /// That pattern was responsible for the v0.6.5 hard crash inside FFXIV's
    /// native <c>Client::System::String::Utf8String::SetString</c> at
    /// <c>+0x23</c> (RDX=null source pointer), triggered from
    /// <c>Dalamud.Game.Gui.ChatGui.UpdateQueue</c> during a framework tick:
    /// some lines of the dump (notably the trailing empty entry produced by
    /// <c>AppendLine</c>'s CRLF terminator splitting on <c>'\n'</c>) marshaled
    /// into a null native string under the right tick timing, and the game's
    /// SetString dereferenced the null pointer. The bug was latent across
    /// every release from v0.4.6 to v0.6.5; v0.6.5 just got unlucky and
    /// surfaced it.
    ///
    /// The fix removes the multi-line ChatGui.Print pattern entirely.
    /// Clipboard write + ImGui calls are dispatched onto the framework
    /// thread (RunOnFrameworkThread) because ImGui.SetClipboardText reads
    /// from the active ImGui context, which only exists during the render
    /// tick. The single short ASCII confirmation line printed to chat
    /// (one Print call, no foreach, no empty entries) is the only chat
    /// I/O this command does now.
    /// </summary>
    private void OnInfoCommand(string command, string args)
    {
        try
        {
            // Build the diagnostic block first. StringBuilder work is thread-safe
            // and StatusPanel.GetDiagnostics() is a snapshot read.
            var info = BuildGoblinInfoString();

            // ImGui clipboard write needs the render-thread ImGui context.
            // Schedule it for the next framework tick.
            DalamudServices.Framework.RunOnFrameworkThread(() =>
            {
                try
                {
                    ImGui.SetClipboardText(info);
                }
                catch (Exception clipEx)
                {
                    // Clipboard failure is non-fatal — user still has the window.
                    DalamudServices.Log.Warning(clipEx,
                        "OnInfoCommand: ImGui.SetClipboardText threw; user will need to use the Diagnostics-tab button instead.");
                }
            });

            // Open the window. IsOpen is just a bool flag the next render
            // tick reads — safe to set from any thread.
            mainWindow.IsOpen = true;

            // One short ASCII confirmation line. This is the ONLY ChatGui.Print
            // this command makes, and it does not iterate a Split() result, so
            // it cannot reproduce the multi-line empty-entry path that caused
            // the v0.6.5 crash. Wrapped in try/catch defensively anyway.
            try
            {
                DalamudServices.ChatGui.Print(
                    "[Tonberry Tactics] Diagnostics copied to clipboard. " +
                    "Opening the Tonberry Tactics window — see the Diagnostics tab for live state.");
            }
            catch (Exception printEx)
            {
                DalamudServices.Log.Warning(printEx,
                    "OnInfoCommand: confirmation ChatGui.Print threw; ignoring.");
            }
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "OnInfoCommand: BuildGoblinInfoString or dispatch threw.");
            try
            {
                DalamudServices.ChatGui.PrintError($"[Tonberry Tactics] /ttinfo failed: {ex.Message}");
            }
            catch
            {
                // Already in a failure path; nothing to do.
            }
        }
    }

    /// <summary>
    /// v0.4.7: /goblinimport command. Reads a GG-PLAN:v1: string from the
    /// system clipboard (or from <paramref name="args"/> if the user passed
    /// the wire string inline), validates it via GearsetImporter, and
    /// persists the resulting plan into Configuration.JobPlans under
    /// PlanMode.Imported. Surfaces results to chat.
    ///
    /// <para>
    /// Status as of v0.4.7 scaffold commit: the validation flow exists in
    /// GearsetImporter (prefix check, base64 decode, JSON parse, schema
    /// version, emitter identity), but the clipboard read and the
    /// Configuration persist steps are still TODO. This handler is
    /// registered and reachable; it just reports honestly that the wiring
    /// isn't complete yet. Replace this comment when the persist body lands.
    /// </para>
    /// </summary>
    private void OnImportCommand(string command, string args)
    {
        try
        {
            // If the user passed the wire string inline (e.g. /ttimport GG-PLAN:v1:abc),
            // route to ImportFromString; otherwise pull from clipboard.
            var result = !string.IsNullOrWhiteSpace(args)
                ? Importer.ImportFromString(args)
                : Importer.ImportFromClipboard();

            if (!result.Success)
            {
                DalamudServices.ChatGui.PrintError(
                    $"[Tonberry Tactics] Import failed: {result.ErrorMessage}");
                return;
            }

            // v0.6.5 — Scaffold notice updated to reflect actual state.
            // The previous "next build" promise rode through v0.4.7 → v0.6.4
            // (seven releases) without bodies being filled in. The honest
            // version below ships in v0.6.5; the real persistence + Plan-tab
            // checklist workflow lands in v0.6.7 ("Round-trip closed").
            DalamudServices.ChatGui.Print(
                "[Tonberry Tactics] Plan parsed successfully. " +
                $"({result.Payload?.Melds.Count ?? 0} meld(s) recommended for " +
                $"{result.Payload?.SourceCharacter.JobAbbreviation ?? "?"}.) " +
                "In-game apply checklist + plan persistence ships in v0.6.7. " +
                "For now: visit tonberrytactics.pages.dev to view the plan.");

            foreach (var warning in result.Warnings)
            {
                DalamudServices.ChatGui.Print($"[Tonberry Tactics] Warning: {warning}");
            }
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "OnImportCommand: importer threw.");
            DalamudServices.ChatGui.PrintError($"[Tonberry Tactics] /ttimport failed: {ex.Message}");
        }
    }

    /// <summary>
    /// v0.4.6: build a human-readable, copy-paste-friendly diagnostic block.
    /// Used by both the /goblininfo slash command and the Diagnostics tab's
    /// "Copy to clipboard" button.
    ///
    /// <para>
    /// Output shape (~15 lines): plugin version + player + job + iLvl,
    /// then every field of the StatusPanelInjector.DiagnosticSnapshot, then
    /// a trailing instruction line directing users to attach their /xllog
    /// if reporting a bug. Wrapped in '─────' separator lines so it's
    /// visually distinct in chat or in a GitHub issue.
    /// </para>
    /// </summary>
    public string BuildGoblinInfoString()
    {
        var sb = new StringBuilder();
        var diag = StatusPanel.GetDiagnostics();

        // v0.6.6 — version resolution unified with the in-game header pill
        // via UI.MainWindow.ResolveVersion(). Previously this function had
        // its own copy of the version-formatter logic (Major.Minor.Build +
        // Revision-if-non-zero), which meant /ttinfo showed AssemblyVersion-
        // formatted numbers while the header pill could be showing the
        // AssemblyInformationalVersion string (e.g. v0.6.5.3a's "0.6.5.3a"
        // pill vs /ttinfo's "0.6.6"). Now both use the same resolver:
        // InformationalVersion preferred, AssemblyVersion-formatting fallback.
        string v = UI.MainWindow.ResolveVersion();

        // v0.6.5.2 — branding sweep: header now reads "Tonberry Tactics /ttinfo"
        // (was "GearGoblin /goblininfo" — a miss from the v0.6.5 chat-message
        // branding sweep that this diagnostic block was never updated to match).
        sb.AppendLine("───── Tonberry Tactics /ttinfo ─────");
        sb.AppendLine($"Plugin version       : v{v}");

        var player = DalamudServices.ObjectTable.LocalPlayer;
        if (player is not null)
        {
            var job = player.ClassJob.Value.Abbreviation.ExtractText();
            sb.AppendLine($"Player               : {player.Name} — {job} Lv {player.Level}");
        }
        else
        {
            sb.AppendLine("Player               : (not logged in)");
        }

        sb.AppendLine("──── Injector state ────");
        sb.AppendLine($"Character panel attached : {(diag.PanelAttached ? "yes" : "no")}");
        sb.AppendLine($"CPR detected             : {(diag.CprDetected ? "yes" : "no")}");
        sb.AppendLine($"Derivations enabled      : {(diag.DerivationsEnabled ? "yes" : "no")}");
        sb.AppendLine($"Advisor section injected : {(diag.AdvisorSectionPresent ? "yes" : "no")}");
        sb.AppendLine($"Advisor recommendations  : {diag.AdvisorRecCount}");
        sb.AppendLine($"Advisor empty-state      : {(diag.AdvisorEmptyState ? "yes (all materia optimal)" : "no")}");
        sb.AppendLine($"Advisor errored          : {(diag.AdvisorErrored ? "YES — check /xllog" : "no")}");
        sb.AppendLine($"Outer-addon height grew  : {diag.InjectedHeightPx} px");
        sb.AppendLine($"Last inject result       : {diag.LastInjectResult}");
        sb.AppendLine($"Last inject time (UTC)   : {(diag.LastInjectTime == default ? "—" : diag.LastInjectTime.ToString("HH:mm:ss"))}");
        sb.AppendLine($"Last update tick (UTC)   : {(diag.LastUpdateTime == default ? "—" : diag.LastUpdateTime.ToString("HH:mm:ss"))}");
        sb.AppendLine("─────────────────────────────");

        // v0.6.5.2 — the previous footer hardcoded "StatusPanelInjector v0.4.6"
        // as the /xllog search term. Two problems: (1) the v0.4.6 has been
        // wrong for every release since v0.4.7, since log lines now reference
        // whatever the current version is; (2) "StatusPanelInjector" alone is
        // the right search prefix that matches log lines across versions.
        sb.AppendLine("If reporting a bug, attach the relevant /xllog lines (search 'StatusPanelInjector' or 'BrandResources').");

        return sb.ToString();
    }

    /// <summary>
    /// Toggle the standalone /goblin window. Public so the v0.4.0
    /// StatusPanelInjector can invoke it from the in-addon Materia Advisor
    /// footer's click handler.
    /// </summary>
    public void ToggleMain() => mainWindow.Toggle();

    private void DrawUI() => WindowSystem.Draw();
}


