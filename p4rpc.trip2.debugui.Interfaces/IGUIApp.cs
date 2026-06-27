namespace p4rpc.trip2.debugui.Interfaces;

public interface IGUIApp
{
    string Name { get; }
    
    List<IGUIWindow> Windows { get; }

    void Tick(float DeltaTime);
}