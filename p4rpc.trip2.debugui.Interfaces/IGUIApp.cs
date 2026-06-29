namespace p4rpc.trip2.debugui.Interfaces;

public interface IGUIApp
{
    string Name { get; }
    
    List<IGUIWindow> Windows { get; }
    
    Dictionary<string, Action> Buttons { get; }

    void Tick(float DeltaTime);
    
    bool RemoveWindow(IGUIWindow window);
}

public abstract class GUIApp(IGUIState state) : IGUIApp
{
    public abstract string Name { get; }
    public Dictionary<string, Action> Buttons { get; } = [];
    public List<IGUIWindow> Windows { get; } = [];
    public abstract void Tick(float DeltaTime);
    public IGUIState? State { get; protected init; } = state;
    public bool RemoveWindow(IGUIWindow window) => Windows.Remove(window);
}