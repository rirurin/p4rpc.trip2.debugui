extern alias imgui;
using imgui::p4rpc.trip2.ImGui;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using p4rpc.trip2.debugui.Interfaces;
using RyoTune.Reloaded;
using UE.Toolkit.Core.Types.Unreal.Factories.Interfaces;

namespace p4rpc.trip2.debug.uobjectviewer.View;

public class UObjectWindow : GUIWindow<App>
{
    private IUObject Object;
    public override string Title { get; }

    private Dictionary<nint, PropertyListView> Views = [];
    private nint CurrentView = nint.Zero;

    public PropertyListView GetCurrentView() => Views[CurrentView];

    public override void Draw(App owner)
    {
        if (!owner.AllObjects.ContainsKey(Object.Ptr))
        {
            Close();
            return;
        }
        
        if (Views.Count == 0)
        {
            Views.Add(Object.Ptr, new StructListView(null, Object.Ptr, Object.ClassPrivate));
            CurrentView = Object.Ptr;
        }

        var HierarchyView = Views[CurrentView];
        List<PropertyListView> ListOrder = [];
        while (HierarchyView != null)
        {
            ListOrder.Insert(0, HierarchyView);
            HierarchyView = HierarchyView.Parent;
        }

        foreach (var (Index, Current) in ListOrder.Select((x, i) => (i, x)))
        {
            if (Index != 0)
            {
                ImGui.SameLine(0, 10);
                ImGui.Text(" >> ");
            }
            ImGui.SameLine(0, 10);
            if (ImGui.Button(Current.ToString(), ImGui.ImVec2ImVec2Nil()))
            {
                CurrentView = Current.GetBaseAddress();
            }
        }
        
        Views[CurrentView].Draw(owner, this);
    }

    public UObjectWindow(IUObject uobject, App owner) : base(owner)
    {
        Object = uobject;
        // var Class = Object.ClassPrivate;
        Title = $"{Object.NamePrivate} @ 0x{Object.Ptr:x}";
    }

    public void AddView(nint address, PropertyListView view)
    {
        if (view.Parent!.Children.TryAdd(address, view))
            Views.Add(address, view);
        CurrentView = address;
    }
}