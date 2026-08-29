namespace EmployeeBoostManager;

internal enum WorkerGroup
{
    Cashiers,
    Restockers,
    CustomerHelpers,
    Janitors,
    Bakers,
    IceCreamHelpers
}

// Selection is independent of boost eligibility and belongs to a (group, ID)
// pair. Switching tabs or filling a meter never discards the user's choices.
internal sealed class WorkerTabSelection
{
    private readonly HashSet<(WorkerGroup Group, int Id)> selected = new();
    public WorkerGroup ActiveGroup { get; private set; } = WorkerGroup.Restockers;

    public void SwitchTo(WorkerGroup group) => ActiveGroup = group;

    public bool IsSelected(WorkerGroup group, int id) => selected.Contains((group, id));

    public bool IsSelectedInCurrentTab(WorkerGroup group, int id) =>
        group == ActiveGroup && IsSelected(group, id);

    public void Toggle(WorkerGroup group, int id)
    {
        if (!selected.Add((group, id)))
            selected.Remove((group, id));
    }

    public void SelectAll(IEnumerable<int> currentTabIds)
    {
        foreach (var id in currentTabIds)
            selected.Add((ActiveGroup, id));
    }

    public void ClearCurrentTab() => selected.RemoveWhere(worker => worker.Group == ActiveGroup);

    public void RetainWorkers(IEnumerable<(WorkerGroup Group, int Id)> liveWorkers)
    {
        // Always pass the complete roster, not just the visible tab: otherwise
        // a refresh would forget selections in hidden groups.
        var live = liveWorkers.ToHashSet();
        selected.RemoveWhere(worker => !live.Contains(worker));
    }
}
