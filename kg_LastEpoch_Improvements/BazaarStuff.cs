using Il2Cpp;
using Il2CppLE.Services.Bazaar;
using Il2CppLE.UI.Bazaar;
using Il2CppLE.UI.MultiPicker;
using Il2CppLE.UI.PanelSystem;
using MelonLoader;
using UnityEngine.SceneManagement;
using State = Il2CppLE.UI.MultiPicker.State;

namespace kg_LastEpoch_Improvements;

public static class BazaarStuff
{
    private static object LastSearchPressRoutine;
    private static BazaarUI GetBazaarUI() => UIBase.instance?.PanelSystem?.GetPanelIfOpen<BazaarPanel>()?.BazaarUI;
    public static void Update()
    {
        if (!Input.GetKey(KeyCode.LeftShift) || !Input.GetKeyDown(KeyCode.Mouse2)) return;
        if (SceneManager.GetActiveScene().name != "Bazaar") return;
        if (UIBase.instance?.TooltipSystem?.ActiveItemTooltipItem is not { } currentItem) return;
        BazaarStallType? type = currentItem.ToStall();
        if (type == null) return;
        UIBase.instance.closeInventory();
        UIBase.instance.openBazaar(new Il2CppSystem.Nullable<BazaarStallType>(type.Value));
        if (LastSearchPressRoutine != null) MelonCoroutines.Stop(LastSearchPressRoutine); 
        LastSearchPressRoutine = MelonCoroutines.Start(PressSearchAfterLoadDone(currentItem, type.Value));
    }
    private static IEnumerator IncludeModsInSearch(List<ItemAffix> mods, Action<bool> completed)
    {
        if (mods == null || mods.Count == 0)
        {
            completed(true);
            yield break;
        }
        BazaarUI bazaarUI = GetBazaarUI();
        if (!bazaarUI) yield break;
        bazaarUI.filterUI.affixesPicker.multiPickerOpener.openPickerButton.onClick.Invoke();
        float deadline = Time.unscaledTime + 10f;
        MultiPickerModal modal = null;
        while (modal == null && Time.unscaledTime < deadline)
        {
            if (!GetBazaarUI()) yield break;
            modal = UIBase.instance.PanelSystem.GetPanelIfOpen<MultiPickerModal>();
            if (modal == null) yield return null;
        }
        if (modal == null) yield break;
        State state = modal._multipicker.CurrentState;
        foreach (ItemAffix mod in mods)
        {
            if (!state.Entries.TryGetValue(mod.affixId, out StatefulEntry val)) yield break;
            val.selected = true;
            val.data = new AffixData() { tier = mod.DisplayTier };
        }
        modal._multipicker.confirmButton.onClick.Invoke();
        completed(true);
    }
    
    private static IEnumerator PressSearchAfterLoadDone(ItemDataUnpacked item, BazaarStallType stallType)
    {
        yield return null; yield return null; yield return null;
        float deadline = Time.unscaledTime + 10f;
        while (!GetBazaarUI() && Time.unscaledTime < deadline) yield return null;
        while (true)
        {
            BazaarUI bazaarUI = GetBazaarUI();
            if (!bazaarUI || !bazaarUI.gameObject.activeSelf || item == null)
                yield break;
            if (!bazaarUI.IsLoadingIndicatorActive)
            {
                yield return new WaitForSeconds(0.5f);
                bazaarUI = GetBazaarUI();
                if (!bazaarUI || !bazaarUI.gameObject.activeSelf) yield break;
                bazaarUI.FilterUI.ResetUI(false, new Il2CppSystem.Nullable<BazaarStallType>(stallType), true);
                if (item.isUniqueSetOrLegendary()) bazaarUI.FilterUI.uniquesPicker.SelectedUniques = new(1) { [0] = item.uniqueID };
                bazaarUI.FilterUI.legendaryPotentialRange.MinValue = new(item.legendaryPotential);
                bazaarUI.FilterUI.sortingSelection.dropdown.value = 1;
                bazaarUI.FilterUI.sortingSelection.SelectedSorting = SortingSelection.GOLD_LOW_FIRST;
                bazaarUI.FilterUI.raritySelection.dropdown.value = (int)BazaarItemRarityHelper.GetBazaarItemRarity(item) + 1;
                bazaarUI.FilterUI.raritySelection.SelectedRarity = new(BazaarItemRarityHelper.GetBazaarItemRarity(item));

                if (item.isExaltedItem())
                { 
                    List<ItemAffix> _6TierPlusMods = [];
                    foreach (ItemAffix affix in item.affixes) if (affix.DisplayTier >= 6) _6TierPlusMods.Add(affix);
                    bool selected = false;
                    IEnumerator routine = IncludeModsInSearch(_6TierPlusMods, success => selected = success);
                    while (routine.MoveNext()) yield return routine.Current;
                    if (!selected) yield break;
                }

                if (item.itemType.IsIdol())
                {
                    List<ItemAffix> _allMods = [];
                    foreach (ItemAffix affix in item.affixes) if (affix.DisplayTier > 0) _allMods.Add(affix);
                    bool selected = false;
                    IEnumerator routine = IncludeModsInSearch(_allMods, success => selected = success);
                    while (routine.MoveNext()) yield return routine.Current;
                    if (!selected) yield break;
                }
                
                yield return new WaitForSeconds(0.5f);
                bazaarUI = GetBazaarUI();
                if (!bazaarUI || !bazaarUI.gameObject.activeSelf) yield break;
                bazaarUI.SearchPress();
                yield break;  
            }
            yield return null; 
        }
    }
    
    [HarmonyPatch(typeof(TooltipItemManager),nameof(TooltipItemManager.OpenItemTooltip))]
    private static class TooltipItemManager_OpenItemTooltip_Patch
    {
        private static void Prefix(ItemDataUnpacked data, out string __state)
        {
            __state = null;
            if (data?.ToStall() == null || data.LoreText == null || SceneManager.GetActiveScene().name != "Bazaar") return;
            __state = data.LoreText;
            data.LoreText += $"\n<color=yellow><size=18>Shift+Middle Mouse to search for this item in the Bazaar</size></color>\n" +
                             $"<color=#808080><size=12>This also will include current item rarity , legendary potential and unique item name (if its unique or legendary)</size></color>";
        }
        private static void Finalizer(ItemDataUnpacked data, string __state)
        {
            if (__state != null) data.LoreText = __state;
        }
    }
}
