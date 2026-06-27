namespace p4rpc.trip2.debugui.Interfaces;

public interface IGUIState
{
    void Register(IGUIApp program);
    void Unregister(IGUIApp program);
}