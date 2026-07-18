extern alias imgui;
using imgui::p4rpc.trip2.ImGui;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using System.Numerics;
using System.Runtime.InteropServices;
using p4rpc.trip2.debug.uobjectviewer.View;
using p4rpc.trip2.debugui.Interfaces;
using RyoTune.Reloaded;
using UE.Toolkit.Core.Types.Unreal.Factories;
using UE.Toolkit.Core.Types.Unreal.Factories.Interfaces;
using UE.Toolkit.Core.Types.Unreal.UE5_4_4;
using UE.Toolkit.Interfaces;

namespace p4rpc.trip2.debug.uobjectviewer;

public class App : GUIApp
{
    public override string Name => "UObject Viewer";

    internal Context Context { get; }

    internal readonly Dictionary<nint, IUObject> VisibleObjects;

    internal readonly Dictionary<nint, IUObject> AllObjects;
    
    internal ObjectSearch ObjectSearch { get; }
    
    internal TypeName TypeName { get; }

    internal bool InitialLoad = false;

    public override void Tick(float DeltaTime) {}

    public App(Context context) : base(context.GUIState)
    {
        Context = context;
        TypeName = new(Context.UnrealFactory);
        VisibleObjects = [];
        AllObjects = [];
        ObjectSearch = new(new(this));
        Context.UnrealObjects.OnObjectLoaded += uobject =>
        {
            unsafe
            {
                var Instance = Context.UnrealFactory.CreateUObject((nint)uobject.Self);
                if (ObjectSearch.SearchMatches(Instance.NamePrivate.ToString()))
                    VisibleObjects[Instance.Ptr] = Instance;
                AllObjects[Instance.Ptr] = Instance;
            }
        };
        Context.UnrealObjects.OnObjectBeginDestroy += uobject =>
        {
            unsafe
            {
                var Instance = (nint)uobject.Self;
                if (!VisibleObjects.Remove(Instance))
                {
                    // Log.Error($"UObject::BeginDestroy was called on object at 0x{Instance:x} but is not in the UObject registry!");
                }

                if (!AllObjects.Remove(Instance))
                {
                    
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

    internal void RecreateObjectList(Dictionary<nint, IUObject> List, Func<IUObject, bool> Callback)
    {
        List.Clear();
        var GUObjectArray = Context.UnrealObjects.GUObjectArray;
        for (var i = 0; i < GUObjectArray.NumElements; i++)
        {
            var CurrentObject = GUObjectArray.IndexToObject(i);
            if (CurrentObject != null && Callback(CurrentObject))
                List[CurrentObject.Ptr] = CurrentObject;
        }
    }
}

public class UObjectWindowColumn(string name, Func<Vector2, float> getWidth)
{
    public string Name { get; } = name;
    public Func<Vector2, float> GetWidth { get; } = getWidth;
}

public abstract class PropertyListView(PropertyListView? parent)
{
    internal readonly Dictionary<PropertyListKey, PropertyListView> Children = [];
    internal PropertyListView? Parent { get; set; } = parent;
    
    public abstract void Draw(App owner, UObjectWindow window);

    public abstract PropertyListKey GetKey();
}

public class PropertyListKey(nint address, string name)
{
    public nint Address => address;
    public string Name => name;

    public override string ToString() => $"{Name} @ 0x{Address:x}";

    public override bool Equals(object? obj)
    {
        if (obj.GetType() != GetType()) return false;
        var Other = (PropertyListKey)obj;
        return Address.Equals(Other.Address) && Name.Equals(Other.Name);
    }
    
    public override int GetHashCode() => HashCode.Combine(Address, Name);
}