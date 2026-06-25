using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;
using p4rpc.trip2.debugui.Template;
using p4rpc.trip2.debugui.Configuration;
using Reloaded.Hooks.Definitions.X64;
using riri.yamlscans.ReloadedII;
using RyoTune.Reloaded;
using SharedScans.Interfaces;

namespace p4rpc.trip2.debugui;

public class Mod : ModBase
{
    private readonly IModLoader _modLoader;
    private readonly IReloadedHooks? _hooks;
    private readonly ILogger _logger;
    private readonly IMod _owner;
    private Config _configuration;
    private readonly IModConfig _modConfig;

    private Context _context;
    private Initialize _initialize;

    public Mod(ModContext context)
    {
        _modLoader = context.ModLoader;
        _hooks = context.Hooks;
        _logger = context.Logger;
        _owner = context.Owner;
        _configuration = context.Configuration;
        _modConfig = context.ModConfig;
        
        Project.Initialize(_modConfig, _modLoader, _logger, true);
        Log.LogLevel = _configuration.LogLevel;
        YamlScans.Initialize(_modConfig, _modLoader);
        Trip2DebugGui.Initialize(_modLoader, _modConfig);

        _context = new(_modLoader, _hooks!, _modConfig, YamlScans.GetDependency<ISharedScans>());
        _initialize = new(_context);
    }

    #region Standard Overrides

    public override void ConfigurationUpdated(Config configuration)
    {
        _configuration = configuration;
        _logger.WriteLine($"[{_modConfig.ModId}] Config Updated: Applying");
    }

    #endregion

    #region For Exports, Serialization etc.

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public Mod()
    {
    }
#pragma warning restore CS8618

    #endregion
}