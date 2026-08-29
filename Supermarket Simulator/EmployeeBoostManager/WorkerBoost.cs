using Photon.Pun;

namespace EmployeeBoostManager;

// The three segments are native boost tiers, NOT a speed multiplier. Read the
// game's live UI fills: GetBoostAmount/CurrentBoostDurations are cached when a
// boost is added, whereas the countdown updates Images and TimeLeft each frame.
internal sealed class BoostState
{
    public const int SegmentCount = 3; // BoostIndicator itself indexes 0, 1, 2.
    public bool Available { get; init; }
    public bool HasEffectListener { get; init; }
    public bool Running { get; init; }
    public int Level { get; init; }
    public float SecondsLeft { get; init; }
    public float[] Segments { get; init; } = new float[SegmentCount];
    public float Amount => Segments.Sum();
    public bool Full => Running && Amount >= SegmentCount - 0.01f;
    public bool CanBoost => Available && HasEffectListener && !Full;

    public static BoostState Read(BoostIndicator? indicator)
    {
        if (indicator == null || !indicator.enabled)
            return new BoostState();

        var images = indicator.Images;
        var durations = indicator.BoostDurations;
        var currentDurations = indicator.CurrentBoostDurations;
        if (images == null || images.Length < SegmentCount ||
            durations == null || durations.Length < SegmentCount ||
            currentDurations == null || currentDurations.Length < SegmentCount)
            return new BoostState();

        var segments = new float[SegmentCount];
        for (var index = 0; index < SegmentCount; index++)
        {
            if (images[index] == null || !float.IsFinite(images[index].fillAmount) ||
                !float.IsFinite(durations[index]) || durations[index] <= 0)
                return new BoostState();

            segments[index] = Math.Clamp(images[index].fillAmount, 0f, 1f);
        }

        var seconds = indicator.TimeLeft;
        if (!float.IsFinite(seconds))
            return new BoostState();

        var level = indicator.m_CurrentBoostLevel;
        return new BoostState
        {
            Available = true,
            HasEffectListener = indicator.onBoostLevelChanged != null,
            Running = indicator.isActiveAndEnabled && indicator.m_CountdownCoroutine != null &&
                level > 0 && level <= SegmentCount && seconds > 0 && segments.Sum() > 0,
            Level = Math.Clamp(level, 0, SegmentCount),
            SecondsLeft = Math.Max(0f, seconds),
            Segments = segments
        };
    }

    public string Describe()
    {
        if (!Available) return "Unavailable";
        if (!HasEffectListener) return "Worker not initialized";
        if (!Running) return Amount > 0 ? "Inactive boost - not running" : "Ready - level 0/3";
        return $"{(Full ? "FULL" : "BOOSTED")} - level {Level}/3 - {Amount:0.00}/3 - {SecondsLeft:0}s left";
    }
}

internal static class WorkerBoost
{
    public static bool TryPurchase(string key, BoostIndicator? indicator, Action applyNativeBoost,
        float price, out string message)
    {
        message = string.Empty;
        if (PhotonNetwork.IsConnected)
        {
            message = "Custom-price boosts currently support single-player only.";
            return false;
        }

        if (!float.IsFinite(price) || price < 0)
        {
            message = "BoostPricePerWorker must be finite and non-negative.";
            return false;
        }

        var money = MoneyManager.Instance;
        if (money == null)
        {
            message = "Money manager is not available.";
            return false;
        }

        try
        {
            var before = BoostState.Read(indicator);
            if (!before.CanBoost)
            {
                message = before.Full ? "Boost meter is already full." : "Worker boost is not ready.";
                return false;
            }
            if (!money.HasMoney(price))
            {
                message = "Not enough money.";
                return false;
            }

            // These native worker methods activate the indicator then AddBoost.
            // The game's coroutine invokes the worker's existing speed callback.
            // They do not charge money or broadcast to Photon despite some names
            // ending in 'Network'. Never use an Others-only broadcast as a local boost.
            applyNativeBoost();
            var after = BoostState.Read(indicator);
            if (!after.Running || !after.HasEffectListener ||
                (before.Running && after.Amount <= before.Amount + 0.0001f &&
                    after.SecondsLeft <= before.SecondsLeft + 0.01f))
            {
                message = "The game did not activate/increase the boost; no charge.";
                Plugin.Logger.LogWarning($"[BoostFix v2] {key}: {message} Before={before.Describe()}, after={after.Describe()}");
                return false;
            }

            // Native effect helpers do not make a purchase. Charge once, only
            // after observing a running/increased native boost; no global price patch.
            money.MoneyTransition(-price, MoneyManager.TransitionType.STAFF, true);
            Plugin.Logger.LogInfo($"[BoostFix v2] {key}: {before.Describe()} -> {after.Describe()}, cost={price:0.##}");
            return true;
        }
        catch (Exception exception)
        {
            message = "Boost failed; see the plugin log.";
            Plugin.Logger.LogError($"[BoostFix v2] Failed to boost {key}: {exception}");
            return false;
        }
    }
}
