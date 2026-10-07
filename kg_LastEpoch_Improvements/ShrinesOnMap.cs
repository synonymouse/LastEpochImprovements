#if SPECIALVERSION
using Il2Cpp;
using Il2CppDMM;
using MelonLoader;
using Object = UnityEngine.Object;
namespace kg_LastEpoch_Improvements;
public static class ShrinesOnMap
{
    private static Sprite icon;
    private sealed class ShrineMarker
    {
        public GameObject Target;
        public GameObject Icon;
        public long Request;
        public bool Pending;
        public bool Consumed;
    }

    private static Dictionary<ShrineSync, ShrineMarker> _shrines = new();

    private static Sprite GetIcon()
    {
        if (icon) return icon;
        var allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var s in allSprites)
        {
            if (s.name == "DivineShrine")
            {
                icon = s;
                return icon;
            }
        }
        // Fallback: simple diamond if DivineShrine not found
        int size = 16;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - center) / center;
                float dy = Mathf.Abs(y - center) / center;
                texture.SetPixel(x, y, dx + dy <= 1f ? Color.white : new Color(0, 0, 0, 0));
            }
        texture.Apply();
        icon = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        icon.name = "ShrinesOnMapIcon";
        return icon;
    }

    private static void QueueIcon(ShrineSync shrine)
    {
        if (!kg_LastEpoch_Improvements.ShowShrinesOnMap.Value || !shrine) return;
        if (!_shrines.TryGetValue(shrine, out ShrineMarker marker))
            _shrines[shrine] = marker = new ShrineMarker();
        if (marker.Consumed || marker.Pending) return;
        if (marker.Icon && marker.Target == shrine.ShrineObject) return;
        if (marker.Icon) Object.Destroy(marker.Icon);
        marker.Icon = null;
        marker.Target = null;
        marker.Pending = true;
        long request = ++marker.Request;
        MelonCoroutines.Start(AddIconWhenReady(shrine, marker, request));
    }

    private static IEnumerator AddIconWhenReady(ShrineSync shrine, ShrineMarker marker, long request)
    {
        try
        {
            float deadline = Time.unscaledTime + 10f;
            // SetShrineObject can run before the shrine description and minimap
            // have completed initialization. Never instantiate a partial marker.
            yield return null;
            while (Time.unscaledTime < deadline)
            {
                if (!shrine || marker.Request != request || marker.Consumed || !kg_LastEpoch_Improvements.ShowShrinesOnMap.Value) yield break;
                GameObject shrineObject = shrine.ShrineObject;
                if (shrineObject && TryAddIcon(shrineObject, marker)) yield break;
                yield return null;
            }
        }
        finally
        {
            if (marker.Request == request) marker.Pending = false;
        }
    }

    private static bool TryAddIcon(GameObject shrineObject, ShrineMarker marker)
    {
        var map = DMMap.Instance;
        if (!map || !map.iconContainer || !kg_LastEpoch_Improvements.CustomMapIcon) return false;
        if (marker.Icon && marker.Target == shrineObject) return true;

        var info = shrineObject.GetComponentInChildren<BaseDisplayInformation>(true);
        if (info == null) return false;
        string description = info.GetLocalizedDescription();
        if (string.IsNullOrWhiteSpace(description)) description = info.description;
        if (string.IsNullOrWhiteSpace(description)) return false;

        GameObject customMapIcon = null;
        try
        {
            customMapIcon = Object.Instantiate(kg_LastEpoch_Improvements.CustomMapIcon, map.iconContainer.transform);
            customMapIcon.SetActive(true);
            var processor = customMapIcon.AddIconProcessor();
            processor.Init(shrineObject, null, keepWhenInactive: true);
            processor.SetCustomText(description, Color.green, 12);
            customMapIcon.GetComponent<Image>().enabled = false;
            customMapIcon.transform.GetChild(0).GetComponent<Image>().sprite = GetIcon();
            customMapIcon.name = $"shrine_{shrineObject.name}";
            marker.Target = shrineObject;
            marker.Icon = customMapIcon;
            return true;
        }
        catch (Exception ex)
        {
            if (customMapIcon) Object.Destroy(customMapIcon);
            MelonLogger.Error($"Error creating shrine marker: {ex}");
            return true;
        }
    }

    [HarmonyPatch(typeof(ShrineSync), nameof(ShrineSync.SetShrineObject), typeof(GameObject))]
    private static class ShrineSync_SetShrineObject_Patch
    {
        private static void Postfix(ShrineSync __instance)
        {
            QueueIcon(__instance);
        }
    }

    [HarmonyPatch(typeof(ShrineSync), nameof(ShrineSync.PlaceClientShrine))]
    private static class ShrineSync_PlaceClientShrine_Patch
    {
        private static void Postfix(ShrineSync __instance) => QueueIcon(__instance);
    }

    private static void RemoveIcon(ShrineSync shrine, bool consumed = false)
    {
        if (!_shrines.TryGetValue(shrine, out ShrineMarker marker))
            _shrines[shrine] = marker = new ShrineMarker();
        marker.Consumed |= consumed;
        marker.Request++;
        marker.Pending = false;
        if (marker.Icon) Object.Destroy(marker.Icon);
        marker.Icon = null;
        marker.Target = null;
    }

    public static void Prune()
    {
        foreach (var shrine in _shrines.Keys.Where(shrine => !shrine).ToArray())
        {
            RemoveIcon(shrine);
            _shrines.Remove(shrine);
        }
    }

    [HarmonyPatch(typeof(ShrineSync), nameof(ShrineSync.UseShrineClient))]
    private static class ShrineSync_UseShrineClient_Patch
    {
        private static void Postfix(ShrineSync __instance) => RemoveIcon(__instance, consumed: true);
    }

    [HarmonyPatch(typeof(ShrineSync), nameof(ShrineSync.OnInteraction))]
    private static class ShrineSync_OnInteraction_Patch
    {
        private static void Postfix(ShrineSync __instance, GameObject entity)
        {
            try
            {
                RemoveIcon(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error removing shrine icon: {ex.Message}");
            }
        }
    }
}
#endif
