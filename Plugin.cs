using System;
using System.Collections.Generic;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using GearGoblin.Services;
using GearGoblin.UI;
using Microsoft.Extensions.DependencyInjection;

namespace GearGoblin;

/// <summary>
/// Dalamud entry point for the in-game Tonberry Tactics client.
/// The assembly/internal name remains GearGoblin for configuration compatibility.
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Tonberry Tactics";

    private const string CommandName       = "/tt";
    private const string ExportCommandName = "/ttexport";
    private const string InfoCommandName   = "/ttinfo";
    private const string ImportCommandName = "/ttimport";

    // Compatibility aliases. Hidden from command help but kept for old muscle memory.
    private const string LegacyCommandName       = "/goblin";
    private const string LegacyExportCommandName = "/goblinexport";
    private const string LegacyInfoCommandName   = "/goblininfo";
    private const string LegacyImportCommandName = "/goblinimport";

    private const string TacticsCommandName       = "/tactics";
    private const string TacticsExportCommandName = "/tacticsexport";
    private const string TacticsInfoCommandName   = "/tacticsinfo";
    private const string TacticsImportCommandName = "/tacticsimport";

    public IConfigurationService ConfigService { get; }
    public WindowSystem WindowSystem { get; } = new("GearGoblin");
    public IServiceProvider Provider { get; }

    public IInventoryReader Inventory { get; }
    public IGearsetExporter Exporter { get; }
    public IGearsetImporter Importer { get; }
    public IStatusPanelInjector StatusPanel { get; }
    public BrandResources Brand { get; }
    public Theme.FontAtlasManager Fonts { get; }

    private readonly MainWindow mainWindow;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<DalamudServices>();

        Provider = ServiceContainer.CreateProvider(this);
        ConfigService = Provider.GetRequiredService<IConfigurationService>();
        Inventory   = Provider.GetRequiredService<IInventoryReader>();
        Exporter    = Provider.GetRequiredService<IGearsetExporter>();
        Importer    = Provider.GetRequiredService<IGearsetImporter>();
        StatusPanel = Provider.GetRequiredService<IStatusPanelInjector>();
        Brand       = new BrandResources();
        Fonts       = new Theme.FontAtlasManager(pluginInterface);

        mainWindow = new MainWindow(this);
        WindowSystem.AddWindow(mainWindow);

        DalamudServices.PluginInterface.UiBuilder.Draw         += DrawUI;
        DalamudServices.PluginInterface.UiBuilder.OpenConfigUi += ToggleMain;
        DalamudServices.PluginInterface.UiBuilder.OpenMainUi   += ToggleMain;

        RegisterCommands();

        DalamudServices.Log.Info(
            $"Tonberry Tactics v{GetType().Assembly.GetName().Version} loaded (InternalName=GearGoblin)."
        );
    }

    public void Dispose()
    {
        StatusPanel.Dispose();
        Fonts.Dispose();
        Brand.Dispose();

        DalamudServices.PluginInterface.UiBuilder.Draw         -= DrawUI;
        DalamudServices.PluginInterface.UiBuilder.OpenConfigUi -= ToggleMain;
        DalamudServices.PluginInterface.UiBuilder.OpenMainUi   -= ToggleMain;

        RemoveCommands();

        WindowSystem.RemoveAllWindows();
        mainWindow.Dispose();

        if (Provider is IDisposable disposableProvider)
            disposableProvider.Dispose();
    }

    private void RegisterCommands()
    {
        DalamudServices.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Tonberry Tactics. Usage: /tt"
        });
        DalamudServices.CommandManager.AddHandler(ExportCommandName, new CommandInfo(OnExportCommand)
        {
            HelpMessage = "Export equipped gear and current stats to the Tonberry Tactics web companion."
        });
        DalamudServices.CommandManager.AddHandler(InfoCommandName, new CommandInfo(OnInfoCommand)
        {
            HelpMessage = "Copy Tonberry Tactics diagnostics and open the Diagnostics tab."
        });
        DalamudServices.CommandManager.AddHandler(ImportCommandName, new CommandInfo(OnImportCommand)
        {
            HelpMessage = "Import a GG-PLAN:v1 plan from the clipboard or inline text."
        });

        AddHiddenAlias(TacticsCommandName, OnCommand, "Open Tonberry Tactics.");
        AddHiddenAlias(TacticsExportCommandName, OnExportCommand, "Export equipped gear.");
        AddHiddenAlias(TacticsInfoCommandName, OnInfoCommand, "Copy Tonberry Tactics diagnostics.");
        AddHiddenAlias(TacticsImportCommandName, OnImportCommand, "Import a Tonberry Tactics plan.");

        AddHiddenAlias(LegacyCommandName, OnCommand, "Open Tonberry Tactics (legacy alias).");
        AddHiddenAlias(LegacyExportCommandName, OnExportCommand, "Export equipped gear (legacy alias).");
        AddHiddenAlias(LegacyInfoCommandName, OnInfoCommand, "Copy diagnostics (legacy alias).");
        AddHiddenAlias(LegacyImportCommandName, OnImportCommand, "Import a plan (legacy alias).");
    }

    private static void AddHiddenAlias(string command, IReadOnlyCommandInfo.HandlerDelegate handler, string help)
    {
        DalamudServices.CommandManager.AddHandler(command, new CommandInfo(handler)
        {
            HelpMessage = help,
            ShowInHelp = false,
        });
    }

    private static void RemoveCommands()
    {
        string[] commands =
        {
            CommandName, ExportCommandName, InfoCommandName, ImportCommandName,
            TacticsCommandName, TacticsExportCommandName, TacticsInfoCommandName, TacticsImportCommandName,
            LegacyCommandName, LegacyExportCommandName, LegacyInfoCommandName, LegacyImportCommandName,
        };

        foreach (var command in commands)
            DalamudServices.CommandManager.RemoveHandler(command);
    }

    private void OnCommand(string command, string args) => ToggleMain();

    private void OnExportCommand(string command, string args) => Exporter.ExportToClipboard();

    private void OnInfoCommand(string command, string args)
    {
        try
        {
            var info = BuildGoblinInfoString();

            DalamudServices.Framework.RunOnFrameworkThread(() =>
            {
                try
                {
                    ImGui.SetClipboardText(info);
                }
                catch (Exception ex)
                {
                    DalamudServices.Log.Warning(ex,
                        "Unable to write /ttinfo payload to the ImGui clipboard.");
                }
            });

            mainWindow.IsOpen = true;

            try
            {
                DalamudServices.ChatGui.Print(
                    "[Tonberry Tactics] Diagnostics copied to clipboard. Opening Diagnostics."
                );
            }
            catch (Exception ex)
            {
                DalamudServices.Log.Warning(ex, "Unable to print /ttinfo confirmation to chat.");
            }
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "/ttinfo failed.");
            try
            {
                DalamudServices.ChatGui.PrintError($"[Tonberry Tactics] /ttinfo failed: {ex.Message}");
            }
            catch
            {
                // Already on the error path; do not let chat failure escape.
            }
        }
    }

    private void OnImportCommand(string command, string args)
    {
        try
        {
            var result = !string.IsNullOrWhiteSpace(args)
                ? Importer.ImportFromString(args)
                : Importer.ImportFromClipboard();

            if (!result.Success || result.Payload == null || result.RawJson == null)
            {
                DalamudServices.ChatGui.PrintError(
                    $"[Tonberry Tactics] Import failed: {result.ErrorMessage ?? "Unknown error"}"
                );
                return;
            }

            // Dalamud API 15 exposes the stable local-character content ID through
            // IPlayerState. Never collapse per-character plans into a synthetic key.
            if (!DalamudServices.PlayerState.IsLoaded || DalamudServices.PlayerState.ContentId == 0)
            {
                DalamudServices.ChatGui.PrintError(
                    "[Tonberry Tactics] Import failed: local character identity is not available yet."
                );
                return;
            }

            ulong contentId = DalamudServices.PlayerState.ContentId;
            uint jobId = result.Payload.SourceCharacter.Job;
            var config = ConfigService.Current;

            if (!config.JobPlans.TryGetValue(contentId, out var characterPlans))
            {
                characterPlans = new Dictionary<uint, JobPlanData>();
                config.JobPlans[contentId] = characterPlans;
            }

            if (!characterPlans.TryGetValue(jobId, out var jobData))
            {
                jobData = new JobPlanData();
                characterPlans[jobId] = jobData;
            }

            jobData.Mode = PlanMode.Imported;
            jobData.ImportedPlanJson = result.RawJson;
            jobData.ImportedAt = DateTime.UtcNow;
            jobData.LastUpdated = DateTime.UtcNow;

            jobData.MeldCompletion.Clear();
            for (int i = 0; i < result.Payload.Melds.Count; i++)
                jobData.MeldCompletion.Add(false);

            ConfigService.Save();

            DalamudServices.ChatGui.Print(
                "[Tonberry Tactics] Plan imported and set active. " +
                $"{result.Payload.Melds.Count} meld(s) for {result.Payload.SourceCharacter.JobAbbreviation}."
            );

            foreach (var warning in result.Warnings)
                DalamudServices.ChatGui.Print($"[Tonberry Tactics] Warning: {warning}");
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "/ttimport failed.");
            DalamudServices.ChatGui.PrintError($"[Tonberry Tactics] /ttimport failed: {ex.Message}");
        }
    }

    /// <summary>Build the diagnostics payload used by /ttinfo and the Diagnostics tab.</summary>
    public string BuildGoblinInfoString()
    {
        var sb = new StringBuilder();
        var diag = StatusPanel.GetDiagnostics();
        string version = MainWindow.ResolveVersion();

        sb.AppendLine("----- Tonberry Tactics /ttinfo -----");
        sb.AppendLine($"Plugin version       : v{version}");

        var player = DalamudServices.ObjectTable.LocalPlayer;
        if (player is not null)
        {
            var job = player.ClassJob.Value.Abbreviation.ExtractText();
            sb.AppendLine($"Player               : {player.Name} - {job} Lv {player.Level}");
            sb.AppendLine($"Content ID available : {(DalamudServices.PlayerState.ContentId != 0 ? "yes" : "no")}");

            var equipped = Inventory.ReadEquipped();
            sb.AppendLine($"---- Equipped ({equipped.Count}) ----");
            foreach (var p in equipped)
            {
                sb.AppendLine(
                    $"{p.Slot,-9}: i{p.ItemLevel} icon={p.IconId} hq={(p.IsHighQuality ? "Y" : "n")} - {p.Name}"
                );
            }
        }
        else
        {
            sb.AppendLine("Player               : (not logged in)");
        }

        sb.AppendLine("---- Injector state ----");
        sb.AppendLine($"Character panel attached : {(diag.PanelAttached ? "yes" : "no")}");
        sb.AppendLine($"CPR detected             : {(diag.CprDetected ? "yes" : "no")}");
        sb.AppendLine($"Derivations enabled      : {(diag.DerivationsEnabled ? "yes" : "no")}");
        sb.AppendLine($"Advisor section injected : {(diag.AdvisorSectionPresent ? "yes" : "no")}");
        sb.AppendLine($"Advisor recommendations  : {diag.AdvisorRecCount}");
        sb.AppendLine($"Advisor empty-state      : {(diag.AdvisorEmptyState ? "yes (all materia optimal)" : "no")}");
        sb.AppendLine($"Advisor errored          : {(diag.AdvisorErrored ? "YES - check /xllog" : "no")}");
        sb.AppendLine($"Outer-addon height grew  : {diag.InjectedHeightPx} px");
        sb.AppendLine($"Last inject result       : {diag.LastInjectResult}");
        sb.AppendLine($"Last inject time (UTC)   : {(diag.LastInjectTime == default ? "-" : diag.LastInjectTime.ToString("HH:mm:ss"))}");
        sb.AppendLine($"Last update tick (UTC)   : {(diag.LastUpdateTime == default ? "-" : diag.LastUpdateTime.ToString("HH:mm:ss"))}");
        sb.AppendLine("-----------------------------");
        sb.AppendLine("If reporting a bug, attach relevant /xllog lines (search 'StatusPanelInjector' or 'BrandResources').");

        return sb.ToString();
    }

    public void ToggleMain() => mainWindow.Toggle();

    private void DrawUI() => WindowSystem.Draw();
}
