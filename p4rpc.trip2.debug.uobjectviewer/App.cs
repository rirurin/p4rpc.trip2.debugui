extern alias imgui;
using imgui::p4rpc.trip2.ImGui;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using System.Numerics;
using p4rpc.trip2.debugui.Interfaces;
using RyoTune.Reloaded;
using UE.Toolkit.Core.Types.Unreal.Factories.Interfaces;

namespace p4rpc.trip2.debug.uobjectviewer;

public class App : GUIApp
{
    public override string Name => "UObject Viewer";

    internal Context Context { get; private init; }

    internal readonly Dictionary<nint, IUObject> LoadedObjects;

    public override void Tick(float DeltaTime)
    {
        
    }

    public App(Context context) : base(context.GUIState)
    {
        Context = context;
        LoadedObjects = [];
        Context.UnrealObjects.OnObjectLoaded += uobject =>
        {
            unsafe
            {
                var Instance = Context.UnrealFactory.CreateUObject((nint)uobject.Self);
                LoadedObjects[Instance.Ptr] = Instance;
            }
        };
        Context.UnrealObjects.OnObjectBeginDestroy += uobject =>
        {
            unsafe
            {
                var Instance = (nint)uobject.Self;
                if (!LoadedObjects.Remove(Instance))
                {
                    // Log.Error($"UObject::BeginDestroy was called on object at 0x{Instance:x} but is not in the UObject registry!");
                }
            }
        };

        var UObjectArrayWindow = () =>
        {
            if (Windows.Count == 0)
                Windows.Add(new GUObjectArrayWindow(this));
        };
        UObjectArrayWindow();
        Buttons.Add("Object Array", UObjectArrayWindow);
    }
}

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
        ImGui.Text($"{owner.LoadedObjects.Count} objects (GUObjectArray has {owner.Context.UnrealObjects.GUObjectArray.NumElements} elements)");
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg;
        if (ImGui.BeginTable("##UObject List", 3, (int)flags, ImGui.ImVec2ImVec2Float(0, 0), 0))
        {
            foreach (var (Index, Column) in TABLE_COLUMNS.Select((x, i) => (i, x)))
                ImGui.TableSetupColumn(Column, 0, 0, (uint)Index);    
            ImGui.TableHeadersRow();
            var Entries = owner.LoadedObjects.ToList();
            unsafe
            {
                var clipper = new ImGuiListClipper.__Internal();
                ImGui.__Internal.ImGuiListClipperBegin((nint)(&clipper), owner.LoadedObjects.Count, 0);
                while (ImGui.__Internal.ImGuiListClipperStep((nint)(&clipper)))
                {
                    for (var k = clipper.DisplayStart; k < clipper.DisplayEnd; k++)
                    {
                        var Entry = Entries[k];
                        ImGui.TableNextRow(0, 0);
                        ImGui.TableSetColumnIndex(0);
                        if (ImGui.SelectableBool($"{Entry.Value.NamePrivate}", false,
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

public class UObjectWindow : GUIWindow<App>
{
    private IUObject Object;
    private string TitleCached;
    public override string Title => TitleCached;

    public override void Draw(App owner)
    {
        if (!owner.LoadedObjects.ContainsKey(Object.Ptr))
        {
            Close();
            return;
        }

        foreach (var Property in Object.ClassPrivate.PropertyLink)
        {
            ImGui.Text($"0x{Property.Offset_Internal:x}: {Property.ClassPrivate.Name} {Property.NamePrivate}");
        }
    }

    public UObjectWindow(IUObject uobject, App owner) : base(owner)
    {
        Object = uobject;
        var Class = Object.ClassPrivate;
        TitleCached = $"{Object.NamePrivate} @ 0x{Object.Ptr:x}";
    }
}