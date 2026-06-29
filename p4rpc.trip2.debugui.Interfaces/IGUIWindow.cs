namespace p4rpc.trip2.debugui.Interfaces;
using System.Numerics;

public interface IGUIWindow
{
    // WeakReference<IGUIApp>? Owner { get; init; }
    
    string Title { get; }
    
    bool IsOpen { get; set; }
    
    Vector2 StartSize { get; }

    Vector2 StartPos { get; }
    
    bool CanClose { get; }

    void DrawContents();

    bool Close();
}

public abstract class GUIWindow<TOwner>(TOwner owner) : IGUIWindow where TOwner: class, IGUIApp
{
    protected WeakReference<TOwner> Owner { get; init; } = new(owner);
    public abstract string Title { get; }
    public virtual bool CanClose => true;
    public bool IsOpen { get; set; }
    public virtual Vector2 StartSize => Vector2.Zero;
    public virtual Vector2 StartPos => Vector2.Zero;

    public void DrawContents()
    {
        if (!Owner.TryGetTarget(out var app)) return;
        Draw(app);
    }
    public abstract void Draw(TOwner owner);

    public bool Close()
    {
        if (!Owner.TryGetTarget(out var App))
            return false;
        App!.RemoveWindow(this);
        return true;
    }
}