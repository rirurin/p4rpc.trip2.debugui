extern alias imgui;
using System.Numerics;
using System.Runtime.InteropServices;
using imgui::p4rpc.trip2.ImGui;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using p4rpc.trip2.debugui.Interfaces;
using Reloaded.Mod.Interfaces;

namespace p4rpc.trip2.debug.reloadedconsole;

// Adds a window that prints the output of the Reloaded-II console into the Debug UI
// Adapted from Metaphor Multiplayer
public class Logging : GUIApp 
{
    public override string Name { get; } = "Reloaded-II Console";
    public List<string> ReloadedConsole { get; } = [];
    public int LineCount { get; set; } = 32;
    private ILogger Logger;

    public Logging(ILogger logger, IGUIState state) : base(state)
    {
        Logger = logger;
        Logger.OnWrite += (_, message) =>
        {
            if (ReloadedConsole.Count == 0)
                ReloadedConsole.Add(string.Empty);
            ReloadedConsole[^1] = ReloadedConsole.Last() + message.text;
        };
        Logger.OnWriteLine += (_, message) =>
        {
            ReloadedConsole.Add(message.text);
            while (ReloadedConsole.Count > LineCount)
                ReloadedConsole.RemoveAt(0);
        };
        var OpenWindow = () =>
        {
            if (Windows.Count == 0)
                Windows.Add(new LoggingWindow(this));
        };
        OpenWindow();
        Buttons.Add("Open Window", OpenWindow);
    }
    public override void Tick(float DeltaTime) {}
}

public class LoggingWindow(Logging owner) : GUIWindow<Logging>(owner)
{
    public override string Title => "Reloaded-II Console";

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
            return new Vector2(SurfaceSize.X / 2 - 30, 30);
        }
    }


    public override void Draw(Logging owner)
    {
        var AsSingleLine = string.Join("\n", owner.ReloadedConsole) + '\0';
        var SingleLineAlloc = Marshal.StringToHGlobalAnsi(AsSingleLine);
        var regionAvail = new ImVec2.__Internal();
        unsafe
        {
            ImGui.__Internal.GetContentRegionAvail((nint)(&regionAvail));
            owner.LineCount = (int)regionAvail.y / (int)ImGui.GetFont().FontSize - 1;
            ImGui.__Internal.InputTextMultiline(
                "##ReloadedConsoleOutput",
                (sbyte*)SingleLineAlloc,
                AsSingleLine.Length,
                regionAvail,
                (int)ImGuiInputTextFlags.ReadOnly,
                nint.Zero,nint.Zero
            );
        }
    }
}