#if SPECIALVERSION
using Il2Cpp;
using Il2CppDMM;
using MelonLoader;
using Object = UnityEngine.Object;
namespace kg_LastEpoch_Improvements;
public static class RaresOnMap
{
    private static Sprite icon;
    private static Dictionary<ActorSync, long> pending = new();
    private static long nextRequest;
    private static Dictionary<ActorSync, GameObject> markers = new();

    public static void Prune()
    {
        foreach (var actor in pending.Keys.Where(actor => !actor || actor.IsDead).ToArray()) pending.Remove(actor);
        foreach (var actor in markers.Keys.Where(actor => !actor || actor.IsDead || !markers[actor]).ToArray())
        {
            if (markers[actor]) Object.Destroy(markers[actor]);
            markers.Remove(actor);
        }
    }
    private static Sprite GetIcon()
    {
        if (icon) return icon;
        int size = 8;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = (size - 1) / 2f;
        float radius = center;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                texture.SetPixel(x, y, dist <= radius ? Color.white : new Color(0, 0, 0, 0));
            }
        texture.Apply();
        icon = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        icon.name = "RaresOnMapIcon";
        return icon;
    }
    
    private static IEnumerator AddWhenReady(ActorSync actor, byte rarity, long request)
    {
        try
        {
            while (true)
            {
                if (!actor || actor.IsDead || !pending.TryGetValue(actor, out long current) || current != request ||
                    !kg_LastEpoch_Improvements.ShowRaresOnMap.Value) yield break;
                if (markers.TryGetValue(actor, out GameObject existing) && existing) yield break;
                var visuals = actor.actorVisuals;
                var map = DMMap.Instance;
                if (visuals && visuals.gameObject.activeSelf && map && map.iconContainer && kg_LastEpoch_Improvements.CustomMapIcon)
                {
                    AddIcon(actor, visuals.gameObject, rarity, map.iconContainer.transform);
                    yield break;
                }
                yield return null;
            }
        }
        finally
        {
            if (pending.TryGetValue(actor, out long current) && current == request) pending.Remove(actor);
        }
    }

    private static void AddIcon(ActorSync actor, GameObject visuals, byte rarity, Transform parent)
    {
        GameObject customMapIcon = null;
        try
        {
            customMapIcon = Object.Instantiate(kg_LastEpoch_Improvements.CustomMapIcon, parent);
            customMapIcon.SetActive(true);
            var processor = customMapIcon.AddIconProcessor();
            processor.Init(visuals, null, keepWhenInactive: true);
            processor.TrackActor(actor);
            customMapIcon.GetComponent<Image>().enabled = false;
            var iconImage = customMapIcon.transform.GetChild(0).GetComponent<Image>();
            iconImage.sprite = GetIcon();
            iconImage.color = new Color(1f, 0.3f, 0.3f);
            iconImage.rectTransform.sizeDelta = new Vector2(8, 8);
            customMapIcon.name = $"rare_{actor.gameObject.name}_{rarity}";
            markers[actor] = customMapIcon;
        }
        catch (Exception ex)
        {
            if (customMapIcon) Object.Destroy(customMapIcon);
            MelonLogger.Error($"Error creating rare monster marker: {ex}");
        }
    }

    [HarmonyPatch(typeof(ActorSync),nameof(ActorSync.ReceiveInitDisplayInformation))]
    private static class ActorSync_MessageSyncRarit 
    { 
        private static void Postfix(ActorSync __instance, byte rarity)
        {
            if (!kg_LastEpoch_Improvements.ShowRaresOnMap.Value || __instance is PlayerActorSync || rarity <= 1) return;
            if (markers.TryGetValue(__instance, out GameObject existing) && existing) return;
            if (pending.ContainsKey(__instance)) return;
            long request = ++nextRequest;
            pending[__instance] = request;
            MelonCoroutines.Start(AddWhenReady(__instance, rarity, request));
        }
    }
}
#endif
