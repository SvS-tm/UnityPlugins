using HarmonyLib;

namespace PricesManagement;

public class Patches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ProductSO), $"get_{nameof(ProductSO.BoxPrice)}")]
    public static void Postfix(ref float __result)
    {
        __result *= Plugin.Configuration.GlobalDiscountMultiplier;

        Plugin.Logger.LogInfo($"{nameof(ProductSO.BoxPrice)} patched. New result: {__result}");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PriceManager), nameof(PriceManager.TotalBoxPrice))]
    public static void TotalBoxPrice_Postfix(ref float __result)
    {
        __result *= Plugin.Configuration.GlobalDiscountMultiplier;

        Plugin.Logger.LogInfo($" patched. New result: {__result}");
    }
}
