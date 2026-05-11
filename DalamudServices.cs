using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace GearGoblin;

/// <summary>
/// Static container for all Dalamud-injected services. Populated once at plugin
/// load via <see cref="Plugin"/>'s constructor; everything else reads from here.
/// </summary>
public class DalamudServices
{
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static ICommandManager       CommandManager   { get; private set; } = null!;
    [PluginService] public static IClientState          ClientState      { get; private set; } = null!;
    [PluginService] public static IObjectTable          ObjectTable      { get; private set; } = null!;
    [PluginService] public static IDataManager          DataManager      { get; private set; } = null!;
    [PluginService] public static IGameInventory        GameInventory    { get; private set; } = null!;
    [PluginService] public static IFramework            Framework        { get; private set; } = null!;
    [PluginService] public static IPluginLog            Log              { get; private set; } = null!;
    [PluginService] public static IChatGui              ChatGui          { get; private set; } = null!;
    [PluginService] public static ITextureProvider      TextureProvider  { get; private set; } = null!;

    // v0.4.0: native AtkNode injection into the CharacterStatus addon.
    [PluginService] public static IAddonLifecycle       AddonLifecycle    { get; private set; } = null!;
    [PluginService] public static IGameGui              GameGui           { get; private set; } = null!;
    [PluginService] public static IAddonEventManager    AddonEventManager { get; private set; } = null!;
}
