extern alias imgui;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using p4rpc.trip2.debugui.Interfaces;
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

[StructLayout(LayoutKind.Sequential)]
public unsafe struct Array<T> where T : unmanaged
{
    public T* Entries;
    public nint Length;
}

[StructLayout(LayoutKind.Sequential)]
public struct WindowState(nint title, int hash)
{
    public nint Title = title;
    public int Hash = hash;
}

public static unsafe class Trip2DebugGui
{
    const string __DllName = "trip2_debug_gui";

    private static string? Namespace;
    private static IModLoader? _modLoader;
    private static IModConfig? _modConfig;

    internal static Dictionary<int, IGUIWindow> Windows { get; } = new();
    
    // Mod functions
        
    [DllImport(__DllName, EntryPoint = "new_frame_ui", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern byte new_frame_ui();

    [DllImport(__DllName, EntryPoint = "set_set_imgui_context", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern void set_set_imgui_context(delegate* unmanaged[Stdcall]<nint, nint, nint, nint, void> callback);
    
    [DllImport(__DllName, EntryPoint = "check_imgui_running", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern byte check_imgui_running();
    
    [DllImport(__DllName, EntryPoint = "get_deltatime", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern float get_deltatime();
    
    [DllImport(__DllName, EntryPoint = "set_window_states", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern void set_window_states(Array<WindowState>* entries);

    [DllImport(__DllName, EntryPoint = "set_draw_window", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern void set_draw_window(delegate* unmanaged[Stdcall]<int, void> callback);
    
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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    public static void GetImguiContext(nint context, nint alloc_func, nint free_func, nint user_data)
    {
        ImGui.__Internal.SetCurrentContext(context);
        ImGui.__Internal.SetAllocatorFunctions(alloc_func, free_func, user_data);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    public static void DrawWindow(int hashId)
    {
        if (Windows.TryGetValue(hashId, out var Window))
            Window.DrawContents();
    }

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
        set_set_imgui_context(&GetImguiContext);
        set_draw_window(&DrawWindow);
    }
}

public static unsafe class RiriModRuntime
{
    const string __DllName = "riri_mod_runtime_reloaded";

    [DllImport(__DllName, EntryPoint = "get_executable_hash_ex", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern ulong get_executable_hash_ex();   
}