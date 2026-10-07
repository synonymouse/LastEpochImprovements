using Il2Cpp;
using Il2CppItemFiltering;
using MelonLoader;
using UnityEngine;
using Il2CppLE.Data;
using Il2CppLE;
using Il2CppSystem;

namespace kg_LastEpoch_Improvements;

public static class PickupItems
{
    public static void Update()
    {
        if (!kg_LastEpoch_Improvements.EnablePickupOnF.Value) return;

        if (Input.GetKey(KeyCode.F))
        {
            PickupAllItemsInRange();
        }
    }

    private static void PickupAllItemsInRange()
    {
        try
        {
            LocalPlayer player = LocalPlayer.instance;
            if (player == null) return;

            Vector3 playerPosition = player.transform.position;
            if (playerPosition == Vector3.zero) return;

            var allItems = GroundItemVisuals.all;

            foreach (GroundItemVisuals visuals in allItems)
            {
                GroundItemLabel item = visuals?.label;
                if (item == null || !item.gameObject.activeSelf)
                    continue;

                if (item.visuals != null && item.visuals.gameObject.activeSelf)
                {
                    Vector3 itemWorldPosition = item.visuals.transform.position;
                    float distance = Vector3.Distance(playerPosition, itemWorldPosition);

                    if (distance <= GroundItemLabel.pickupDistance)
                    {
                        // Небольшая задержка между подъемами предметов для стабильности
                        if (item.visuals.gameObject != null)
                        {
                            item.requestPickup();
                            System.Threading.Thread.Sleep(10);
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            MelonLogger.Warning($"[PickupItems] Error picking up items: {ex.Message}");
        }
    }
}
