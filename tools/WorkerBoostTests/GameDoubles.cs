// Tests control the result of a native action. These types do not emulate Unity,
// native countdowns, worker AI, or IL2CPP; those require an in-game test.
namespace Photon.Pun
{
    public static class PhotonNetwork { public static bool IsConnected; }
}
public class Image { public float fillAmount; }
public class BoostIndicator
{
    public bool enabled = true;
    public bool isActiveAndEnabled;
    public Image[] Images = { new(), new(), new() };
    public float[] BoostDurations = { 30, 60, 90 };
    public float[] CurrentBoostDurations = { 30, 60, 90 };
    public float TimeLeft;
    public int m_CurrentBoostLevel;
    public object? m_CountdownCoroutine;
    public Action<int>? onBoostLevelChanged = _ => { };
}
public class MoneyManager
{
    public enum TransitionType { STAFF }
    public static MoneyManager Instance = new();
    public float Balance = 500;
    public int Charges;
    public float ChargedAmount;
    public bool HasMoney(float amount) => Balance >= amount;
    public void MoneyTransition(float amount, TransitionType type, bool show)
    {
        Balance += amount;
        Charges++;
        ChargedAmount -= amount;
    }
}
namespace EmployeeBoostManager
{
    public static class Plugin { public static Log Logger = new(); }
    public class Log
    {
        public void LogInfo(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message) { }
    }
}
