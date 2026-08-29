using EmployeeBoostManager;

var checks = 0;
var idle = new BoostIndicator();
Check(BoostState.Read(idle).Describe().StartsWith("Ready"), "cached durations do not falsely mark an idle worker boosted");
Check(BoostState.Read(idle).CanBoost, "inactive native indicator can be activated by a purchase");

var live = new BoostIndicator();
Activate(live, 1f, 0.4f, 0f, 2, 54f);
var state = BoostState.Read(live);
Check(state.Running && state.Level == 2 && Near(state.Amount, 1.4f), "read actual native level and three bar fills");
Check(state.SecondsLeft == 54f, "read live TimeLeft, not max cached duration (90)");
live.Images[1].fillAmount = 0.2f;
live.TimeLeft = 42f;
state = BoostState.Read(live);
Check(Near(state.Amount, 1.2f) && state.SecondsLeft == 42f, "status follows native countdown changes");
live.isActiveAndEnabled = false;
Check(!BoostState.Read(live).Running && !BoostState.Read(live).Describe().Contains("BOOSTED"),
    "filled but inactive indicator is not reported as boosted");
live.isActiveAndEnabled = true;
live.m_CountdownCoroutine = null;
Check(!BoostState.Read(live).Running, "active visual without countdown is not reported as running");

MoneyManager.Instance = new();
var indicator = new BoostIndicator();
var appliedLevel = 0;
indicator.onBoostLevelChanged = level => appliedLevel = level;
var success = WorkerBoost.TryPurchase("Restockers:7", indicator, () =>
{
    Check(MoneyManager.Instance.Charges == 0, "native effect runs before custom charge");
    Activate(indicator, 0.5f, 0f, 0f, 1, 15f);
    indicator.onBoostLevelChanged?.Invoke(1);
}, 25f, out _);
Check(success && appliedLevel == 1, "successful native effect is accepted");
Check(MoneyManager.Instance.Charges == 1 && MoneyManager.Instance.ChargedAmount == 25f,
    "exactly one configured price charged");

var charges = MoneyManager.Instance.Charges;
Check(!WorkerBoost.TryPurchase("Restockers:7", indicator, () => { }, 25f, out _), "unchanged running boost is rejected");
Check(MoneyManager.Instance.Charges == charges, "no charge for a silent no-op");
Check(WorkerBoost.TryPurchase("Restockers:7", indicator, () => Activate(indicator, 1f, 0f, 0f, 1, 30f), 25f, out _),
    "topping up an existing level is accepted without requiring level change");

ExpectNoPurchase(new(), _ => { }, 25f, "native no-op on idle worker");
ExpectNoPurchase(new(), target =>
{
    target.Images[0].fillAmount = 0.5f;
    target.TimeLeft = 15f;
    target.m_CurrentBoostLevel = 1;
}, 25f, "visual-only boost without component/countdown activation");
ExpectNoPurchase(new(), _ => throw new InvalidOperationException("native failure"), 25f, "throwing native action");

var full = new BoostIndicator();
Activate(full, 1f, 1f, 1f, 3, 180f);
Check(BoostState.Read(full).Full && !BoostState.Read(full).CanBoost, "full native meter is ineligible");
var actionCalled = false;
ExpectNoPurchase(full, _ => actionCalled = true, 25f, "full meter purchase");
Check(!actionCalled, "full meter does not call native boost");

MoneyManager.Instance.Balance = 0;
ExpectNoPurchase(new(), _ => actionCalled = true, 25f, "insufficient funds");
Check(!actionCalled, "insufficient funds checked before applying effect");
MoneyManager.Instance.Balance = 500;
foreach (var invalidPrice in new[] { -1f, float.NaN, float.PositiveInfinity })
    ExpectNoPurchase(new(), _ => actionCalled = true, invalidPrice, "invalid price " + invalidPrice);
Check(!actionCalled, "invalid prices do not apply a boost");

Photon.Pun.PhotonNetwork.IsConnected = true;
ExpectNoPurchase(new(), _ => actionCalled = true, 25f, "multiplayer purchase");
Check(!actionCalled, "multiplayer never applies a local-only effect");
Photon.Pun.PhotonNetwork.IsConnected = false;

var uninitialized = new BoostIndicator { onBoostLevelChanged = null };
Check(!BoostState.Read(uninitialized).CanBoost, "missing worker effect callback blocks purchase");
ExpectNoPurchase(uninitialized, _ => actionCalled = true, 25f, "uninitialized worker");
Check(!BoostState.Read(new BoostIndicator { Images = Array.Empty<Image>() }).Available,
    "incomplete indicator is rejected before native array access");
Check(!BoostState.Read(new BoostIndicator { BoostDurations = new[] { 0f, 1f, 1f } }).Available,
    "invalid countdown duration rejected");

charges = MoneyManager.Instance.Charges;
Check(WorkerBoost.TryPurchase("Restockers:7", new(), () => { }, 0f, out _) == false,
    "zero-cost setting does not mask failed boost");
var free = new BoostIndicator();
Check(WorkerBoost.TryPurchase("Restockers:7", free, () => Activate(free, 0.5f, 0f, 0f, 1, 15f), 0f, out _),
    "zero-cost successful boost supported");
var tabs = new WorkerTabSelection();
Check(tabs.ActiveGroup == WorkerGroup.Restockers, "menu opens on the restockers tab by default");
var roster = new[]
{
    (Group: WorkerGroup.Restockers, Id: 1, CanBoost: true),
    (Group: WorkerGroup.Restockers, Id: 2, CanBoost: false),
    (Group: WorkerGroup.Cashiers, Id: 1, CanBoost: true)
};
tabs.SelectAll(roster.Where(worker => worker.Group == tabs.ActiveGroup).Select(worker => worker.Id));
Check(tabs.IsSelected(WorkerGroup.Restockers, 1) && tabs.IsSelected(WorkerGroup.Restockers, 2),
    "Select All remembers both ready and full-meter workers");
Check(!tabs.IsSelected(WorkerGroup.Cashiers, 1), "identical worker IDs in different groups stay independent");
tabs.SwitchTo(WorkerGroup.Cashiers);
Check(tabs.IsSelected(WorkerGroup.Restockers, 1), "switching tabs retains hidden selections");
Check(!tabs.IsSelectedInCurrentTab(WorkerGroup.Restockers, 1), "hidden selections are not purchase targets");
tabs.Toggle(WorkerGroup.Cashiers, 1);
var targets = roster.Where(worker => worker.CanBoost && tabs.IsSelectedInCurrentTab(worker.Group, worker.Id)).ToArray();
Check(targets.Length == 1 && targets[0].Group == WorkerGroup.Cashiers,
    "purchase filtering includes only eligible selected workers in the current tab");
tabs.RetainWorkers(roster.Select(worker => (worker.Group, worker.Id)));
Check(tabs.IsSelected(WorkerGroup.Restockers, 2) && tabs.IsSelected(WorkerGroup.Cashiers, 1),
    "refreshing the full roster retains selections across tabs");
tabs.ClearCurrentTab();
Check(!tabs.IsSelected(WorkerGroup.Cashiers, 1) && tabs.IsSelected(WorkerGroup.Restockers, 1),
    "Clear All affects only the current tab");
tabs.SwitchTo(WorkerGroup.Restockers);
Check(tabs.IsSelectedInCurrentTab(WorkerGroup.Restockers, 2), "returning to a tab restores full-meter selection");
targets = roster.Where(worker => worker.CanBoost && tabs.IsSelectedInCurrentTab(worker.Group, worker.Id)).ToArray();
Check(targets.Length == 1 && targets[0].Id == 1, "full selected meters remain selected but are excluded from price/boost targets");
tabs.Toggle(WorkerGroup.Restockers, 2);
Check(!tabs.IsSelected(WorkerGroup.Restockers, 2) && tabs.IsSelected(WorkerGroup.Restockers, 1),
    "individual deselection does not clear another worker");
tabs.SelectAll(new[] { 1, 2 });
Check(tabs.IsSelected(WorkerGroup.Restockers, 1) && tabs.IsSelected(WorkerGroup.Restockers, 2),
    "Select All adds missing selections without toggling existing ones off");
tabs.RetainWorkers(new[] { (WorkerGroup.Restockers, 2), (WorkerGroup.Cashiers, 1) });
Check(!tabs.IsSelected(WorkerGroup.Restockers, 1) && tabs.IsSelected(WorkerGroup.Restockers, 2),
    "fired workers are pruned without losing remaining selections");
tabs.SwitchTo(WorkerGroup.Bakers);
tabs.SelectAll(Array.Empty<int>());
tabs.ClearCurrentTab();
Check(tabs.IsSelected(WorkerGroup.Restockers, 2), "empty tab selection controls do not affect other tabs");
Console.WriteLine($"All {checks} boost/tab checks passed (game doubles; not an in-game UI test).");

static void Activate(BoostIndicator target, float first, float second, float third, int level, float seconds)
{
    target.isActiveAndEnabled = true;
    target.m_CountdownCoroutine = new object();
    target.Images[0].fillAmount = first;
    target.Images[1].fillAmount = second;
    target.Images[2].fillAmount = third;
    target.m_CurrentBoostLevel = level;
    target.TimeLeft = seconds;
}

void ExpectNoPurchase(BoostIndicator target, Action<BoostIndicator> action, float price, string description)
{
    var previousCharges = MoneyManager.Instance.Charges;
    var previousBalance = MoneyManager.Instance.Balance;
    var result = WorkerBoost.TryPurchase("Restockers:7", target, () => action(target), price, out _);
    Check(!result && MoneyManager.Instance.Charges == previousCharges && MoneyManager.Instance.Balance == previousBalance,
        description + " rejected without a charge");
}

void Check(bool condition, string description)
{
    if (!condition) throw new Exception("FAIL: " + description);
    checks++;
    Console.WriteLine("PASS: " + description);
}

static bool Near(float a, float b) => Math.Abs(a - b) < 0.001f;
