using BepInEx.Configuration;
using UnityEngine;

namespace PricesManagement;

public readonly struct Configuration(ConfigFile Source)
{
    private const string SettingsSectionName = "Settings";

    public readonly ConfigEntry<float> GlobalDiscount = Source.Bind
    (
        SettingsSectionName,
        "GlobalProductOrderingDiscount",
        50f,
        "Global discount ONLY for product ordering (in %)"
    );

    public float GlobalDiscountMultiplier
    {
        get
        {
            var percentage = Mathf.Clamp(GlobalDiscount.Value, 0f, 100f);

            return 1f - percentage / 100f;
        }
    }
}
