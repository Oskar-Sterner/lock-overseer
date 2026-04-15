using DeadworksManaged.Api;

namespace LockOverseer;

public class LockOverseerPlugin : DeadworksPluginBase
{
    public override string Name => "LockOverseer";

    public override void OnLoad(bool isReload)
    {
        System.Console.WriteLine($"[{Name}] {(isReload ? "Reloaded" : "Loaded")}");
    }

    public override void OnUnload()
    {
        System.Console.WriteLine($"[{Name}] Unloaded");
    }
}
