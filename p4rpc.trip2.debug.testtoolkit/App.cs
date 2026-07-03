extern alias imgui;
using System.Numerics;
using System.Runtime.InteropServices;
using imgui::p4rpc.trip2.ImGui;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using p4rpc.trip2.debugui.Interfaces;
using RyoTune.Reloaded;

namespace p4rpc.trip2.debug.testtoolkit;

public enum ObjectLogError 
{
    OnObjectLoaded = 1 << 0,
    OnObjectBeginDestroy = 1 << 1
}

public enum MemoryError
{
    None,
    TestResultsPending,
    DidNotAllocate,
    BadAlignment,
    DidNotGetAllocSize,
    GetAllocSizeWrongValue,
    QuantizedValueWasSmaller,
}

public enum StructExtensionError
{
    None,
    DidNotCallConstructor,
    CouldNotGetClassInfo,
    DidNotExtendStruct
}

public enum AddPropertyError
{
    CouldNotCreateIntProperty = 1 << 0,
    CouldNotCreateStringProperty = 1 << 1,
}

public class App : GUIApp
{

    internal Context Context { get; private init; }

    internal ObjectLogError ObjectLogError { get; private set; }
    
    private const int ALLOC_SIZE = 0x80;
    public bool CheckFMemory;
    internal MemoryError MemoryError { get; private set; } = MemoryError.TestResultsPending;
    private const int EXTENSION_SIZE = 0x40;
    public bool CheckStructExtension;
    internal StructExtensionError StructExtensionError { get; private set; } = StructExtensionError.DidNotCallConstructor;
    public bool CheckAddProperty;
    internal AddPropertyError AddPropertyError { get; private set; }
    
    
    public override string Name => "UE Toolkit Unit Testing";

    
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
            MemoryError = MemoryError.None;
            Context.UnrealMemory.Free(Heap);
        }
    }

    private bool GetUSkeletalMeshSize(out nint SizeOf)
    {
        SizeOf = Context.UnrealEssentials.GetEngineVersion() switch
        {
            "++UE4+Release-4.27" => 0x3A0,
            "++UE5+Release-5.0" => 0x470,
            "++UE5+Release-5.2" or "++UE5+Release-5.4" => 0x4D8,
            "++UE5+Release-5.1" or "++UE5+Release-5.3" => 0x4E0,
            "++UE5+Release-5.7" => 0x518,
            "++UE5+Release-5.5" => 0x528,
            "++UE5+Release-5.6" => 0x568,
            _ => 0
        };
        return SizeOf != 0;
    }

    // Placeholder    
    private struct USkeletalMesh {}

    private bool TestStructExtension(nint sizeOf)
    {
        if (!CheckStructExtension)
        {
            if (Context.UnrealClasses.GetClassInfoFromClass<USkeletalMesh>(out var ClassInfo))
            {
                if (ClassInfo.PropertiesSize != sizeOf + EXTENSION_SIZE)
                {
                    StructExtensionError = StructExtensionError.DidNotExtendStruct;
                    return false;
                }
            }
            else
            {
                StructExtensionError = StructExtensionError.CouldNotGetClassInfo;
                return false;
            }

            StructExtensionError = StructExtensionError.None; 
            CheckStructExtension = true;    
        }
        return true;
    }

    private bool TestAddProperties(nint sizeOf)
    {
        if (!CheckAddProperty)
        {
            if (!Context.UnrealClasses.AddI32Property<USkeletalMesh>(
                    "SampleInteger", (int)sizeOf, out _)) return false;
            AddPropertyError &= ~AddPropertyError.CouldNotCreateIntProperty;
            if (!Context.UnrealClasses.AddStringProperty<USkeletalMesh>(
                    "SampleString", (int)sizeOf + 0x8, out _)) return false;
            AddPropertyError &= ~AddPropertyError.CouldNotCreateStringProperty;
            CheckAddProperty = true;   
        }
        return true;
    }

    public App(Context context) : base(context.GUIState)
    {
        Context = context;
        ObjectLogError = ObjectLogError.OnObjectLoaded | ObjectLogError.OnObjectBeginDestroy;
        Context.UnrealObjects.OnObjectLoaded += _ => ObjectLogError &= ~ObjectLogError.OnObjectLoaded;
        Context.UnrealObjects.OnObjectBeginDestroy += _ => ObjectLogError &= ~ObjectLogError.OnObjectBeginDestroy;
        
        AddPropertyError = AddPropertyError.CouldNotCreateIntProperty | AddPropertyError.CouldNotCreateStringProperty;

        if (GetUSkeletalMeshSize(out var sizeOf))
        {
            Context.UnrealClasses.AddExtension<USkeletalMesh>(EXTENSION_SIZE, x =>
            {
                unsafe
                {
                    NativeMemory.Clear((void*)((nint)x.Self + sizeOf), EXTENSION_SIZE);
                }
                if (!TestStructExtension(sizeOf)) return;
                if (!TestAddProperties(sizeOf)) return;
            });
        }
        else
        {
            Log.Warning("sizeof for UDynamicEntryBox is not defined for this engine version! Dump the object types to get the size!");
        }
        
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

public class StructExtensionTest(WeakReference<App> owner) : UnitTest(owner)
{
    protected override string Name => "Struct Extension";
    protected override bool Passed => Owner.TryGetTarget(out var owner) && owner.StructExtensionError == StructExtensionError.None;
    protected override string Reason => Owner.TryGetTarget(out var owner) ? owner.StructExtensionError.ToString() : "N/A";
}

public class AddPropertiesTest(WeakReference<App> owner) : UnitTest(owner)
{
    protected override string Name => "Add Properties";
    protected override bool Passed => Owner.TryGetTarget(out var owner) && owner.AddPropertyError == 0;
    protected override string Reason => Owner.TryGetTarget(out var owner) ? owner.AddPropertyError.ToString() : "N/A";
}

public class AppWindow : GUIWindow<App>
{
    public override string Title => "Unreal Toolkit Unit Tests";
    public override bool CanClose => false;
    private static string[] TABLE_COLUMNS = ["Test", "Result", "Reason"];
    private List<UnitTest> Tests = [];

    public AppWindow(App owner) : base(owner)
    {
        Tests.AddRange([
            new ObjectLogging(Owner), new MemoryTest(Owner), new StructExtensionTest(Owner),
            new AddPropertiesTest(Owner)
        ]);
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