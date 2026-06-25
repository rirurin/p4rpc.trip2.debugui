using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X64;
using riri.yamlscans.ReloadedII;
using RyoTune.Reloaded;

namespace p4rpc.trip2.debugui;

public class Initialize
{
    private Context Context;
    private SHFunction2<FEngineLoop_Tick> _FEngineLoop_Tick;
    
    [Function(CallingConventions.Microsoft)]
    private delegate byte FEngineLoop_Tick(nint self);

    private byte FEngineLoop_TickImpl(nint self)
    {
        Trip2DebugGui.fengineloop_tick();
        return _FEngineLoop_Tick.Hook!.OriginalFunction(self);
    }

    public Initialize(Context context)
    {
        Context = context;
        _FEngineLoop_Tick = new(FEngineLoop_TickImpl);
    }
}
