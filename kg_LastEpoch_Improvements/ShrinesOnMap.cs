#if SPECIALVERSION
using Il2Cpp;
using Il2CppDMM;
using MelonLoader;
using Object = UnityEngine.Object;
namespace kg_LastEpoch_Improvements;
public static class ShrinesOnMap
{
    private static Sprite icon;
    private static Dictionary<GameObject, GameObject> _shrineIconMap = new();

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

    [HarmonyPatch(typeof(ShrineSync),nameof(ShrineSync.PlaceClientShrine))]
    private static class ShrineSync_MessageSyncRarit
    {
        private static void Postfix(ShrineSync __instance)
        {
            try
            {
                if (!kg_LastEpoch_Improvements.ShowShrinesOnMap.Value) return;
                GameObject customMapIcon = Object.Instantiate(kg_LastEpoch_Improvements.CustomMapIcon, DMMap.Instance.iconContainer.transform);
                customMapIcon.SetActive(true);

                var customMapIconComponent = customMapIcon.GetIconProcessor();

                customMapIconComponent.Init(__instance.ShrineObject.gameObject, null);
                if (__instance.ShrineObject?.GetComponent<DisplayInformation>() is {} info)
                    customMapIconComponent.SetCustomText(info.description, Color.green, 12);
                customMapIcon.GetComponent<Image>().enabled = false;
                customMapIcon.transform.GetChild(0).GetComponent<Image>().sprite = GetIcon();
                customMapIcon.name = $"shrine_{__instance.gameObject.name}";

                if (__instance.ShrineObject != null)
                    _shrineIconMap[__instance.ShrineObject] = customMapIcon;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ShrineSync patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(ShrineSync), nameof(ShrineSync.OnInteraction))]
    private static class ShrineSync_OnInteraction_Patch
    {
        private static void Postfix(ShrineSync __instance, GameObject entity)
        {
            try
            {
                if (__instance.ShrineObject != null && _shrineIconMap.TryGetValue(__instance.ShrineObject, out GameObject mapIcon))
                {
                    if (mapIcon != null && mapIcon.activeSelf)
                    {
                        Object.Destroy(mapIcon);
                    }
                    _shrineIconMap.Remove(__instance.ShrineObject);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error removing shrine icon: {ex.Message}");
            }
        }
    }
}
#endif
