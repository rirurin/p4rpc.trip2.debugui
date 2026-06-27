extern alias imgui;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;
using p4rpc.trip2.debugui.Interfaces;
using RyoTune.Reloaded;

namespace p4rpc.trip2.debug.helloworld;

public class App : IGUIApp
{
    public string Name { get; } = "Hello App";
    public List<IGUIWindow> Windows { get; } = [];
    public void Tick(float DeltaTime)
    {
        if (Windows.Count == 0)
        {
            Windows.Add(new AppWindow());
        }
    }
}

public class AppWindow : IGUIWindow
{
    public WeakReference<IGUIApp>? Owner { get; init; }
    public string Title { get; } = "Hello Window";
    public bool IsOpen { get; set; }
    public void DrawContents()
    {
        ImGui.Text("Hello World!");
        /*
        bool isOpen = true;
        if (!ImGui.Begin("Test Window", ref isOpen, 0))
        {
            ImGui.End();
            return;
        }
        ImGui.Text("Hello World!");
        ImGui.End();
        */
    }
}