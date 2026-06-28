using System.ComponentModel;
using p4rpc.trip2.debug.testtoolkit.Template.Configuration;
using Reloaded.Mod.Interfaces.Structs;

namespace p4rpc.trip2.debug.testtoolkit.Configuration;

public class Config : Configurable<Config>
{
}

/// <summary>
/// Allows you to override certain aspects of the configuration creation process (e.g. create multiple configurations).
/// Override elements in <see cref="ConfiguratorMixinBase"/> for finer control.
/// </summary>
public class ConfiguratorMixin : ConfiguratorMixinBase
{
    // 
}