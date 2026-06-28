using System.Numerics;
using p4rpc.trip2.debugui.Interfaces;

namespace p4rpc.trip2.debugui;

public class GuiState : IGUIState
{
    internal readonly Dictionary<string, IGUIApp> Programs = [];

    public void Register(IGUIApp program) => Programs.Add(program.Name, program);

    public void Unregister(IGUIApp program) => Programs.Remove(program.Name);

    public Vector2 GetSurfaceSize() => Trip2DebugGui.get_surface_size();
}