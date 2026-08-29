# Worker boost checks and native findings

Run from the solution root:

```powershell
dotnet run --project tools/WorkerBoostTests
```

The test project links the real `EmployeeBoostManager/WorkerBoost.cs` against game doubles. It checks live status interpretation, purchase preconditions, charging only after a successful effect, same-level top-ups, no-op/failure cases, and multiplayer rejection. It does not run IL2CPP, Unity coroutines, native worker AI, or the menu. The fake action supplies the expected outcome; only an in-game test verifies the real action produces it.

## Native findings

Inspected offline with `tools/NativeInspection` against game v1.6.0(223), Unity 6000.3.6f1. Addresses are specific to that build.

- `SupermarketSimulator.Clerk.NetworkClerk.Boost_Broadcast` (RVA `0x9D60C0`) sends `BoostClerk_RPC` with RpcTarget value 1. The installed Photon enum maps 1 to **Others**, not All. Calling this instead of the local action omits the local boost.
- `Clerk.InstantInteract` (RVA `0x9D1930`) activates the boost indicator GameObject, calls `AddBoost(m_BoostAmount)`, shows interaction UI, and handles payment. In multiplayer it additionally broadcasts to other players.
- `Clerk.BoostRestockerNetwork` (RVA `0x9D0FC0`) is a reusable **local effect helper**: activate the indicator, then `AddBoost`. Despite its name, it does not broadcast or charge money. The custom menu calls this helper, not the full interaction (which would use the normal price and interaction UI).
- Equivalent no-charge helpers inspected for the other supported workers: `Cashier.BoostCashier_Order`, `CustomerHelper.BoostHelper_Order`, `IceCreamHelper.BoostHelper_Order`, `Baker.BoostBakerNetwork`, and `Janitor.BoostJanitorNetwork`.
- `BoostIndicator.AddBoost` (RVA `0x7F1AE0`) updates three image fills, assigns the level, calculates time, and starts a coroutine. Calling it on an inactive GameObject can update the visual fields without starting the coroutine.
- `BoostIndicator.<StartBoostCountdown>d__19.MoveNext` (RVA `0x80A450`) invokes `onBoostLevelChanged` when entering a level (unless a multiplayer slave). It updates the live image fill and `m_TimeLeft` each frame, then steps down through the levels and resets at expiry.
- `Clerk.Start` subscribes `SetRestockerBoost` to that event. `SetRestockerBoost` (RVA `0x9D2910`) applies the configured NavMeshAgent walking speed and product-placing speed for the level. The plugin does not replace these speed tables or the countdown.
- `CurrentBoostDurations` is populated by `SetTimeLeft` when a boost is added/initialized; the countdown uses a local remaining-time variable. `GetBoostAmount` reads a cached total. Neither is a continuously updated meter.
- For live display, read `Images[0..2].fillAmount`, `m_CurrentBoostLevel`, and `TimeLeft`. The menu also checks that the indicator is active, the native countdown exists, and a worker effect listener is present before displaying an active boost. The displayed amount out of 3 is meter fill, not a movement-speed multiplier.

## Purchase behavior

Each selected, eligible worker gets one normal native boost increment for `Settings.BoostPricePerWorker`. Full/nearly-full meters (within 0.01 total fill of 3) are skipped. The normal game interaction price is untouched.

The purchase validates the price, funds, indicator arrays, and effect listener, calls the native effect helper, and checks that a boost is running and has increased/activated. Only then does it debit the configured cost once. Failed effects/no-op calls are not charged. This validates native boost state; it does not independently measure every worker's speed in game.

Custom-price purchases are restricted to single-player. Multiplayer would require separate authoritative payment and effect synchronization; an Others-only RPC is not a substitute.

## In-game verification

1. Replace `EmployeeBoostManager.dll`, restart, and confirm `[BoostFix v2]` in the log. Use a backed-up save.
2. Open Shift+B, select an unboosted restocker (including an extra ID), and boost once. Confirm the configured price is deducted once, the native boost display activates, and the worker actually works/moves faster.
3. Compare the custom menu's three segments and remaining time with the game's native meter while it drains. Top up at the same level and verify the meter increases and one charge occurs.
4. Test a group and the other worker types. Full meters should be skipped; insufficient funds should apply no boosts or charges.
5. Boost a worker through the original game interaction. The custom menu should pick up that native change without a plugin purchase. Let it expire and verify the menu returns to Ready.

The menu refreshes existing row controls every 0.25 seconds. It does not rebuild the list when boost levels or eligibility change, preserving scroll position and pointer targets during countdowns.

## Worker group tabs (v1.2.0)

Each group has a persistent tab, including an empty-state message when it has no workers. The default tab is Restockers. Select All and Clear All operate only on the current tab, and individual rows can be selected even if their boost meter is full. The purchase button and total include only eligible selected workers in the visible tab; hidden-tab selections never cause a charge.

Selections survive boosting, meter changes, switching tabs, and closing/reopening the menu. They are session-local, not saved across game restarts. Workers removed from the complete live roster are removed from the selection. The tests link the production `WorkerTabSelection.cs` and exercise independent IDs, bulk/individual selection, tab-scoped purchase filtering, empty tabs, and selection retention/pruning.

In game, select restockers, switch to cashiers and select one, then boost from that tab. Confirm only the cashier is charged/boosted, and that returning to Restockers keeps their selection. Also check Select All/Clear All on a full-meter group and that the six tabs and list scrolling remain usable.
