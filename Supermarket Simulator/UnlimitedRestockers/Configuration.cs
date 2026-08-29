using BepInEx.Configuration;

namespace UnlimitedRestockers;

public readonly struct Configuration(ConfigFile Source)
{
    private const string BindingsSectionName = "Bindings";
    private const string SettingsSectionName = "Settings";

    public readonly ConfigEntry<bool> TraceHiring = Source.Bind
    (
        "Diagnostics",
        "TraceHiring",
        true,
        "Log detailed hiring/spawning calls. Logging only: does not disable hiring. Restart the game after changing this setting."
    );

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

    public readonly ConfigEntry<string> RestockerMenuBinding = Source.Bind
    (
        BindingsSectionName,
        "RestockerMenu",
        "<Keyboard>/leftShift+<Keyboard>/r",
        "Binding to open or close the active restockers menu"
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
        "Cool-down time (in seconds)"
    );

    public readonly ConfigEntry<float> DailyWage = Source.Bind
    (
        SettingsSectionName,
        "DailyWage",
        150f,
        "Daily wage for newly hired restockers"
    );
}
