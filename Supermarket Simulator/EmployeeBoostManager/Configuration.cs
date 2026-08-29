using BepInEx.Configuration;

namespace EmployeeBoostManager;

public readonly struct Configuration(ConfigFile source)
{
    public readonly ConfigEntry<float> MouseWheelSensitivity = source.Bind
    (
        "UI",
        "MouseWheelSensitivity",
        350f,
        new ConfigDescription
        (
            "Mouse-wheel scroll sensitivity for the worker list. Higher is faster; does not affect mouse dragging. Applied when the menu opens.",
            new AcceptableValueRange<float>(1f, 5000f)
        )
    );

    public readonly ConfigEntry<string> MenuBinding = source.Bind
    (
        "Bindings",
        "BoostMenu",
        "<Keyboard>/leftShift+<Keyboard>/b",
        "Binding to open or close the employee boost menu"
    );

    public readonly ConfigEntry<float> BoostPricePerWorker = source.Bind
    (
        "Settings",
        "BoostPricePerWorker",
        50f,
        "Price per successful built-in boost through this menu (single-player). Adds the worker's normal boost amount; full meters are skipped. Does not change the game's normal interaction price."
    );
}
