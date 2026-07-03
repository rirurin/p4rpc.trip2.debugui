namespace p4rpc.trip2.debugui.Interfaces;
using System.Numerics;

public interface IGUIState
{
    void Register(IGUIApp program);
    void Unregister(IGUIApp program);
    Vector2 GetSurfaceSize();
    nint AddFont(string Path, /*ref uint glyphRange,*/ float fontSize);
    nint GetFont(string Name);
}