// Minimal doubles for testing patch branching and state transitions, not game behavior.
namespace HarmonyLib
{
    public sealed class HarmonyPrefix : Attribute { }
    public sealed class HarmonyFinalizer : Attribute { }
    public sealed class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method) { } }
}
namespace Photon.Pun { public static class PhotonNetwork { public static bool IsConnected; } }
namespace Il2CppSystem.Collections.Generic
{
    public class List<T> : System.Collections.Generic.List<T> { }
}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
    public class Il2CppReferenceArray<T>
    {
        private readonly T[] items;
        public Il2CppReferenceArray(int count) => items = new T[count];
        public int Count => items.Length;
        public T this[int index] { get => items[index]; set => items[index] = value; }
    }
}
namespace UnityEngine
{
    public class Object
    {
        public static RestockerSO Instantiate(RestockerSO template) => new()
        { ID = template.ID, DailyWage = template.DailyWage, RestockerPrefab = template.RestockerPrefab };
    }
    public class GameObject
    {
        public GameObject(string name) => this.name = name;
        public string name;
        public Transform transform = new();
    }
    public class Transform
    {
        public Transform? parent;
        public Vector3 position;
        public Quaternion rotation;
        public void SetParent(Transform? value, bool worldPositionStays) => parent = value;
    }
    public struct Vector3
    {
        public float x, y, z;
        public static Vector3 right => new() { x = 1 };
        public static Vector3 operator +(Vector3 a, Vector3 b) => new() { x = a.x + b.x, y = a.y + b.y, z = a.z + b.z };
    }
    public struct Quaternion { }
    public static class Random { public static int Range(int min, int max) => min; }
}
namespace Utilities
{
    public static class Extensions
    {
        public static void Il2CppCopyTo<T>(this Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<T> source,
            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<T> destination)
        {
            for (var i = 0; i < source.Count; i++) destination[i] = source[i];
        }
    }
}
public class Clerk
{
    public int EmployeeId;
    public UnityEngine.GameObject gameObject = new("Clerk");
}
public class RestockerSO
{
    public int ID;
    public float DailyWage;
    public Clerk RestockerPrefab = new();
}
public class IDManager
{
    public static IDManager Instance = new();
    public List<RestockerSO> m_Restockers = new();
    public RestockerSO RestockerSO(int id) => m_Restockers.FirstOrDefault(s => s.ID == id)!;
}
public class EmployeeManager
{
    public Il2CppSystem.Collections.Generic.List<int> m_RestockersData = new();
    public List<Clerk> m_ActiveRestockers = new();
    public Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<UnityEngine.Transform> m_RestockerSpawnPositions = new(6);
    public Action? onRestockerHired;
    public Clerk? GetRestockerByID(int id) => m_ActiveRestockers.FirstOrDefault(c => c.EmployeeId == id);
    public void SpawnRestocker(int id) { }
    public void HandleCorruptEmployeeData() { }
}
public class EmployeeGenerator
{
    public static EmployeeGenerator Instance = new();
    public int Calls;
    public bool ReturnNull;
    public Clerk? SpawnRestocker(Clerk prefab, UnityEngine.Vector3 position, UnityEngine.Quaternion rotation)
    {
        Calls++;
        return ReturnNull ? null : new Clerk { gameObject = new(prefab.gameObject.name) };
    }
}
namespace UnlimitedRestockers
{
    public static class Plugin
    {
        public static Log Logger = new();
        public static Config Configuration = new();
    }
    public class Config { public Entry DailyWage = new(); }
    public class Entry { public float Value = 150; }
    public class Log
    {
        public void LogInfo(string message) { }
        public void LogError(string message) { }
    }
}
