using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using p4rpc.trip2.debugui.Template.Configuration;
using RyoTune.Reloaded;

namespace p4rpc.trip2.debugui.Configuration;

public class Config : Configurable<Config>
{
    [DisplayName("Log Level")]
    [Category("Debug")]
    [Display(Order = 0)]
    [DefaultValue(LogLevel.Information)]
    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    [DisplayName("Theme")]
    [Category("Appearence")]
    [Display(Order = 1)]
    [DefaultValue("Default")]
    public string ThemeName { get; set; } = "Default";

    [DisplayName("Window Size")]
    [Category("Appearence")]
    [Description("Sets the size of the window on startup")]
    [Display(Order = 2)]
    public PhysicalSize WindowSize { get; set; } = new();
    
    [DisplayName("Window Position")]
    [Category("Appearence")]
    [Description("Sets the top-left position of the window")]
    [Display(Order = 3)]
    public PhysicalPosition WindowPos { get; set; } = new();
}

/// <summary>
/// Allows you to override certain aspects of the configuration creation process (e.g. create multiple configurations).
/// Override elements in <see cref="ConfiguratorMixinBase"/> for finer control.
/// </summary>
public class ConfiguratorMixin : ConfiguratorMixinBase
{
    // 
}

public class ConfigIntVector2(int x, int y)
{
    public int X { get; set; } = x;
    public int Y { get; set; } = y;
    
    public int ToInt() => X & 0xffff | (Y & 0xffff) << 0x10;

    public ConfigIntVector2(int packed) : this(packed & 0xffff, (packed >> 0x10) & 0xffff) {}

    public override string ToString() => $"<{X}, {Y}>";
}

public class PhysicalSize : ConfigIntVector2
{
    public PhysicalSize(int x, int y) : base(x, y) {}
    
    public PhysicalSize(int packed) : base(packed) {}
    
    public PhysicalSize(): this(1920, 1080) {}
}

public class PhysicalPosition : ConfigIntVector2
{
    public PhysicalPosition(int x, int y) : base(x, y) {}
    
    public PhysicalPosition(int packed) : base(packed) {}
    
    public PhysicalPosition(): this(100, 100) {}
}