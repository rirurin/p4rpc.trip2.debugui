using System.Numerics;
using System.Runtime.InteropServices;
using p4rpc.trip2.debugui.Interfaces;

namespace p4rpc.trip2.debugui;

public class GuiState : IGUIState
{
    internal readonly Dictionary<string, IGUIApp> Programs = [];

    public void Register(IGUIApp program) => Programs.Add(program.Name, program);

    public void Unregister(IGUIApp program) => Programs.Remove(program.Name);

    public Vector2 GetSurfaceSize() => Trip2DebugGui.get_surface_size();
    
    public nint AddFont(string Path, /*ref uint glyphRange,*/ float fontSize)
        => Trip2DebugGui.add_font_from_path(Marshal.StringToHGlobalUni(Path), /*ref glyphRange,*/ fontSize);

    public nint GetFont(string Name)
        => Trip2DebugGui.get_font(Marshal.StringToHGlobalUni(Name));
}