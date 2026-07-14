extern alias imgui;
using System.Numerics;
using imgui::p4rpc.trip2.ImGui;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using p4rpc.trip2.debugui.Interfaces;

namespace p4rpc.trip2.debug.uobjectviewer.View;

public class GUObjectArrayWindow(App owner) : GUIWindow<App>(owner)
{
    public override string Title => "All Loaded UObjects";

    private static string[] TABLE_COLUMNS = ["Type", "Name", "Address"];

    public override Vector2 StartSize
    {
        get
        {
            if (!Owner.TryGetTarget(out var App)) return Vector2.Zero; 
            var SurfaceSize = App.State!.GetSurfaceSize();
            return new Vector2(SurfaceSize.X / 2, SurfaceSize.Y * 3 / 4);
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
        if (!owner.InitialLoad)
        {
            owner.RecreateObjectList(owner.VisibleObjects, _ => true);
            owner.RecreateObjectList(owner.AllObjects, _ => true);
            // owner.ObjectSearch.OnSearchClear();
            owner.InitialLoad = true;
        }
        ImGui.Text($"{owner.VisibleObjects.Count} objects (GUObjectArray has {owner.Context.UnrealObjects.GUObjectArray.NumElements} elements)");
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg;
        owner.ObjectSearch.DrawPanel();
        if (ImGui.BeginTable("##UObject List", 3, (int)flags, ImGui.ImVec2ImVec2Float(0, 0), 0))
        {
            foreach (var (Index, Column) in TABLE_COLUMNS.Select((x, i) => (i, x)))
                ImGui.TableSetupColumn(Column, 0, 0, (uint)Index);
            ImGui.TableHeadersRow();
            var Entries = owner.VisibleObjects.ToList();
            unsafe
            {
                var clipper = new ImGuiListClipper.__Internal();
                ImGui.__Internal.ImGuiListClipperBegin((nint)(&clipper), Entries.Count, 0);
                while (ImGui.__Internal.ImGuiListClipperStep((nint)(&clipper)))
                {
                    for (var k = clipper.DisplayStart; k < clipper.DisplayEnd; k++)
                    {
                        var Entry = Entries[k];
                        ImGui.TableNextRow(0, 0);
                        ImGui.TableSetColumnIndex(0);
                        if (ImGui.SelectableBool($"{Entry.Value.NamePrivate}##0x{Entry.Value.Ptr:X}", false,
                                (int)ImGuiSelectableFlags.SpanAllColumns, ImGui.ImVec2ImVec2Nil()))
                            owner.Windows.Add(new UObjectWindow(Entry.Value, owner));
                        ImGui.TableSetColumnIndex(1);
                        ImGui.Text($"{Entry.Value.ClassPrivate.NamePrivate}");
                        ImGui.TableSetColumnIndex(2);
                        ImGui.Text($"0x{Entry.Value.Ptr:X}");
                    }
                }
                ImGui.__Internal.ImGuiListClipperEnd((nint)(&clipper));
            }
            ImGui.EndTable();
        }
    }
}