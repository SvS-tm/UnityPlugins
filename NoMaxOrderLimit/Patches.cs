using HarmonyLib;
using UnityEngine;

namespace NoMaxOrderLimit;

public class Patches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MarketShoppingCart), nameof(MarketShoppingCart.TryAddProduct))]
    public static bool Prefix(MarketShoppingCart __instance, ItemQuantity salesItem, SalesType salesType, ref bool __result)
    {
        NoMaxOrderLimitPlugin.Logger.LogInfo($"Try add called: {salesItem.FirstItemCount} {salesType}");

        __instance.AddProduct(salesItem, salesType);

        __result = true;

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MarketShoppingCart), nameof(MarketShoppingCart.CartMaxed))]
    public static bool Prefix(ref bool __result)
    {
        NoMaxOrderLimitPlugin.Logger.LogInfo($"Cart maxed called: {__result}");

        __result = false;

        return false;
    }


    [HarmonyPostfix]
    [
        HarmonyPatch
        (
            typeof(BoxGenerator),
            nameof(BoxGenerator.SpawnBox),
            [
                typeof(FurnitureSO),
                typeof(Vector3),
                typeof(Quaternion),
                typeof(Transform)
            ]
        )
    ]
    public static void Postfix(FurnitureBox __result)
    {
        NoMaxOrderLimitPlugin.Logger.LogInfo($"SpawnBox: {__result.Data.Bucket.LocalizedName.ToString()}");

        if (__result.transform.position.y > 8f)
        {
            float x = __result.transform.position.x;
            float z = __result.transform.position.z;

            __result.transform.position = new Vector3(x, 2f, z);
        }
    }

    [HarmonyPostfix]
    [
        HarmonyPatch
        (
            typeof(BoxGenerator),
            nameof(BoxGenerator.SpawnBox),
            [
                typeof(ProductSO),
                typeof(Vector3),
                typeof(Quaternion),
                typeof(Transform)
            ]
        )
    ]
    public static void Postfix(Box __result)
    {
        NoMaxOrderLimitPlugin.Logger.LogInfo($"SpawnBox: {__result.Data.Product.LocalizedName.ToString()}");

        if (__result.transform.position.y > 8f && !__result.Racked)
        {
            float x = __result.transform.position.x;
            float z = __result.transform.position.z;

            __result.transform.position = new Vector3(x, 2f, z);
        }
    }
}
