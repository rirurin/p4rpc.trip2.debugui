extern alias imgui;
using System.Numerics;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using p4rpc.trip2.debugui.Interfaces;
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
public struct WindowState(nint title, ulong hash, Vector2 size, Vector2 position, bool canClose)
{
    public nint Title = title;
    public ulong Hash = hash;
    public Vector2 Size = size;
    public Vector2 Position = position;
    public bool CanClose = canClose;
}

[StructLayout(LayoutKind.Sequential)]
public struct AppState(nint name, uint hash)
{
    public nint Name = name;
    public uint Hash = hash;
}

[StructLayout(LayoutKind.Sequential)]
public struct ButtonState(nint name, ulong hash)
{
    public nint Name = name;
    public ulong Hash = hash;
}

[StructLayout(LayoutKind.Sequential)]
public struct InteropState
{
    public Array<WindowState> Windows;
    public Array<AppState> Apps;
    public Array<ButtonState> Buttons;
}

public static unsafe class Trip2DebugGui
{
    const string __DllName = "trip2_debug_gui";

    private static string? Namespace;
    private static Context? _context;

    internal static Dictionary<uint, IGUIApp> Apps { get; } = new();
    internal static Dictionary<ulong, Action> Buttons { get; } = new();
    internal static Dictionary<ulong, IGUIWindow> Windows { get; } = new();
    
    // Mod functions
        
    [DllImport(__DllName, EntryPoint = "new_frame_ui", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern byte new_frame_ui();

    [DllImport(__DllName, EntryPoint = "set_set_imgui_context", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern void set_set_imgui_context(delegate* unmanaged[Stdcall]<nint, nint, nint, nint, void> callback);
    
    [DllImport(__DllName, EntryPoint = "check_imgui_running", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern byte check_imgui_running();
    
    [DllImport(__DllName, EntryPoint = "get_deltatime", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern float get_deltatime();

    [DllImport(__DllName, EntryPoint = "set_draw_window", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern void set_draw_window(delegate* unmanaged[Stdcall]<ulong, void> callback);
    
    [DllImport(__DllName, EntryPoint = "get_surface_size", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern Vector2 get_surface_size();
    
    [DllImport(__DllName, EntryPoint = "set_get_window_initial_size", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern void set_get_window_initial_size(delegate* unmanaged[Stdcall]<ulong, Vector2> callback);
    
    [DllImport(__DllName, EntryPoint = "set_get_window_initial_pos", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern void set_get_window_initial_pos(delegate* unmanaged[Stdcall]<ulong, Vector2> callback);
    
    [DllImport(__DllName, EntryPoint = "set_remove_window", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern void set_remove_window(delegate* unmanaged[Stdcall]<ulong, void> callback);
    
    [DllImport(__DllName, EntryPoint = "set_get_branch_version", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern nint set_get_branch_version(delegate* unmanaged[Stdcall]<nint> offset);
    
    [DllImport(__DllName, EntryPoint = "set_interop_state", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern void set_interop_state(InteropState* state);
    
    [DllImport(__DllName, EntryPoint = "set_button_action", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern void set_button_action(delegate* unmanaged[Stdcall]<ulong, void> callback);
    
    [DllImport(__DllName, EntryPoint = "add_font_from_path", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern nint add_font_from_path(nint path, /*ref uint glyphRange,*/ float fontSize);
    
    [DllImport(__DllName, EntryPoint = "get_font", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern nint get_font(nint name);
    
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
        var ModDirectory = _context!.ModLoader.GetDirectoryForModId(_context!.ModConfig.ModId);
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
    public static void DrawWindow(ulong windowHash)
    {
        if (Windows.TryGetValue(windowHash, out var Window))
            Window.DrawContents();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    public static Vector2 GetWindowInitialSize(ulong windowHash)
         => Windows.TryGetValue(windowHash, out var Window) ? Window.StartSize : Vector2.Zero;
    
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    public static Vector2 GetWindowInitialPos(ulong windowHash)
        => Windows.TryGetValue(windowHash, out var Window) ? Window.StartPos : Vector2.Zero;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    public static void RemoveWindow(ulong windowHash)
    {
        if (!Windows.TryGetValue(windowHash, out var Window))
        {
            Log.Warning($"Could not remove window 0x{windowHash:x} - Window object was not found");
            return;
        }
        if (!Window.Close())
            Log.Warning($"Could not close window {Window.Title}");
    }
    
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    public static nint GetBranchVersion()
        => Marshal.StringToHGlobalUni(_context!.UnrealEssentials.GetEngineVersion());

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    public static void ButtonAction(ulong hash)
    {
        if (!Buttons.TryGetValue(hash, out var Action)) return;
        Action();
    }

    internal static void Initialize(Context context)
    {
        _context = context;
        Namespace = typeof(Trip2DebugGui).Namespace;
        set_current_process();
        set_reloaded_logger(&ReloadedLoggerWrite);
        set_reloaded_logger_newline(&ReloadedLoggerWriteLine);
        set_get_directory_for_mod(&GetDirectoryForMod);
        set_free_csharp_string(&FreeCSharpString);
        set_set_imgui_context(&GetImguiContext);
        set_draw_window(&DrawWindow);
        set_get_window_initial_size(&GetWindowInitialSize);
        set_get_window_initial_pos(&GetWindowInitialPos);
        set_remove_window(&RemoveWindow);
        set_get_branch_version(&GetBranchVersion);
        set_button_action(&ButtonAction);
    }
}

public static unsafe class RiriModRuntime
{
    const string __DllName = "riri_mod_runtime_reloaded";

    [DllImport(__DllName, EntryPoint = "get_executable_hash_ex", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern ulong get_executable_hash_ex();   
}