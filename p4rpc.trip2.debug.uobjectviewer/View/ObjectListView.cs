extern alias imgui;
using System.Runtime.InteropServices;
using imgui::p4rpc.trip2.ImGui;
using NCalc;
using RyoTune.Reloaded;
using UE.Toolkit.Core.Types.Unreal.Factories;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using UE.Toolkit.Core.Types.Unreal.Factories.Interfaces;
using UE.Toolkit.Interfaces;

namespace p4rpc.trip2.debug.uobjectviewer.View;

public class MethodListEntry(IUFunction inner, string definedIn)
{
    public IUFunction Inner { get; } = inner;
    public string DefinedIn { get; } = definedIn;
}

public class ObjectListView(PropertyListView? parent, nint baseAddress, IUClass value, IUnrealFactory factory,
    IUnrealClasses classes) : StructListViewBase<IUClass>(parent, baseAddress, value)
{
    private IUnrealFactory Factory = factory;
    private IUnrealClasses Classes = classes;

    internal List<MethodListEntry> Functions = [];
    private MethodSearch? MethodSearch;
    
    public override void Draw(App owner, UObjectWindow window)
    {
        const ImGuiTabBarFlags tabFlags = ImGuiTabBarFlags.Reorderable;
        if (ImGui.Button($"Class Default Object @ 0x{Value.ClassDefaultObject.Ptr:x}", ImGui.ImVec2ImVec2Nil()))
            owner.Windows.Add(new UObjectWindow(Value.ClassDefaultObject, owner));
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
        new("Defined by", _ => 0),
        new("Value", _ => 0),
    ];

    internal void RecreateFunctionsList(Func<MethodListEntry, bool> Callback)
    {
        Functions.Clear();
        var CurrentClass = Value;
        while (CurrentClass != null)
        {
            Functions.AddRange(
                CurrentClass.GetFunctions().Select(x => new MethodListEntry(x, 
                    CurrentClass.NamePrivate.ToString())).Where(Callback).DistinctBy(x => x.Inner.Ptr));
            CurrentClass = CurrentClass.GetSuperClass();
        }
        Functions.Sort((x, y) => string.Compare(
            x.Inner.NamePrivate.ToString(), y.Inner.NamePrivate.ToString(), StringComparison.Ordinal));
    }

    private void DrawMethods(App owner, UObjectWindow window)
    {
        if (MethodSearch == null) RecreateFunctionsList(_ => true);
        MethodSearch ??= new(this);
        MethodSearch.DrawPanel();
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg;
        var regionAvailable = GetRegionAvailable();
        const ImGuiTableColumnFlags columnFlags = ImGuiTableColumnFlags.WidthFixed;
        if (ImGui.BeginTable($"##StructListView{BaseAddress:x}", METHOD_COLUMNS.Length, (int)flags, ImGui.ImVec2ImVec2Nil(), 0))
        {
            foreach (var (Index, Column) in METHOD_COLUMNS.Select((x, i) => (i, x)))
                ImGui.TableSetupColumn(Column.Name, (int)columnFlags, Column.GetWidth(regionAvailable), (uint)Index);
            ImGui.TableHeadersRow();
            foreach (var Function in Functions)
            {
                ImGui.TableNextRow(0, 0);
                ImGui.TableSetColumnIndex(0);
                ImGui.Text($"{Function.Inner.NamePrivate}");
                ImGui.TableSetColumnIndex(1);
                ImGui.Text($"0x{Function.Inner.FunctionPtr:x}");
                ImGui.TableSetColumnIndex(2);
                ImGui.Text(Function.DefinedIn);
                ImGui.TableSetColumnIndex(3);
                if (ImGui.Button($"Open Function##{Function.Inner.Ptr:x}", ImGui.ImVec2ImVec2Nil()))
                {
                    var AllocSize = Function.Inner.GetTotalParameterSize();
                    var Allocation = owner.Context.UnrealMemory.Malloc(AllocSize);
                    unsafe { NativeMemory.Clear((void*)Allocation, (nuint)AllocSize); }
                    var CurrentObject = window.UnrealFactory!.CreateUObject(BaseAddress);
                    window.AddView(
                        new(Allocation, Function.Inner.NamePrivate.ToString()),
                        new FunctionParamListView(window.GetCurrentView(), Allocation, CurrentObject, Function.Inner, 
                            window.UnrealFactory!, window.UnrealMemory!, window.UnrealClasses!));
                }
            }
            ImGui.EndTable();
        }
    }
}