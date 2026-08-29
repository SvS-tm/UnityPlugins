using UnlimitedRestockers;
using UnityEngine;
using NativeIds = Il2CppSystem.Collections.Generic.List<int>;

// Exercises the actual production patch source with isolated game doubles.
// Does NOT exercise Harmony detours, IL2CPP, pooling, animation, or in-game AI.
var manager = new EmployeeManager();
IDManager.Instance = new IDManager();
EmployeeGenerator.Instance = new EmployeeGenerator();
for (var id = 1; id <= 6; id++)
{
    IDManager.Instance.m_Restockers.Add(new RestockerSO
    {
        ID = id, DailyWage = 75,
        RestockerPrefab = new Clerk { gameObject = new GameObject($"Restocker_{id}") }
    });
    manager.m_RestockerSpawnPositions[id - 1] = new GameObject($"Position_{id}").transform;
}

Check(Patches.SpawnExtraRestocker(manager, 1), "ID 1 uses native spawn");
Check(Patches.SpawnExtraRestocker(manager, 6), "ID 6 uses native spawn");
Check(EmployeeGenerator.Instance.Calls == 0, "original IDs never call replacement generator");

var notifications = 0;
manager.onRestockerHired = () =>
{
    notifications++;
    Check(manager.GetRestockerByID(7) != null, "ID assigned and registered before event");
};
Check(!Patches.SpawnExtraRestocker(manager, 7), "ID 7 bypasses native guard");
Check(manager.m_ActiveRestockers.Count == 1 && notifications == 1, "one worker and one event");
Check(manager.m_RestockersData.Count == 0, "spawn never changes saved IDs/payment");
Check(manager.m_RestockerSpawnPositions.Count == 7, "extra spawn slot prepared");
Check(IDManager.Instance.RestockerSO(7)!.DailyWage == 150, "configured wage on extra setup");
Check(IDManager.Instance.RestockerSO(6)!.DailyWage == 75, "original setup unchanged");
Patches.SpawnExtraRestocker(manager, 7);
Check(EmployeeGenerator.Instance.Calls == 1 && notifications == 1, "duplicate spawn is idempotent");
Patches.EnsureRestockerCapacity(manager, 9);
Check(IDManager.Instance.m_Restockers.Count == 9 && manager.m_RestockerSpawnPositions.Count == 9,
    "gaps expanded through requested ID");

Photon.Pun.PhotonNetwork.IsConnected = true;
Check(!Patches.SpawnExtraRestocker(manager, 8) && EmployeeGenerator.Instance.Calls == 1,
    "no unsynchronized multiplayer spawn");
Photon.Pun.PhotonNetwork.IsConnected = false;
EmployeeGenerator.Instance.ReturnNull = true;
Patches.SpawnExtraRestocker(manager, 8);
Check(manager.GetRestockerByID(8) == null && notifications == 1, "failed generation does not register/notify");

var vanillaIds = new NativeIds { 2, 1, 6 };
manager.m_RestockersData = vanillaIds;
Patches.ProtectExtraSavedRestockers(manager, out var noState);
Check(noState == null && ReferenceEquals(manager.m_RestockersData, vanillaIds), "vanilla save untouched");

var savedIds = new NativeIds { 2, 7, 1, 9, 6 };
manager.m_RestockersData = savedIds;
Patches.ProtectExtraSavedRestockers(manager, out var state);
try
{
    Check(manager.m_RestockersData.SequenceEqual(new[] { 2, 1, 6 }), "cleanup sees vanilla IDs only");
    Check(savedIds.SequenceEqual(new[] { 2, 7, 1, 9, 6 }), "actual saved list unchanged during cleanup");
    throw new InvalidOperationException("Simulated native cleanup failure");
}
catch (InvalidOperationException) { }
finally { Patches.RestoreExtraSavedRestockers(manager, state); }
Check(ReferenceEquals(manager.m_RestockersData, savedIds), "finalizer restores exact list reference");
Check(manager.m_RestockersData.SequenceEqual(new[] { 2, 7, 1, 9, 6 }), "extra IDs and ordering preserved");

var emptyManager = new EmployeeManager { m_RestockerSpawnPositions = new(0) };
var rejectedEmptyPositions = false;
try { Patches.EnsureRestockerCapacity(emptyManager, 7); }
catch (InvalidOperationException) { rejectedEmptyPositions = true; }
Check(rejectedEmptyPositions, "empty spawn array rejected before indexing -1");
Console.WriteLine("All patch behavior checks passed (game doubles; in-game testing still required).");

static void Check(bool condition, string description)
{
    if (!condition) throw new Exception("FAIL: " + description);
    Console.WriteLine("PASS: " + description);
}
