extern alias imgui;
using System.Numerics;
using System.Runtime.InteropServices;
using imgui::p4rpc.trip2.ImGui;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using p4rpc.trip2.debugui.Interfaces;
using RyoTune.Reloaded;
using UE.Toolkit.Core.Types.Interfaces;
using UE.Toolkit.Core.Types.Unreal.Common.FunctionParam;
using UE.Toolkit.Core.Types.Unreal.Factories.Interfaces;
using UE.Toolkit.Core.Types.Unreal.UE5_4_4;

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

public enum CallMethodError
{
    None,
    CouldNotFindSkeletalMesh,
    NullReturnObject,
    WrongSocketIndex,
    // from ProcessEventResult
    CouldNotFindFunction,
    ParameterTypeMismatch,
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
    
    public bool CheckAddScriptStruct;
    public bool AddScriptStructPassed;
    
    // public bool CheckCallBlueprint;
    public int CallBlueprintAttempts;
    public float CallBlueprintTime;
    public readonly string AppId;
    public CallMethodError CalledBlueprintMethod = CallMethodError.CouldNotFindSkeletalMesh;
    
    
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

        if (CallBlueprintTime <= 0 && CalledBlueprintMethod != CallMethodError.None)
        {
            CallBlueprintTime = 1 << CallBlueprintAttempts;
            CallBlueprintAttempts++;
            var SK_Mesh = GetSkeletalMeshObject();
            if (SK_Mesh == null) return;
            var SocketName = GetSocketName();
            int SocketIndex;
            unsafe
            {
                var Result = SK_Mesh.ProcessEvent("FindSocketAndIndex", [
                    new NameParam(new(&SocketName)),
                    new IntParam(new(&SocketIndex))
                ], out var Return);
                if (Result != ProcessEventResult.Success)
                {
                    CalledBlueprintMethod = Result switch
                    {
                        ProcessEventResult.CouldNotFindFunction => CallMethodError.CouldNotFindFunction,
                        ProcessEventResult.ParameterTypeMismatch => CallMethodError.ParameterTypeMismatch,
                        _ => CalledBlueprintMethod
                    };
                    return;
                }
                if (Return == null)
                {
                    CalledBlueprintMethod = CallMethodError.NullReturnObject;
                    return;
                }
                var ReturnObject = Context.UnrealFactory.CreateUObject(((ObjectParam)Return).Value);
                if (SocketIndex != GetExpectedSocketIndex())
                {
                    CalledBlueprintMethod = CallMethodError.WrongSocketIndex;
                    return;
                }
                Log.Debug($"FindSocketAndIndex({SocketName}) Result: {Result}, Socket Index: {SocketIndex}, Return value: 0x{ReturnObject.Ptr:x}");
                CalledBlueprintMethod = CallMethodError.None;
            }
        }
        else CallBlueprintTime -= DeltaTime;
    }

    private IUObject? GetSkeletalMeshObject()
    {
        var TargetMesh = AppId switch
        {
            "p3r.exe" => "SK_PC0001_Title_00",
            _ => "SKM_Quinn_Simple"
        };
        return Context.UnrealObjects.FindObjectByName(TargetMesh, "SkeletalMesh");
    }

    private FName GetSocketName()
        => new(AppId switch
        {
            "p3r.exe" => "Soc_L_AttachUpLeg00_00", // P3R (SKEL_Human): Socket 21
            _ => "foot_l_Socket" // Third Person Sample: Socket 2
        });

    private int GetExpectedSocketIndex()
        => AppId switch
        {
            "p3r.exe" => 21,
            _ => 2
        };

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

    private bool GetUStaticMeshSize(out nint SizeOf)
    {
        SizeOf = Context.UnrealEssentials.GetEngineVersion() switch
        {
            "++UE4+Release-4.27" => 0x150,
            "++UE5+Release-5.4" => 0x248,
            "++UE5+Release-5.2" or "++UE5+Release-5.3" or "++UE5+Release-5.5" => 0x250,
            "++UE5+Release-5.0" or "++UE5+Release-5.1" => 0x258,
            "++UE5+Release-5.6" => 0x290,
            "++UE5+Release-5.7" => 0x2a0,
            _ => 0
        };
        return SizeOf != 0;
    }

    // Placeholder    
    // private struct USkeletalMesh {}
    
    // Placeholder    
    private struct UStaticMesh {}

    private bool TestStructExtension(nint sizeOf)
    {
        if (!CheckStructExtension)
        {
            if (Context.UnrealClasses.GetClassInfoFromClass<UStaticMesh>(out var ClassInfo))
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
            if (!Context.UnrealClasses.AddI32Property<UStaticMesh>(
                    "SampleInteger", (int)sizeOf, out _)) return false;
            AddPropertyError &= ~AddPropertyError.CouldNotCreateIntProperty;
            if (!Context.UnrealClasses.AddStringProperty<UStaticMesh>(
                    "SampleString", (int)sizeOf + 0x8, out _)) return false;
            AddPropertyError &= ~AddPropertyError.CouldNotCreateStringProperty;
            CheckAddProperty = true;   
        }
        return true;
    }

    private bool TestAddScriptStruct()
    {
        if (!CheckAddScriptStruct)
        {
            AddScriptStructPassed = Context.UnrealClasses.CreateScriptStruct("AgePanelSection", 0x30,
            [
                Context.UnrealClasses.CreateF32Param("X1", 0),
                Context.UnrealClasses.CreateF32Param("X2", 4),
                Context.UnrealClasses.CreateF32Param("Y1", 8),
                Context.UnrealClasses.CreateF32Param("Y2", 0xc),
                Context.UnrealClasses.CreateF32Param("Field28", 0x28)
            ], out _);
            CheckAddScriptStruct = true;
        }
        return AddScriptStructPassed;
    }

    public App(Context context) : base(context.GUIState)
    {
        Context = context;
        ObjectLogError = ObjectLogError.OnObjectLoaded | ObjectLogError.OnObjectBeginDestroy;
        Context.UnrealObjects.OnObjectLoaded += _ => ObjectLogError &= ~ObjectLogError.OnObjectLoaded;
        Context.UnrealObjects.OnObjectBeginDestroy += _ => ObjectLogError &= ~ObjectLogError.OnObjectBeginDestroy;
        
        AddPropertyError = AddPropertyError.CouldNotCreateIntProperty | AddPropertyError.CouldNotCreateStringProperty;

        AppId = Context.ModLoader.GetAppConfig().AppId;

        if (GetUStaticMeshSize(out var sizeOf))
        {
            Context.UnrealClasses.AddExtension<UStaticMesh>(EXTENSION_SIZE, x =>
            {
                unsafe
                {
                    NativeMemory.Clear((void*)((nint)x.Self + sizeOf), EXTENSION_SIZE);
                }
                if (!TestStructExtension(sizeOf)) return;
                if (!TestAddProperties(sizeOf)) return;
                if (!TestAddScriptStruct()) return;
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

public class AddScriptStruct(WeakReference<App> owner) : UnitTest(owner)
{
    protected override string Name => "Add UScriptStruct";
    protected override bool Passed => Owner.TryGetTarget(out var owner) && owner.AddScriptStructPassed;
    protected override string Reason => "Failed...";
}

public class CallMethodProcessEvent(WeakReference<App> owner) : UnitTest(owner)
{
    protected override string Name => "Call Blueprint Method";
    protected override bool Passed => Owner.TryGetTarget(out var owner) && owner.CalledBlueprintMethod == CallMethodError.None;
    protected override string Reason => Owner.TryGetTarget(out var owner) ? 
        $"{owner.CalledBlueprintMethod.ToString()} (retry in {owner.CallBlueprintTime:F2} sec)" : "N/A";
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
            new AddPropertiesTest(Owner), new AddScriptStruct(Owner), new CallMethodProcessEvent(Owner),
        ]);
    }
    
    public override Vector2 StartSize => 
        Owner.TryGetTarget(out var App) ? App.GetProportionalSize(0.33f, 0.33f) : Vector2.Zero;
    
    public override Vector2 StartPos => Owner.TryGetTarget(out _) ? new Vector2(15, 30) : Vector2.Zero;

    public override void Draw(App owner)
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg;
        if (ImGui.BeginTable("##UEToolkitUnitTests", TABLE_COLUMNS.Length, (int)flags, ImGui.ImVec2ImVec2Nil(), 0))
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