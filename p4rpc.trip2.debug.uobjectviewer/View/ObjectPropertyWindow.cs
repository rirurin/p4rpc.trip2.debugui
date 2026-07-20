extern alias imgui;
using imgui::p4rpc.trip2.ImGui;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using p4rpc.trip2.debugui.Interfaces;
using RyoTune.Reloaded;
using UE.Toolkit.Core.Types.Unreal.Factories;
using UE.Toolkit.Core.Types.Unreal.Factories.Interfaces;
using UE.Toolkit.Interfaces;

namespace p4rpc.trip2.debug.uobjectviewer.View;

public class UObjectWindow : GUIWindow<App>
{
    private IUObject Object;
    public override string Title { get; }

    private Dictionary<PropertyListKey, PropertyListView> Views = [];
    private PropertyListKey? CurrentView;

    public PropertyListView GetCurrentView() => Views[CurrentView!];

    internal IUnrealFactory? UnrealFactory { get; private set; }
    internal IUnrealMemory? UnrealMemory { get; private set; }
    internal IUnrealClasses? UnrealClasses { get; private set; }

    public override void Draw(App owner)
    {
        UnrealFactory ??= owner.Context.UnrealFactory;
        UnrealMemory ??= owner.Context.UnrealMemory;
        UnrealClasses ??= owner.Context.UnrealClasses;
        if (!owner.AllObjects.ContainsKey(Object.Ptr))
        {
            Close();
            return;
        }
        
        if (Views.Count == 0)
        {
            var ObjectKey = new PropertyListKey(Object.Ptr, Object.ClassPrivate.NamePrivate.ToString());
            Views.Add(ObjectKey, Object.ClassPrivate.NamePrivate.ToString() == "DataTable"
                ? new DataTableListView(null, Object.Ptr, Object.ClassPrivate, UnrealMemory)
                : new ObjectListView(null, Object.Ptr, Object.ClassPrivate, UnrealFactory, UnrealClasses));
            CurrentView = ObjectKey;
        }

        var HierarchyView = Views[CurrentView!];
        List<PropertyListView> ListOrder = [];
        while (HierarchyView != null)
        {
            ListOrder.Insert(0, HierarchyView);
            HierarchyView = HierarchyView.Parent;
        }
        
        var windowSize = new ImVec2.__Internal();
        unsafe { ImGui.__Internal.GetContentRegionAvail((nint)(&windowSize)); }
        windowSize.x -= ImGui.GetStyle().WindowPadding.X * 2;
        float windowDistance = 0;
        foreach (var (Index, Current) in ListOrder.Select((x, i) => (i, x)))
        {
            var ButtonString = Current.ToString();
            var itemSize = new ImVec2.__Internal();
            unsafe { ImGui.__Internal.CalcTextSize((nint)(&itemSize), " >> " + ButtonString + "\0", 
                null, true, -1); }
            var itemWidth = itemSize.x + ImGui.GetStyle().FramePadding.X * 2 + 10;
            var NextLine = itemWidth + windowDistance > windowSize.x;
            windowDistance = itemWidth + (NextLine ? 0 : windowDistance);
            if (Index != 0)
            {
                if (!NextLine)
                {
                    ImGui.SameLine(0, 10);
                }
                ImGui.Text(" >> ");
            }
            ImGui.SameLine(0, 10);
            if (ImGui.Button(ButtonString, ImGui.ImVec2ImVec2Nil()))
            {
                CurrentView = Current.GetKey();
            }
        }
        
        Views[CurrentView!].Draw(owner, this);
    }

    public UObjectWindow(IUObject uobject, App owner) : base(owner)
    {
        Object = uobject;
        Title = $"{Object.NamePrivate} @ 0x{Object.Ptr:x}";
    }

    public void AddView(PropertyListKey key, PropertyListView view)
    {
        if (view.Parent!.Children.TryAdd(key, view))
            Views.Add(key, view);
        CurrentView = key;
    }
}