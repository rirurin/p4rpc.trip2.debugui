using p4rpc.trip2.debugui.Interfaces;

namespace p4rpc.trip2.debugui;

public class GuiState : IGUIState
{
    public Dictionary<string, IGUIApp> Programs = new();

    public void Register(IGUIApp program) => Programs.Add(program.Name, program);

    public void Unregister(IGUIApp program) => Programs.Remove(program.Name);
}