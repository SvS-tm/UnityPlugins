using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Utilities;
using Clerk = SupermarketSimulator.Clerk.Clerk;
using Janitor = __Project__.Scripts.Janitor.Janitor;

namespace EmployeeBoostManager;

public class EmployeeBoostMenu(IntPtr ptr) : MonoBehaviour(ptr)
{
    private const float MenuToggleCooldown = 0.25f;
    private const float RefreshInterval = 0.25f;

    private readonly List<IDisposable> resources = new(1);
    private readonly WorkerTabSelection selection = new();
    private readonly List<WorkerEntry> workers = new();
    private readonly Dictionary<string, WorkerRow> workerRows = new();
    private readonly Dictionary<WorkerGroup, Button> tabButtons = new();

    private InputAction menuAction = default!;
    private CameraRotationLock? cameraRotationLock;
    private GameObject menuRoot = default!;
    private RectTransform workerList = default!;
    private ScrollRect workerScroll = default!;
    private Text currentGroupText = default!;
    private Button selectAllButton = default!;
    private Button clearAllButton = default!;
    private Text titleText = default!;
    private Text balanceText = default!;
    private Text selectionText = default!;
    private Text feedbackText = default!;
    private Button boostButton = default!;
    private Text boostButtonText = default!;

    private bool isOpen;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private float nextRefreshTime;
    private string feedback = string.Empty;
    private string renderedWorkersSignature = string.Empty;

    public void Awake()
    {
        BuildUi();

        menuAction = InputHelpers.ParseAction
        (
            "employeeBoostMenuAction",
            Plugin.Configuration.MenuBinding.Value
        );

        resources.Add(menuAction.WithCooldownCallback(Toggle, () => MenuToggleCooldown));
        Plugin.Configuration.MenuBinding.SettingChanged += OnMenuBindingChanged;
    }

    public void OnDestroy()
    {
        Plugin.Configuration.MenuBinding.SettingChanged -= OnMenuBindingChanged;

        foreach (var resource in resources)
            resource.Dispose();

        if (isOpen)
            RestoreCursor();

        cameraRotationLock?.Dispose();
        cameraRotationLock = null;

        if (menuRoot != null)
            Destroy(menuRoot);
    }

    public void Update()
    {
        if (!isOpen)
            return;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        cameraRotationLock?.Maintain();

        if (Time.unscaledTime >= nextRefreshTime)
            RefreshWorkerState();
    }

    [HideFromIl2Cpp]
    private void OnMenuBindingChanged(object? sender, EventArgs eventArgs)
    {
        menuAction.Rebind(Plugin.Configuration.MenuBinding.Value);
    }

    private void Toggle()
    {
        if (isOpen)
            Close();
        else
            Open();
    }

    private void Open()
    {
        EnsureUi();
        workerScroll.scrollSensitivity = Plugin.Configuration.MouseWheelSensitivity.Value;

        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;

        cameraRotationLock = CameraRotationLock.Acquire();
        isOpen = true;
        feedback = string.Empty;
        menuRoot.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        RebuildWorkerList();
    }

    private void Close()
    {
        isOpen = false;

        if (menuRoot != null)
            menuRoot.SetActive(false);

        cameraRotationLock?.Dispose();
        cameraRotationLock = null;
        RestoreCursor();
    }

    private void RestoreCursor()
    {
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
    }

    [HideFromIl2Cpp]
    private void ToggleWorker(WorkerGroup group, int id)
    {
        selection.Toggle(group, id);
        feedback = string.Empty;
        RefreshWorkerState();
    }

    [HideFromIl2Cpp]
    private void SwitchTab(WorkerGroup group)
    {
        if (selection.ActiveGroup == group)
            return;

        selection.SwitchTo(group);
        feedback = string.Empty;
        RebuildWorkerList();
        workerScroll.StopMovement();
        workerList.anchoredPosition = Vector2.zero;
    }

    private void SelectAllWorkers()
    {
        CollectWorkers();
        selection.SelectAll(workers.Where(worker => worker.Group == selection.ActiveGroup).Select(worker => worker.Id));
        feedback = string.Empty;
        RefreshWorkerState();
    }

    private void ClearTabSelection()
    {
        selection.ClearCurrentTab();
        feedback = string.Empty;
        RefreshWorkerState();
    }

    private void BoostSelectedWorkers()
    {
        if (PhotonNetwork.IsConnected)
        {
            feedback = "Custom-price boosts currently support single-player only.";
            UpdateSummary();
            return;
        }

        CollectWorkers();
        var selected = workers.Where(worker => worker.CanBoost && selection.IsSelectedInCurrentTab(worker.Group, worker.Id)).ToArray();
        if (selected.Length == 0)
        {
            feedback = "Select a worker in this tab whose boost meter is not full.";
            RebuildWorkerList();
            return;
        }

        var price = GetPricePerWorker();
        var totalPrice = price * selected.Length;
        if (!float.IsFinite(price) || price < 0 || !float.IsFinite(totalPrice))
        {
            feedback = "BoostPricePerWorker must be finite and non-negative.";
            UpdateSummary();
            return;
        }

        var money = MoneyManager.Instance;
        if (money == null || !money.HasMoney(totalPrice))
        {
            feedback = money == null ? "Money manager is not available." : $"Not enough money. Required: ${totalPrice:0.##}";
            UpdateSummary();
            return;
        }

        var boostedCount = 0;
        var lastFailure = string.Empty;
        foreach (var worker in selected)
        {
            if (WorkerBoost.TryPurchase(worker.Key, worker.Indicator, worker.Boost!, price, out var message))
                boostedCount++;
            else
                lastFailure = message;
        }

        // Keep the user's selection for repeated boosts. Eligibility (e.g. a
        // full meter) only controls purchasing, not the remembered selection.
        var failedCount = selected.Length - boostedCount;
        feedback = failedCount == 0
            ? $"Boosted {boostedCount} worker(s) for ${price * boostedCount:0.##}."
            : $"Boosted {boostedCount}; {failedCount} failed. {lastFailure}";
        RefreshWorkerState();
    }

    private void RebuildWorkerList()
    {
        EnsureUi();

        nextRefreshTime = Time.unscaledTime + RefreshInterval;
        CollectWorkers();

        workerRows.Clear();

        for (var index = workerList.childCount - 1; index >= 0; --index)
        {
            var child = workerList.GetChild(index).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

        var visibleWorkers = workers.Where(worker => worker.Group == selection.ActiveGroup).ToArray();
        foreach (var worker in visibleWorkers)
            AddWorkerButton(worker);

        if (visibleWorkers.Length == 0)
        {
            var emptyRow = CreateListButton("EmptyGroup", $"No active {GetGroupName(selection.ActiveGroup).ToLowerInvariant()}.",
                new Color(0.12f, 0.14f, 0.18f, 0.95f), 62f, FontStyle.Normal, out _);
            emptyRow.interactable = false;
        }

        renderedWorkersSignature = GetWorkersSignature();
        UpdateSummary();
    }

    private void RefreshWorkerState()
    {
        nextRefreshTime = Time.unscaledTime + RefreshInterval;
        CollectWorkers();

        if (GetWorkersSignature() != renderedWorkersSignature)
        {
            RebuildWorkerList();
            return;
        }

        // Refresh existing controls in place, including eligibility and segment
        // widths. Native countdown updates must not recreate pointer targets.
        foreach (var worker in workers)
        {
            if (workerRows.TryGetValue(worker.Key, out var row))
                UpdateWorkerRow(worker, row);
        }

        UpdateSummary();
    }

    private string GetWorkersSignature()
    {
        return $"{selection.ActiveGroup}:" + string.Join("|",
            workers.Where(worker => worker.Group == selection.ActiveGroup).Select(worker => worker.Key));
    }

    private void UpdateSummary()
    {
        var visible = workers.Where(worker => worker.Group == selection.ActiveGroup).ToArray();
        var selectedCount = visible.Count(worker => selection.IsSelected(worker.Group, worker.Id));
        var boostableCount = visible.Count(worker => worker.CanBoost && selection.IsSelected(worker.Group, worker.Id));
        var price = GetPricePerWorker();
        var totalPrice = boostableCount * price;
        var validPrice = float.IsFinite(price) && price >= 0 && float.IsFinite(totalPrice);
        var money = MoneyManager.Instance?.Money ?? 0f;

        titleText.text = $"Employee Boost Manager ({workers.Count})";
        balanceText.text = $"Balance: ${money:0.##}    Price per worker: ${price:0.##}";
        currentGroupText.text = $"{GetGroupName(selection.ActiveGroup)} ({visible.Length})";
        selectAllButton.interactable = visible.Length > selectedCount;
        clearAllButton.interactable = selectedCount > 0;
        selectionText.text = selectedCount == 0
            ? "Select workers individually or use Select All in this tab"
            : $"This tab — Selected: {selectedCount}    Boostable: {boostableCount}    Total: ${totalPrice:0.##}";
        feedbackText.text = PhotonNetwork.IsConnected
            ? "Custom-price boosts currently support single-player only."
            : !validPrice ? "BoostPricePerWorker must be finite and non-negative." : feedback;
        boostButtonText.text = "Boost Selected (This Tab)";
        boostButton.interactable = !PhotonNetwork.IsConnected && boostableCount > 0 &&
            validPrice && money >= totalPrice;

        foreach (var (group, button) in tabButtons)
        {
            button.GetComponentInChildren<Text>().text =
                $"{GetGroupName(group)} ({workers.Count(worker => worker.Group == group)})";
            button.GetComponent<Image>().color = group == selection.ActiveGroup
                ? new Color(0.16f, 0.48f, 0.72f, 1f) : new Color(0.17f, 0.20f, 0.27f, 1f);
        }
    }

    private void CollectWorkers()
    {
        workers.Clear();
        var employeeManager = EmployeeManager.Instance;
        if (employeeManager != null)
        {
            foreach (var cashier in employeeManager.m_ActiveCashiers)
                AddWorker(WorkerGroup.Cashiers, cashier.CashierID, cashier.BoostIndicator, cashier.BoostCashier_Order);

            foreach (Clerk clerk in employeeManager.m_ActiveRestockers)
                AddWorker(WorkerGroup.Restockers, clerk.EmployeeId, clerk.BoostIndicator, clerk.BoostRestockerNetwork);

            foreach (var helper in employeeManager.m_ActiveCustomerHelpers)
                AddWorker(WorkerGroup.CustomerHelpers, helper.CustomerHelperID, helper.BoostIndicator, helper.BoostHelper_Order);

            foreach (Janitor janitor in employeeManager.m_ActiveJanitor)
                AddWorker(WorkerGroup.Janitors, janitor.JanitorID, janitor.BoostIndicator, janitor.BoostJanitorNetwork);

            foreach (var helper in employeeManager.m_ActiveIceCreamHelpers)
                AddWorker(WorkerGroup.IceCreamHelpers, helper.ID, helper.BoostIndicator, helper.BoostHelper_Order);
        }

        var bakeryManager = BakeryManager.Instance;
        if (bakeryManager != null)
        {
            foreach (var baker in bakeryManager.Bakers)
                AddWorker(WorkerGroup.Bakers, baker.BakerID, baker.BoostIndicator, baker.BoostBakerNetwork);
        }

        workers.Sort((left, right) =>
        {
            var groupComparison = left.Group.CompareTo(right.Group);
            return groupComparison != 0 ? groupComparison : left.Id.CompareTo(right.Id);
        });

        selection.RetainWorkers(workers.Select(worker => (worker.Group, worker.Id)));
    }

    [HideFromIl2Cpp]
    private void AddWorker(WorkerGroup group, int id, BoostIndicator? indicator, Action? boost)
    {
        workers.Add(new WorkerEntry(group, id, indicator, boost));
    }

    private void BuildTabs(Transform panel)
    {
        tabButtons.Clear();
        var tabs = CreateUiObject("WorkerTabs", panel, out var tabsRect);
        SetAnchoredRect(tabsRect, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(-48f, 78f));

        // Two rows keep long group names readable without widening the menu.
        var groups = Enum.GetValues<WorkerGroup>();
        for (var index = 0; index < groups.Length; index++)
        {
            var group = groups[index];
            var button = CreateButton($"Tab_{group}", tabs.transform, GetGroupName(group),
                new Color(0.17f, 0.20f, 0.27f, 1f));
            var column = index % 3;
            var row = index / 3;
            SetAnchoredRect(button.GetComponent<RectTransform>(), new Vector2(column / 3f, 1f),
                new Vector2((column + 1) / 3f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -row * 40f), new Vector2(-6f, 36f));
            button.GetComponentInChildren<Text>().fontSize = 16;
            button.onClick.AddListener((UnityAction)(Action)(() => SwitchTab(group)));
            tabButtons[group] = button;
        }

        currentGroupText = CreateText("CurrentGroup", panel, string.Empty, 18);
        currentGroupText.fontStyle = FontStyle.Bold;
        currentGroupText.alignment = TextAnchor.MiddleLeft;
        SetAnchoredRect(currentGroupText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(24f, -180f), new Vector2(390f, 34f));

        selectAllButton = CreateButton("SelectAll", panel, "Select All", new Color(0.16f, 0.38f, 0.58f, 1f));
        SetAnchoredRect(selectAllButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(-180f, -180f), new Vector2(140f, 34f));
        selectAllButton.onClick.AddListener((UnityAction)(Action)SelectAllWorkers);

        clearAllButton = CreateButton("ClearAll", panel, "Clear All", new Color(0.32f, 0.27f, 0.27f, 1f));
        SetAnchoredRect(clearAllButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(-24f, -180f), new Vector2(140f, 34f));
        clearAllButton.onClick.AddListener((UnityAction)(Action)ClearTabSelection);
    }

    [HideFromIl2Cpp]
    private void AddWorkerButton(WorkerEntry worker)
    {
        var button = CreateListButton(worker.Key, string.Empty, Color.white, 62f, FontStyle.Normal, out var text);
        text.fontSize = 16;
        Stretch(text.rectTransform, 12f, 12f, 0f, 19f);
        var fills = new RectTransform[BoostState.SegmentCount];
        var colors = new[]
        {
            new Color(0.22f, 0.72f, 0.42f, 1f),
            new Color(0.24f, 0.60f, 0.95f, 1f),
            new Color(0.98f, 0.67f, 0.20f, 1f)
        };
        for (var index = 0; index < fills.Length; index++)
        {
            var track = CreateUiObject($"BoostSegment_{index + 1}", button.transform, out var trackRect);
            trackRect.anchorMin = new Vector2(index / 3f, 0f);
            trackRect.anchorMax = new Vector2((index + 1) / 3f, 0f);
            trackRect.offsetMin = new Vector2(12f, 8f);
            trackRect.offsetMax = new Vector2(-12f, 16f);
            var background = track.Il2CppAddComponent<Image>();
            background.color = new Color(0.04f, 0.05f, 0.07f, 1f);
            background.raycastTarget = false;

            var fill = CreateUiObject("Fill", track.transform, out fills[index]);
            Stretch(fills[index], 0f, 0f, 0f, 0f);
            var fillImage = fill.Il2CppAddComponent<Image>();
            fillImage.color = colors[index];
            fillImage.raycastTarget = false;
        }

        var row = new WorkerRow(button, text, fills);
        workerRows[worker.Key] = row;
        UpdateWorkerRow(worker, row);
        // Selection is allowed even at a full meter, for a later repeat boost.
        button.onClick.AddListener((UnityAction)(Action)(() => ToggleWorker(worker.Group, worker.Id)));
    }

    [HideFromIl2Cpp]
    private void UpdateWorkerRow(WorkerEntry worker, WorkerRow row)
    {
        var selected = selection.IsSelected(worker.Group, worker.Id);
        row.Button.interactable = true;
        row.Button.GetComponent<Image>().color = selected
            ? new Color(0.18f, 0.55f, 0.9f, 0.95f) : new Color(0.12f, 0.14f, 0.18f, 0.95f);
        row.Text.text = $"{(selected ? "[x]" : "[ ]")} ID {worker.Id}    {worker.State.Describe()}";
        for (var index = 0; index < row.Fills.Length; index++)
            row.Fills[index].anchorMax = new Vector2(worker.State.Segments[index], 1f);
    }

    private Button CreateListButton
    (
        string name,
        string label,
        Color color,
        float height,
        FontStyle fontStyle,
        out Text text
    )
    {
        var row = CreateUiObject(name, workerList, out var rowRect);
        var image = row.Il2CppAddComponent<Image>();
        var button = row.Il2CppAddComponent<Button>();
        var layout = row.Il2CppAddComponent<LayoutElement>();

        image.color = color;
        button.targetGraphic = image;
        layout.preferredHeight = height;
        layout.minHeight = height;
        rowRect.sizeDelta = new Vector2(0f, height);

        text = CreateText("Label", row.transform, label, 18);
        Stretch(text.rectTransform, 12f, 12f, 0f, 0f);
        text.alignment = TextAnchor.MiddleLeft;
        text.fontStyle = fontStyle;
        return button;
    }

    private static float GetPricePerWorker()
    {
        return Plugin.Configuration.BoostPricePerWorker.Value;
    }

    private static string GetGroupName(WorkerGroup group) => group switch
    {
        WorkerGroup.Cashiers => "Cashiers",
        WorkerGroup.Restockers => "Restockers",
        WorkerGroup.CustomerHelpers => "Customer Helpers",
        WorkerGroup.Janitors => "Janitors",
        WorkerGroup.Bakers => "Bakers",
        WorkerGroup.IceCreamHelpers => "Ice Cream Helpers",
        _ => group.ToString()
    };

    private void BuildUi()
    {
        menuRoot = new GameObject("EmployeeBoostManager_Menu");
        DontDestroyOnLoad(menuRoot);

        var canvas = menuRoot.Il2CppAddComponent<Canvas>();
        menuRoot.Il2CppAddComponent<CanvasScaler>();
        menuRoot.Il2CppAddComponent<GraphicRaycaster>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 10010;

        var panel = CreateUiObject("Panel", menuRoot.transform, out var panelRect);
        var panelImage = panel.Il2CppAddComponent<Image>();
        panelImage.color = new Color(0.05f, 0.06f, 0.08f, 0.98f);
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(760f, 720f);
        panelRect.anchoredPosition = Vector2.zero;

        titleText = CreateText("Title", panel.transform, "Employee Boost Manager", 28);
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        SetAnchoredRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-70f, 44f));

        var closeButton = CreateButton("Close", panel.transform, "X", new Color(0.5f, 0.14f, 0.14f, 1f));
        SetAnchoredRect(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(42f, 42f));
        closeButton.onClick.AddListener((UnityAction)(Action)Close);

        balanceText = CreateText("Balance", panel.transform, string.Empty, 17);
        balanceText.alignment = TextAnchor.MiddleCenter;
        SetAnchoredRect(balanceText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(-32f, 30f));

        BuildTabs(panel.transform);

        var scrollObject = CreateUiObject("WorkerScroll", panel.transform, out var scrollRectTransform);
        var scrollBackground = scrollObject.Il2CppAddComponent<Image>();
        var mask = scrollObject.Il2CppAddComponent<Mask>();
        var scroll = scrollObject.Il2CppAddComponent<ScrollRect>();
        workerScroll = scroll;

        scrollBackground.color = new Color(0.085f, 0.1f, 0.125f, 1f);
        mask.showMaskGraphic = true;
        Stretch(scrollRectTransform, 24f, 24f, 226f, 142f);

        var content = CreateUiObject("Content", scrollObject.transform, out workerList);
        var layout = content.Il2CppAddComponent<VerticalLayoutGroup>();
        var fitter = content.Il2CppAddComponent<ContentSizeFitter>();

        workerList.anchorMin = new Vector2(0f, 1f);
        workerList.anchorMax = new Vector2(1f, 1f);
        workerList.pivot = new Vector2(0.5f, 1f);
        workerList.anchoredPosition = Vector2.zero;
        workerList.sizeDelta = Vector2.zero;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 5f;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = scrollRectTransform;
        scroll.content = workerList;
        scroll.horizontal = false;
        scroll.vertical = true;
        // Wheel-only sensitivity: leave drag handling and movement settings intact.
        scroll.scrollSensitivity = Plugin.Configuration.MouseWheelSensitivity.Value;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        selectionText = CreateText("Selection", panel.transform, string.Empty, 18);
        selectionText.alignment = TextAnchor.MiddleCenter;
        SetAnchoredRect(selectionText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(-32f, 28f));

        feedbackText = CreateText("Feedback", panel.transform, string.Empty, 16);
        feedbackText.color = new Color(1f, 0.78f, 0.25f, 1f);
        feedbackText.alignment = TextAnchor.MiddleCenter;
        SetAnchoredRect(feedbackText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(-32f, 26f));

        boostButton = CreateButton("BoostSelected", panel.transform, "Boost Selected (This Tab)", new Color(0.12f, 0.58f, 0.3f, 1f));
        SetAnchoredRect(boostButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(320f, 46f));
        boostButtonText = boostButton.GetComponentInChildren<Text>();
        boostButton.onClick.AddListener((UnityAction)(Action)BoostSelectedWorkers);
        boostButton.interactable = false;

        menuRoot.SetActive(false);
    }

    private void EnsureUi()
    {
        if (menuRoot != null && workerList != null)
            return;

        // Keep the managed menu component and its Unity canvas in sync across
        // scene transitions, and recover if another component destroys the UI.
        if (menuRoot != null)
            Destroy(menuRoot);

        BuildUi();
    }

    private static GameObject CreateUiObject(string name, Transform parent, out RectTransform rectTransform)
    {
        var result = new GameObject(name, Il2CppType.Of<RectTransform>());
        result.transform.SetParent(parent, false);
        rectTransform = result.Il2CppGetComponent<RectTransform>()!;
        return result;
    }

    private static Text CreateText(string name, Transform parent, string value, int fontSize)
    {
        var host = CreateUiObject(name, parent, out _);
        var text = host.Il2CppAddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.color = Color.white;
        text.text = value;
        text.supportRichText = false;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label, Color color)
    {
        var host = CreateUiObject(name, parent, out _);
        var image = host.Il2CppAddComponent<Image>();
        var button = host.Il2CppAddComponent<Button>();
        image.color = color;
        button.targetGraphic = image;

        var text = CreateText("Label", host.transform, label, 20);
        Stretch(text.rectTransform, 8f, 8f, 4f, 4f);
        text.alignment = TextAnchor.MiddleCenter;
        return button;
    }

    private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }



    private sealed class WorkerEntry(WorkerGroup group, int id, BoostIndicator? indicator, Action? boost)
    {
        public WorkerGroup Group { get; } = group;
        public int Id { get; } = id;
        public BoostIndicator? Indicator { get; } = indicator;
        public Action? Boost { get; } = boost;
        public BoostState State { get; } = BoostState.Read(indicator);
        public bool CanBoost => !PhotonNetwork.IsConnected && Boost != null && State.CanBoost;
        public string Key => $"{Group}:{Id}";
    }

    private sealed class WorkerRow(Button button, Text text, RectTransform[] fills)
    {
        public Button Button { get; } = button;
        public Text Text { get; } = text;
        public RectTransform[] Fills { get; } = fills;
    }
}
