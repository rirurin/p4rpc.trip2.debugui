using System.Runtime.InteropServices;
using p4rpc.trip2.debugui.Interfaces;
using Reloaded.Hooks.Definitions.X64;
using riri.yamlscans.ReloadedII;
using RyoTune.Reloaded;

namespace p4rpc.trip2.debugui;

public class Tick
{
    private Context Context;
    private GuiState GuiState;
    private SHFunction2<FEngineLoop_Tick> _FEngineLoop_Tick;
    
    [Function(CallingConventions.Microsoft)]
    private delegate byte FEngineLoop_Tick(nint self);

    private void NewFrameActive()
    {
        Array<WindowState> WindowStates;
        List<WindowState> WindowStatesList = [];
        foreach (var App in GuiState.Programs.Values)
        {
            var AppHash = App.Name.GetHashCode();
            Trip2DebugGui.Apps.Add(AppHash, App);
            foreach (var Window in App.Windows)
            {
                var WindowHash = ((long)AppHash << 0x20) | Window.Title.GetHashCode();
                Trip2DebugGui.Windows.Add(WindowHash, Window);
                WindowStatesList.Add(new WindowState(
                    Marshal.StringToHGlobalUni(Window.Title), 
                    WindowHash, 
                    Window.StartSize, 
                    Window.StartPos
                    ));
            }
        }
        var WindowStatesArray = WindowStatesList.ToArray();
        unsafe
        {
            fixed (WindowState* pWindowStatesArray = WindowStatesArray)
            {
                WindowStates.Entries = pWindowStatesArray;
                WindowStates.Length = WindowStatesArray.Length;
                Trip2DebugGui.set_window_states(&WindowStates);
                Trip2DebugGui.new_frame_ui();
                Trip2DebugGui.Windows.Clear();
                Trip2DebugGui.Apps.Clear();
            }   
        }
        var dt = Trip2DebugGui.get_deltatime();
        foreach (var Program in GuiState.Programs.Values)
            Program.Tick(dt);
    }
    

    private static void NewFrameInactive() => Trip2DebugGui.new_frame_ui();

    private byte FEngineLoop_TickImpl(nint self)
    {
        if (Trip2DebugGui.check_imgui_running() != 0) NewFrameActive();
        else NewFrameInactive();
        return _FEngineLoop_Tick.Hook!.OriginalFunction(self);
    }

    public Tick(Context context, GuiState guiState)
    {
        Context = context;
        GuiState = guiState;
        _FEngineLoop_Tick = new(FEngineLoop_TickImpl);
    }
}
