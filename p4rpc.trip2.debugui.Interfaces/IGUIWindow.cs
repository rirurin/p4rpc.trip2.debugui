namespace p4rpc.trip2.debugui.Interfaces;

public interface IGUIWindow
{
    WeakReference<IGUIApp>? Owner { get; init; }
    
    string Title { get; }
    
    bool IsOpen { get; set; }

    void DrawContents();
}