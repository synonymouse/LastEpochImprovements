using LastEpochInteropRepair;
using MelonLoader;
using MelonLoader.Utils;

[assembly: MelonInfo(typeof(AutoRepairUnityInterop.RepairPlugin), "KG.AutoRepairUnityInterop", "0.0.1", "Local compatibility")]
[assembly: MelonPriority(-1000)]

namespace AutoRepairUnityInterop;

public sealed class RepairPlugin : MelonPlugin
{
    public override void OnPreModsLoaded()
    {
        var assembly = Path.Combine(MelonEnvironment.Il2CppAssembliesDirectory, "UnityEngine.CoreModule.dll");
        if (File.Exists(assembly))
        {
            CoreModuleRepair.Run(new[] { assembly, "--apply" });
            MelonLogger.Msg("Unity CoreModule metadata checked before mod loading.");
        }
    }
}
