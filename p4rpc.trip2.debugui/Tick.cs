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
        InteropState Interop;
        List<WindowState> WindowStatesList = [];
        List<AppState> AppStatesList = [];
        List<ButtonState> ButtonStatesList = [];
        
        foreach (var App in GuiState.Programs.Values)
        {
            var AppHash = (uint)App.Name.GetHashCode();
            Trip2DebugGui.Apps.Add(AppHash, App);
            AppStatesList.Add(new AppState(
                Marshal.StringToHGlobalUni(App.Name), AppHash));
            foreach (var (Name, Action) in App.Buttons)
            {
                var ButtonHash = ((ulong)AppHash << 0x20) | (uint)Name.GetHashCode();
                Trip2DebugGui.Buttons.Add(ButtonHash, Action);
                ButtonStatesList.Add(new ButtonState(
                    Marshal.StringToHGlobalUni(Name), ButtonHash));
            }
            foreach (var Window in App.Windows)
            {
                var WindowHash = ((ulong)AppHash << 0x20) | (uint)Window.Title.GetHashCode();
                Trip2DebugGui.Windows.Add(WindowHash, Window);
                WindowStatesList.Add(new WindowState(
                    Marshal.StringToHGlobalUni(Window.Title), 
                    WindowHash, 
                    Window.StartSize, 
                    Window.StartPos,
                    Window.CanClose
                    ));
            }
        }
        unsafe
        {
            fixed (WindowState* pWindowStatesArray = WindowStatesList.ToArray())
            {
                fixed (AppState* pAppStatesArray = AppStatesList.ToArray())
                {
                    fixed (ButtonState* pButtonStatesArray = ButtonStatesList.ToArray())
                    {
                        Interop.Windows.Entries = pWindowStatesArray;
                        Interop.Windows.Length = WindowStatesList.Count;

                        Interop.Apps.Entries = pAppStatesArray;
                        Interop.Apps.Length = AppStatesList.Count;
                        
                        Interop.Buttons.Entries = pButtonStatesArray;
                        Interop.Buttons.Length = ButtonStatesList.Count;
                        
                        Trip2DebugGui.set_interop_state(&Interop);
                        Trip2DebugGui.new_frame_ui();
                    
                        Trip2DebugGui.Windows.Clear();
                        Trip2DebugGui.Apps.Clear();
                        Trip2DebugGui.Buttons.Clear();   
                    }
                }
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