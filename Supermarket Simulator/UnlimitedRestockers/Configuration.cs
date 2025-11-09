using BepInEx.Configuration;

namespace UnlimitedRestockers;

public readonly struct Configuration(ConfigFile Source)
{
    private const string BindingsSectionName = "Bindings";
    private const string SettingsSectionName = "Settings";

    public readonly ConfigEntry<string> HireRestockerBinding = Source.Bind
    (
        BindingsSectionName, 
        "HireRestocker", 
        "<Keyboard>/leftShift+<Keyboard>/h", 
        "Binding to hire restocker"
    );

    public readonly ConfigEntry<string> FireRestockerBinding = Source.Bind
    (
        BindingsSectionName,
        "FireRestocker",
        "<Keyboard>/leftShift+<Keyboard>/f",
        "Binding to fire restocker"
    );

    public readonly ConfigEntry<float> HireCost = Source.Bind
    (
        SettingsSectionName,
        "HiringCost",
        150f,
        "Cost (in dollars) to hire a restocker"
    );

    public readonly ConfigEntry<float> HireCooldown = Source.Bind
    (
        SettingsSectionName,
        "HireCooldown",
        1f,
        "Cool-down time (rate per second)"
    );
}
