extern alias imgui;
using imgui::p4rpc.trip2.ImGui;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;
using p4rpc.trip2.debugui.Interfaces;

namespace p4rpc.trip2.debug.helloworld;

public class App : GUIApp 
{
    public override string Name => "Hello App";
    public float CountTotal;
    public float CountMult = 1;
    public override void Tick(float DeltaTime)
    {
        CountTotal += DeltaTime * CountMult;
    }

    public App()
    {
        var OpenWindow = () =>
        {
            if (Windows.Count == 0)
                Windows.Add(new AppWindow(this));
        };
        OpenWindow();
        Buttons.Add("Open Window", OpenWindow);
    }
}

public class AppWindow(App owner) : GUIWindow<App>(owner)
{
    public override string Title => "Sample Window";

    private const string EXPECTED_GAME = "Persona 4 Revival";
    
    public override void DrawContents()
    {
        if (!Owner.TryGetTarget(out var State)) return;
        ImGui.Text($"File Version is {GameVersion.GetFileVersion()}");
        var ProductName = GameVersion.GetLocalizedProperty("ProductName");
        ImGui.Text($"Product Name is \"{ProductName}\". {(ProductName == EXPECTED_GAME ? "Welcome home!" : $"This is not {EXPECTED_GAME}!")}");
        var regionAvail = new ImVec2.__Internal();
        unsafe { ImGui.__Internal.GetContentRegionAvail((nint)(&regionAvail)); }
        ImGui.SetNextItemWidth(regionAvail.x / 3);
        ImGui.SliderFloat("Multiplier", ref State.CountMult, 0.25f, 4, "%f", 0);
        ImGui.SameLine(0, 10);
        ImGui.Text($"Total: {State.CountTotal}");
    }
}