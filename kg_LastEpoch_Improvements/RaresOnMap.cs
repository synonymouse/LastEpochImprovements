#if SPECIALVERSION
using Il2Cpp;
using Il2CppDMM;
using MelonLoader;
using Object = UnityEngine.Object;
namespace kg_LastEpoch_Improvements;
public static class RaresOnMap
{
    private static Sprite icon;
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
    
    [HarmonyPatch(typeof(ActorSync),nameof(ActorSync.ReceiveInitDisplayInformation))]
    private static class ActorSync_MessageSyncRarit 
    { 
        private static void Postfix(ActorSync __instance, byte rarity)
        {
            int stage = 0;
            try
            {
                if (!kg_LastEpoch_Improvements.ShowRaresOnMap.Value) return;
                if (__instance is PlayerActorSync || rarity <= 1) return;

                GameObject customMapIcon = Object.Instantiate(kg_LastEpoch_Improvements.CustomMapIcon, DMMap.Instance.iconContainer.transform);
                stage = 3;
                customMapIcon.SetActive(true);
                customMapIcon.GetIconProcessor().Init(__instance.actorVisuals.gameObject, null);
                stage = 4;
                customMapIcon.GetComponent<Image>().enabled = false;
                var iconImage = customMapIcon.transform.GetChild(0).GetComponent<Image>();
                iconImage.sprite = GetIcon();
                iconImage.color = new Color(1f, 0.3f, 0.3f);
                iconImage.rectTransform.sizeDelta = new Vector2(8, 8);
                stage = 5;
                customMapIcon.name = $"rare_{__instance.gameObject.name}_{rarity}";
                stage = 6;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error while trying to show {__instance} ({__instance.gameObject.name}) on map. Stage: {stage}");
            }
        }
    }
}
#endif
