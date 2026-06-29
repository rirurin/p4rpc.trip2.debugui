extern alias imgui;
using System.Numerics;
using imgui::p4rpc.trip2.ImGui;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using p4rpc.trip2.debugui.Interfaces;

namespace p4rpc.trip2.debug.testtoolkit;

public enum ObjectLogError 
{
    OnObjectLoaded = 1 << 0,
    OnObjectBeginDestroy = 1 << 1
}

public enum MemoryError
{
    None,
    DidNotAllocate,
    BadAlignment,
    DidNotGetAllocSize,
    GetAllocSizeWrongValue,
    QuantizedValueWasSmaller,
}

public class App : GUIApp
{

    internal Context Context { get; private init; }

    internal ObjectLogError ObjectLogError { get; private set; }
    internal MemoryError MemoryError { get; private set; } = MemoryError.None;
    private const int ALLOC_SIZE = 0x80;
    
    public override string Name => "UE Toolkit Unit Testing";

    public bool CheckFMemory;
    
    public override void Tick(float DeltaTime)
    {
        if (!CheckFMemory)
        {
            CheckFMemory = true;
            var Heap = Context.UnrealMemory.Malloc(ALLOC_SIZE);
            if (Heap == nint.Zero)
            {
                MemoryError = MemoryError.DidNotAllocate;
                return;               
            }
            if (Heap % 16 != 0)
            {
                MemoryError = MemoryError.BadAlignment;
                return;
            }

            nint Size = 0;
            if (!Context.UnrealMemory.GetAllocSize(Heap, ref Size))
            {
                MemoryError = MemoryError.DidNotGetAllocSize;
                return;
            }
            if (Size != ALLOC_SIZE)
            {
                MemoryError = MemoryError.GetAllocSizeWrongValue;
                return;
            }

            var quantizedSize = Context.UnrealMemory.QuantizeSize(0x5c);
            if (quantizedSize < 0x5c)
            {
                MemoryError = MemoryError.QuantizedValueWasSmaller;
                return;               
            }
            Heap = Context.UnrealMemory.Realloc(Heap, ALLOC_SIZE * 2);
            if (!Context.UnrealMemory.GetAllocSize(Heap, ref Size))
            {
                MemoryError = MemoryError.DidNotGetAllocSize;
                return;
            }
            if (Size != ALLOC_SIZE * 2)
            {
                MemoryError = MemoryError.GetAllocSizeWrongValue;
                return;
            }
            Context.UnrealMemory.Free(Heap);
        }
    }

    public App(Context context) : base(context.GUIState)
    {
        Context = context;
        ObjectLogError = ObjectLogError.OnObjectLoaded | ObjectLogError.OnObjectBeginDestroy;
        Context.UnrealObjects.OnObjectLoaded += _ => ObjectLogError &= ~ObjectLogError.OnObjectLoaded;
        Context.UnrealObjects.OnObjectBeginDestroy += _ => ObjectLogError &= ~ObjectLogError.OnObjectBeginDestroy;
        
        Windows.Add(new AppWindow(this));
    }
}

public abstract class UnitTest(WeakReference<App> owner)
{
    private ImVec4.__Internal ErrorColor = new() { x = 1, y = 0, z = 0, w = 1 };
    protected readonly WeakReference<App> Owner = owner;
    protected abstract string Name { get; }
    protected abstract bool Passed { get; }
    protected abstract string Reason { get; }

    public void Row()
    {
        ImGui.TableNextRow(0, 0);
        ImGui.TableSetColumnIndex(0);
        ImGui.Text(Name);
        ImGui.TableSetColumnIndex(1);
        if (Passed) ImGui.__Internal.Text("PASS");
        else ImGui.__Internal.TextColored(ErrorColor, "FAIL");
        ImGui.TableSetColumnIndex(2);
        if (!Passed)
            ImGui.__Internal.Text(Reason);
    }
}

public class ObjectLogging(WeakReference<App> owner) : UnitTest(owner)
{
    protected override string Name => "Object Logging";
    protected override bool Passed => Owner.TryGetTarget(out var owner) && owner.ObjectLogError == 0;
    protected override string Reason => Owner.TryGetTarget(out var owner) ? $"{owner.ObjectLogError} was not called" : "N/A";
}

public class MemoryTest(WeakReference<App> owner) : UnitTest(owner)
{
    protected override string Name => "FMemory Functions";
    protected override bool Passed => Owner.TryGetTarget(out var owner) && owner.MemoryError == MemoryError.None;
    protected override string Reason => Owner.TryGetTarget(out var owner) ? owner.MemoryError.ToString() : "N/A";
}

public class AppWindow : GUIWindow<App>
{
    public override string Title => "Unreal Toolkit Unit Tests";
    public override bool CanClose => false;
    private static string[] TABLE_COLUMNS = ["Test", "Result", "Reason"];
    private List<UnitTest> Tests = [];

    public AppWindow(App owner) : base(owner)
    {
        Tests.Add(new ObjectLogging(Owner));
        Tests.Add(new MemoryTest(Owner));
    }

    public override Vector2 StartSize
    {
        get
        {
            if (!Owner.TryGetTarget(out var App)) return Vector2.Zero;
            var SurfaceSize = App.State!.GetSurfaceSize();
            return new Vector2(SurfaceSize.X / 3, SurfaceSize.Y / 3);
        }
    }
    
    public override Vector2 StartPos
    {
        get
        {
            if (!Owner.TryGetTarget(out var App)) return Vector2.Zero;
            var SurfaceSize = App.State!.GetSurfaceSize();
            return new Vector2(15, 30);
        }
    }

    public override void Draw(App owner)
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg;
        if (ImGui.BeginTable("##UEToolkitUnitTests", 3, (int)flags, ImGui.ImVec2ImVec2Float(0, 0), 0))
        {
            foreach (var (Index, Column) in TABLE_COLUMNS.Select((x, i) => (i, x)))
                ImGui.TableSetupColumn(Column, 0, 0, (uint)Index);    
            ImGui.TableHeadersRow();
            foreach (var Test in Tests)
                Test.Row();
            ImGui.EndTable();
        }
    }
}