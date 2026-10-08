#if SPECIALVERSION
using Il2Cpp;
using Minimap = Il2CppLE.UI.Minimap.Minimap;

namespace kg_LastEpoch_Improvements;

internal static class MapReveal
{
    internal sealed class Request
    {
        internal readonly MinimapInformation Information;

        internal Request(MinimapInformation information)
        {
            Information = information;
        }
    }

    private static readonly Dictionary<int, Request> Pending = new();

    internal static void Queue(Minimap map)
    {
        if (!kg_LastEpoch_Improvements.IsMapRevealEnabled || map == null) return;
        var info = map.minimapInformation;
        if (info == null) return;
        var request = new Request(info);
        Pending[map.GetInstanceID()] = request;
    }

    internal static void Toggle(bool enabled)
    {
        Pending.Clear();
        if (!enabled) return;
        // If no map exists yet, OnMapLoaded will queue the request when it arrives.
        Queue(UnityEngine.Object.FindObjectOfType<Minimap>());
    }

    internal static Request Begin(Minimap map, ref float radius)
    {
        if (!kg_LastEpoch_Improvements.IsMapRevealEnabled || map == null) return null;
        if (!Pending.TryGetValue(map.GetInstanceID(), out var request)) return null;
        var info = map.minimapInformation;
        if (info == null || request.Information == null || info.Pointer != request.Information.Pointer) return null;
        if (map.player == null || info.LiveSdf == null) return null;

        radius = 10000f;
        return request;
    }

    internal static void Complete(Minimap map, Request request)
    {
        if (request == null || map == null) return;
        int id = map.GetInstanceID();
        // A newer load/manual request must not be consumed by an older update.
        if (!Pending.TryGetValue(id, out var current) || !ReferenceEquals(current, request)) return;
        Pending.Remove(id);
    }

    [HarmonyPatch(typeof(Minimap), nameof(Minimap.OnMapLoaded))]
    private static class MapLoadedPatch
    {
        private static void Postfix(Minimap __instance) => Queue(__instance);
    }

    [HarmonyPatch(typeof(Minimap), nameof(Minimap.OnFoWDataReady))]
    private static class FoWReadyPatch
    {
        private static void Postfix(Minimap __instance, MinimapInformation __0)
        {
            var current = __instance.minimapInformation;
            if (current != null && __0 != null && current.Pointer == __0.Pointer)
                Queue(__instance);
        }
    }

    [HarmonyPatch(typeof(Minimap), nameof(Minimap.UpdateFoW))]
    private static class FoWUpdatePatch
    {
        private static void Prefix(Minimap __instance, ref float __2, out Request __state)
        {
            __state = Begin(__instance, ref __2);
        }

        // Postfix is not reached if the game's update throws; the request stays pending.
        private static void Postfix(Minimap __instance, Request __state) => Complete(__instance, __state);
    }

    [HarmonyPatch(typeof(Minimap), nameof(Minimap.OnDestroy))]
    private static class DestroyPatch
    {
        private static void Prefix(Minimap __instance) => Pending.Remove(__instance.GetInstanceID());
    }
}
#endif
