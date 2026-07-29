extern alias imgui;
using System.Numerics;
using System.Runtime.CompilerServices;
using imgui::p4rpc.trip2.ImGui;
using RyoTune.Reloaded;
using UE.Toolkit.Core.Types;
using UE.Toolkit.Core.Types.Interfaces;
using UE.Toolkit.Core.Types.Unreal.Common.FunctionParam;
using UE.Toolkit.Core.Types.Unreal.Factories;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using UE.Toolkit.Core.Types.Unreal.Factories.Interfaces;
using UE.Toolkit.Core.Types.Unreal.UE5_4_4;
using UE.Toolkit.Interfaces;

namespace p4rpc.trip2.debug.uobjectviewer.View;

public class ObjectListView(PropertyListView? parent, nint baseAddress, IUClass value, IUnrealFactory factory,
    IUnrealClasses classes) : StructListViewBase<IUClass>(parent, baseAddress, value)
{
    private IUnrealFactory Factory = factory;
    private IUnrealClasses Classes = classes;
    
    public override void Draw(App owner, UObjectWindow window)
    {
        const ImGuiTabBarFlags tabFlags = ImGuiTabBarFlags.Reorderable;
        if (ImGui.Button($"Class Default Object##{BaseAddress:x}", ImGui.ImVec2ImVec2Nil()))
        {
            owner.Windows.Add(new UObjectWindow(Value.ClassDefaultObject, owner));
        }
        ImGui.SameLine(0, 10);
        ImGui.Text($"0x{Value.ClassDefaultObject.Ptr:x}");
        if (ImGui.BeginTabBar($"##ObjectListTabs{BaseAddress:x}", (int)tabFlags))
        {
            var fieldsOpen = true;
            if (ImGui.BeginTabItem($"Fields##ObjectListView{BaseAddress:x}", ref fieldsOpen, 0))
            {
                base.Draw(owner, window);
                ImGui.EndTabItem();
            }
            var methodsOpen = true;
            if (ImGui.BeginTabItem($"Methods##ObjectListView{BaseAddress:x}", ref methodsOpen, 0))
            {
                DrawMethods(owner, window);
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }
    
    protected static UObjectWindowColumn[] METHOD_COLUMNS =
    [
        new("Name", _ => 0),
        new("Address", _ => 0),
        new("Value", _ => 0),
    ];

    private void DrawMethods(App owner, UObjectWindow window)
    {
        var FunctionList = Value.GetFunctions();
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg;
        /*
        if (ImGui.Button($"METHOD TEST##{BaseAddress:x}", ImGui.ImVec2ImVec2Nil()))
        {
            var Object = Factory.CreateUObject(BaseAddress);
            Log.Debug($"Object: 0x{BaseAddress:x}, Class: {Object.ClassPrivate.NamePrivate}");
            // var SocketName = new FName("Soc_L_AttachUpLeg00_00"); // P3R: Socket 21
            var SocketName = new FName("foot_l_Socket"); // Third Person Sample: Socket 2
            int SocketIndex;
            unsafe
            {
                var Result = Object.ProcessEvent("FindSocketAndIndex", [
                    new NameParam(new(&SocketName)),
                    new IntParam(new(&SocketIndex))
                ], out var Return);
                var ReturnObject = Factory.CreateUObject(((ObjectParam)Return).Value);
                Log.Debug($"Result: {Result}, Socket Index: {SocketIndex}, Return value: 0x{ReturnObject.Ptr:x}");
            }
        }
        */
        var regionAvailable = GetRegionAvailable();
        if (ImGui.BeginTable($"##StructListView{BaseAddress:x}", METHOD_COLUMNS.Length, (int)flags, ImGui.ImVec2ImVec2Nil(), 0))
        {
            var columnFlags = (int)ImGuiTableColumnFlags.WidthFixed;
            foreach (var (Index, Column) in METHOD_COLUMNS.Select((x, i) => (i, x)))
                ImGui.TableSetupColumn(Column.Name, columnFlags, Column.GetWidth(regionAvailable), (uint)Index);
            ImGui.TableHeadersRow();
            foreach (var Function in FunctionList)
            {
                ImGui.TableNextRow(0, 0);
                ImGui.TableSetColumnIndex(0);
                ImGui.Text($"{Function.NamePrivate}");
                ImGui.TableSetColumnIndex(1);
                ImGui.Text($"0x{Function.FunctionPtr:x}");
                ImGui.TableSetColumnIndex(2);
                List<string> ParamFmt = [];
                foreach (var (Index, Field) in Function.ChildProperties.Select((x, i) => (i, x)))
                {
                    // Assume CPF_Parm
                    var Property = Factory.CreateFProperty(Field.Ptr);
                    // OutIndex should be out type (FindSocketAndIndex)
                    /*
                    var CPPRef = Property.PropertyFlags.HasFlag(EPropertyFlags.CPF_OutParm) &&
                        !Property.PropertyFlags.HasFlag(EPropertyFlags.CPF_ReferenceParm);
                    var Modifiers = CPPRef switch
                    {
                        true => "&",
                        false => string.Empty
                    };
                    */
                    var Modifiers = string.Empty;
                    ParamFmt.Add($"{Classes.GetPropertyTypeName(Property)}{Modifiers} {Property.NamePrivate} @ 0x{Property.Offset_Internal:x}");
                }
                ImGui.Text($"({string.Join(",", ParamFmt)})");
            }
            ImGui.EndTable();
        }
    }
}

public class UObjectDynamic
{
    public IUObject Inner { get; }
    protected Dictionary<string, int> FieldOffsets;

    public UObjectDynamic(IUObject inner) {
        Inner = inner;
        FieldOffsets = Inner.ClassPrivate.PropertyLink
            .Select(x => (x.NamePrivate, x.Offset_Internal)).ToDictionary();
    }
}

public class UInputComponent(IUObject inner) : UObjectDynamic(inner);

public class APlayerController(IUObject inner) : UObjectDynamic(inner);

public class AInputManager(IUObject inner) : APlayerController(inner) {

    public UObjectDynamic FindSocketAndIndex(FName SocketName, ref int SocketIndex, UObjectDynamic Test)
    {
        unsafe
        {
            _ = Inner.ProcessEvent("FindSocketAndIndex", [
                new NameParam(new(&SocketName)),
                new IntParam(new((int*)Unsafe.AsPointer(ref SocketIndex)))
            ], out var Return);
            return new(Inner.GetFactory().CreateUObject(((ObjectParam)Return).Value));
        }   
    }

    public unsafe UInputComponent mpDefaultInputComponent
    {
        get => new(Inner.GetFactory().CreateUObject(*(nint*)(Inner.Ptr + FieldOffsets["mpDefaultInputComponent"])));
        set => *(nint*)(Inner.Ptr + FieldOffsets["mpDefaultInputComponent"]) = value.Inner.Ptr;
    }

    public unsafe UInputComponent mpCurrentInputComponent
    {
        get => new(Inner.GetFactory().CreateUObject(*(nint*)(Inner.Ptr + FieldOffsets["mpCurrentInputComponent"])));
        set => *(nint*)(Inner.Ptr + FieldOffsets["mpCurrentInputComponent"]) = value.Inner.Ptr;
    }

    public unsafe float InputYawScale 
    {
        get => *(float*)(Inner.Ptr + FieldOffsets["InputYawScale"]);
        set => *(float*)(Inner.Ptr + FieldOffsets["InputYawScale"]) = value;
    }
}