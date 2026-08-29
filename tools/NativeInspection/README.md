# Offline native inspection

Read-only analysis of an installed IL2CPP game using the LibCpp2IL and Iced libraries already included in BepInEx. This tool reads files and prints annotated x64 instructions; it does not execute GameAssembly.dll, attach to the game, or modify saves/binaries. Method names are metadata names (nested types use `+`). Set `BEPIN_EX_PATH` to the installed BepInEx directory before building.

```powershell
dotnet run --project tools/NativeInspection -- 'C:\Program Files (x86)\Steam\steamapps\common\Supermarket Simulator' 6000.3.6f1
```

Optional method names after the Unity version override the default hire/spawn inspection:

```powershell
dotnet run --project tools/NativeInspection -- 'C:\Program Files (x86)\Steam\steamapps\common\Supermarket Simulator' 6000.3.6f1 EmployeeManager.HandleCorruptEmployeeData EmployeeManager.LoadData
```

## Restocker findings, 2026-08-29

Observed against game `v1.6.0(223)`, Unity `6000.3.6f1`, metadata version 39. RVAs and behavior are version-specific, not stable APIs. Generic shared-code call annotations can name multiple methods; they are hints, not a full decompilation.

- `EmployeeManager.HireRestocker`, RVA `0x6C9AE0`: checks funds, debits the hiring cost, appends the saved ID, calls `SpawnRestocker`. No Photon call or six-worker check in this method.
- `EmployeeManager.SpawnRestocker`, RVA `0x6CB7D0`: at RVA `0x6CB829`, `cmp ebx,6` followed by `jg` to return. This is a literal ID limit, not a read of `MAX_RESTOCKER_COUNT`.
- Its offline branch resolves `IDManager.Instance.RestockerSO(id)` and spawn position `[id-1]`, then calls `EmployeeGenerator.Instance.SpawnRestocker(prefab, position, rotation)`. It assigns `Clerk.EmployeeId`, appends to `m_ActiveRestockers`, and invokes `onRestockerHired` in that order.
- `EmployeeGenerator.SpawnRestocker`, RVA `0xAEC910`: pools a modern `Clerk` using `LeanPool.Spawn<Clerk>`, parented to the generator. No ID limit here.
- `EmployeeManager.HandleCorruptEmployeeData`, RVA `0x6C8360`: the restocker section compares saved IDs to literal 6 at RVA `0x6C87BD`, then removes larger IDs and their management records. Merely replacing spawning is insufficient for reloads.
- `EmployeeManager.LoadData`, RVA `0x6CA030`: assigns saved-list references, runs corruption cleanup, removes management records for unhired IDs, and enumerates the saved restocker IDs through `SpawnRestocker` (call at RVA `0x6CA3FF`).
- `EmployeeManager.FireRestocker`, RVA `0x6C74E0`, and `DespawnRestocker`, RVA `0x6C6CB0`: lookup/removal by ID, with no corresponding six-ID guard.
- `Clerk.LoadRestockerManagementData`, RVA `0x9D1C20`: loads the matching management record or initializes/adds a default record. A blanket hire postfix that replaces management settings is unnecessary.

The user's successful single-player trace (HireTrace 32–39, frame 13296) independently confirms the order `HireRestocker -> SpawnRestocker -> EmployeeGenerator.SpawnRestocker`, with saved ID insertion before spawn and synchronous active-Clerk registration before return. No NetworkEmployeeManager was present. This disproves the earlier network-required hypothesis.

## Patch and verification boundaries

The plugin now leaves original-ID spawning native, replaces only spawning for IDs above six, and calls native `HireRestocker` for payment/saved-ID handling. Unexpected failed purchases are refunded only when the newly added saved ID exists but no active Clerk does.

During corruption cleanup, the manager temporarily receives a list containing only original IDs. SaveManager's actual saved list and management records remain untouched; a Harmony finalizer restores the original manager reference even if cleanup throws. Other worker-type cleanup still runs. This also prevents extra-ID deletion if the modded save is inspected in multiplayer, but **extra-worker spawning/hiring is single-player-only**.

`TraceHiring` now only controls logging, not hiring. Startup marker: `[HireFix v2]`.

Build and isolated logic checks:

```powershell
dotnet build UnityPlugins.slnx --no-restore -p:IsPackable=false
dotnet run --project tools/RestockerPatchTests
```

The tests link the production `Patches.cs` against game doubles. They check native-ID pass-through, extra-ID registration/event order, no duplicates, capacity extension, original setup preservation, failure paths, multiplayer rejection, and saved-list protection/restoration. They do **not** verify IL2CPP detours, native pool lifecycle, AI/navigation, or real persistence.

Still required in game, using a backed-up save:

1. Replace the plugin DLL, restart, and confirm `[HireFix v2]` in the log.
2. With IDs 1–6 hired, hire once: ID 7 should appear, debit the configured cost exactly once, and log `Spawned extra restocker ID 7 as Clerk`.
3. Confirm the worker actually restocks, its board shows ID 7, and it appears in the plugin menu.
4. Fire/re-hire ID 7, then hire ID 8. Verify IDs and charges.
5. Save/reload with extra workers. Check IDs, wages, and management settings survive. Keep this plugin installed while using that save: the unpatched game will prune IDs above six.
