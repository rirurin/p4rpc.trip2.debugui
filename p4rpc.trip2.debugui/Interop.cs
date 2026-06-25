using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Reloaded.Mod.Interfaces;
using RyoTune.Reloaded;

namespace p4rpc.trip2.debugui;

public enum RustLogLevel : int {
    Verbose,
    Debug,
    Information,
    Warning,
    Error
}

public static unsafe class Trip2DebugGui
{
    const string __DllName = "trip2_debug_gui";

    private static string? Namespace;
    private static IModLoader? _modLoader;
    private static IModConfig? _modConfig;
    
    // Mod functions
        
    [DllImport(__DllName, EntryPoint = "fengineloop_tick", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern byte fengineloop_tick();
    
    // riri-mod-tools functions
    
    [DllImport(__DllName, EntryPoint = "set_current_process", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern void set_current_process();
    
    [DllImport(__DllName, EntryPoint = "set_reloaded_logger", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern nint set_reloaded_logger(delegate* unmanaged[Stdcall]<nint, nint, int, int, byte, void> offset);
    
    [DllImport(__DllName, EntryPoint = "set_reloaded_logger_newline", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern nint set_reloaded_logger_newline(delegate* unmanaged[Stdcall]<nint, nint, int, int, byte, void> offset);
    
    [DllImport(__DllName, EntryPoint = "set_get_directory_for_mod", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern nint set_get_directory_for_mod(delegate* unmanaged[Stdcall]<nint> offset);
    
    [DllImport(__DllName, EntryPoint = "set_free_csharp_string", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern nint set_free_csharp_string(delegate* unmanaged[Stdcall]<nint, void> offset);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    public static void ReloadedLoggerWrite(nint p, nint len, int color, int level, byte showPrefix)
        => LoggerWrite(Marshal.PtrToStringUTF8(p, (int)len), (RustLogLevel)level);

    [UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
    public static void ReloadedLoggerWriteLine(nint p, nint len, int color, int level, byte showPrefix)
         => LoggerWrite(Marshal.PtrToStringUTF8(p, (int)len), (RustLogLevel)level);

    private delegate void PrintRyoTune(string message, bool useAsync);
        
    private static PrintRyoTune PrintDelegate(RustLogLevel level) => level switch 
    {
        RustLogLevel.Debug => Log.Debug,
        RustLogLevel.Information => Log.Information,
        RustLogLevel.Warning => Log.Warning,
        RustLogLevel.Error => Log.Error,
        _ => Log.Verbose,
    };

    private static void LoggerWrite(string text, RustLogLevel level) => PrintDelegate(level)(text, false);
    
    [UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
    public static unsafe nint GetDirectoryForMod()
    {
        var ModDirectory = _modLoader!.GetDirectoryForModId(_modConfig!.ModId);
        return Marshal.StringToHGlobalUni(ModDirectory);
    }
    
    [UnmanagedCallersOnly(CallConvs = [ typeof(CallConvStdcall) ])]
    public static void FreeCSharpString(nint p) => Marshal.FreeHGlobal(p);

    internal static void Initialize(IModLoader modLoader, IModConfig modConfig)
    {
        _modLoader = modLoader;
        _modConfig = modConfig;
        Namespace = typeof(Trip2DebugGui).Namespace;
        set_current_process();
        set_reloaded_logger(&ReloadedLoggerWrite);
        set_reloaded_logger_newline(&ReloadedLoggerWriteLine);
        set_get_directory_for_mod(&GetDirectoryForMod);
        set_free_csharp_string(&FreeCSharpString);
    }
}

public static unsafe class RiriModRuntime
{
    const string __DllName = "riri_mod_runtime_reloaded";

    [DllImport(__DllName, EntryPoint = "get_executable_hash_ex", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern ulong get_executable_hash_ex();   
}