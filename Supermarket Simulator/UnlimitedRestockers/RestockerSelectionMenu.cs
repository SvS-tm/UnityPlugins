using MyBox;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Utilities;

namespace UnlimitedRestockers;

public class RestockerSelectionMenu(IntPtr ptr) : MonoBehaviour(ptr)
{
    private const float MenuToggleCooldown = 0.25f;

    private readonly List<IDisposable> resources = new(1);

    private InputAction menuAction = default!;
    private GameObject menuRoot = default!;
    private RectTransform workerList = default!;
    private Text titleText = default!;
    private Text selectionText = default!;
    private Button fireButton = default!;

    private int selectedEmployeeId = -1;
    private string activeWorkersSignature = string.Empty;
    private bool isOpen;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;

    public void Awake()
    {
        BuildUi();

        menuAction = InputHelpers.ParseAction
        (
            "restockerSelectionMenuAction",
            Plugin.Configuration.RestockerMenuBinding.Value
        );

        resources.Add
        (
            menuAction.WithCooldownCallback(Toggle, () => MenuToggleCooldown)
        );

        Plugin.Configuration.RestockerMenuBinding.SettingChanged += OnMenuBindingChanged;
    }

    public void OnDestroy()
    {
        Plugin.Configuration.RestockerMenuBinding.SettingChanged -= OnMenuBindingChanged;

        foreach (var resource in resources)
            resource.Dispose();

        if (isOpen)
            RestoreCursor();

        if (menuRoot != null)
            Destroy(menuRoot);
    }

    public void Update()
    {
        if (!isOpen)
            return;

        // The game may update the cursor during play, so keep it available while
        // this overlay is open and restore its previous state when it closes.
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            Close();
            return;
        }

        var signature = GetActiveWorkersSignature();

        if (signature != activeWorkersSignature)
            RebuildWorkerList();
    }

    private void OnMenuBindingChanged(object? sender, EventArgs eventArgs)
    {
        menuAction.Rebind(Plugin.Configuration.RestockerMenuBinding.Value);
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
        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;

        isOpen = true;
        menuRoot.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        RebuildWorkerList();
    }

    private void Close()
    {
        isOpen = false;
        menuRoot.SetActive(false);
        RestoreCursor();
    }

    private void RestoreCursor()
    {
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
    }

    private void SelectWorker(int employeeId)
    {
        selectedEmployeeId = employeeId;
        RebuildWorkerList();
    }

    private void FireSelectedWorker()
    {
        var manager = GetEmployeeManager();

        if (manager == null || selectedEmployeeId < 0)
            return;

        var clerk = manager.GetRestockerByID(selectedEmployeeId);

        if (clerk == null)
        {
            selectedEmployeeId = -1;
            RebuildWorkerList();
            return;
        }

        Plugin.Logger.LogInfo($"Firing selected restocker: {selectedEmployeeId}");
        manager.FireRestocker(selectedEmployeeId);

        selectedEmployeeId = -1;
        activeWorkersSignature = string.Empty;
        RebuildWorkerList();
    }

    private void RebuildWorkerList()
    {
        for (var index = workerList.childCount - 1; index >= 0; --index)
            Destroy(workerList.GetChild(index).gameObject);

        var manager = GetEmployeeManager();
        var hasSelectedWorker = false;
        var workerCount = 0;

        if (manager != null)
        {
            foreach (var clerk in manager.m_ActiveRestockers)
            {
                var employeeId = clerk.EmployeeId;
                var isSelected = employeeId == selectedEmployeeId;

                hasSelectedWorker |= isSelected;
                workerCount++;
                AddWorkerButton(employeeId, isSelected);
            }
        }

        if (!hasSelectedWorker)
            selectedEmployeeId = -1;

        titleText.text = $"Active Restockers ({workerCount})";
        selectionText.text = selectedEmployeeId < 0
            ? "Select a restocker by ID"
            : $"Selected restocker: {selectedEmployeeId}";

        fireButton.interactable = selectedEmployeeId >= 0;
        activeWorkersSignature = GetActiveWorkersSignature();
    }

    private string GetActiveWorkersSignature()
    {
        var manager = GetEmployeeManager();

        if (manager == null)
            return string.Empty;

        var ids = new List<int>(manager.m_ActiveRestockers.Count);

        foreach (var clerk in manager.m_ActiveRestockers)
            ids.Add(clerk.EmployeeId);

        ids.Sort();
        return string.Join(",", ids);
    }

    private void AddWorkerButton(int employeeId, bool isSelected)
    {
        var row = CreateUiObject($"Restocker_{employeeId}", workerList, out var rowRect);
        var rowImage = row.Il2CppAddComponent<Image>();
        var rowButton = row.Il2CppAddComponent<Button>();
        var layout = row.Il2CppAddComponent<LayoutElement>();

        rowImage.color = isSelected
            ? new Color(0.18f, 0.55f, 0.9f, 0.95f)
            : new Color(0.16f, 0.18f, 0.22f, 0.95f);
        rowButton.targetGraphic = rowImage;
        rowButton.onClick.AddListener((UnityAction)(Action)(() => SelectWorker(employeeId)));
        layout.preferredHeight = 42f;
        layout.minHeight = 42f;
        rowRect.sizeDelta = new Vector2(0f, 42f);

        var label = CreateText("Label", row.transform, $"Restocker ID: {employeeId}", 20);
        Stretch(label.rectTransform, 14f, 14f, 0f, 0f);
        label.alignment = TextAnchor.MiddleLeft;
    }

    private void BuildUi()
    {
        menuRoot = new GameObject("UnlimitedRestockers_SelectionMenu");

        var canvas = menuRoot.Il2CppAddComponent<Canvas>();
        menuRoot.Il2CppAddComponent<CanvasScaler>();
        menuRoot.Il2CppAddComponent<GraphicRaycaster>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000;

        var panel = CreateUiObject("Panel", menuRoot.transform, out var panelRect);
        var panelImage = panel.Il2CppAddComponent<Image>();
        panelImage.color = new Color(0.055f, 0.065f, 0.085f, 0.97f);
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(500f, 620f);
        panelRect.anchoredPosition = Vector2.zero;

        titleText = CreateText("Title", panel.transform, "Active Restockers", 28);
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        SetAnchoredRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(-32f, 52f));

        var closeButton = CreateButton("Close", panel.transform, "X", new Color(0.5f, 0.14f, 0.14f, 1f));
        SetAnchoredRect(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(42f, 42f));
        closeButton.onClick.AddListener((UnityAction)(Action)Close);

        var scrollObject = CreateUiObject("WorkerScroll", panel.transform, out var scrollRectTransform);
        var scrollBackground = scrollObject.Il2CppAddComponent<Image>();
        var mask = scrollObject.Il2CppAddComponent<Mask>();
        var scroll = scrollObject.Il2CppAddComponent<ScrollRect>();

        scrollBackground.color = new Color(0.09f, 0.105f, 0.13f, 1f);
        mask.showMaskGraphic = true;
        Stretch(scrollRectTransform, 24f, 24f, 82f, 116f);

        var content = CreateUiObject("Content", scrollObject.transform, out workerList);
        var verticalLayout = content.Il2CppAddComponent<VerticalLayoutGroup>();
        var sizeFitter = content.Il2CppAddComponent<ContentSizeFitter>();

        workerList.anchorMin = new Vector2(0f, 1f);
        workerList.anchorMax = new Vector2(1f, 1f);
        workerList.pivot = new Vector2(0.5f, 1f);
        workerList.anchoredPosition = Vector2.zero;
        workerList.sizeDelta = Vector2.zero;
        verticalLayout.padding = new RectOffset(8, 8, 8, 8);
        verticalLayout.spacing = 6f;
        verticalLayout.childControlWidth = true;
        verticalLayout.childForceExpandWidth = true;
        verticalLayout.childControlHeight = true;
        verticalLayout.childForceExpandHeight = false;
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = scrollRectTransform;
        scroll.content = workerList;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        selectionText = CreateText("Selection", panel.transform, "Select a restocker by ID", 18);
        SetAnchoredRect(selectionText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(-32f, 36f));
        selectionText.alignment = TextAnchor.MiddleCenter;

        fireButton = CreateButton("FireSelected", panel.transform, "Fire Selected", new Color(0.72f, 0.18f, 0.16f, 1f));
        SetAnchoredRect(fireButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(220f, 48f));
        fireButton.onClick.AddListener((UnityAction)(Action)FireSelectedWorker);
        fireButton.interactable = false;

        menuRoot.SetActive(false);
    }

    private static GameObject CreateUiObject(string name, Transform parent, out RectTransform rectTransform)
    {
        var result = new GameObject(name, Il2CppType.Of<RectTransform>());
        result.transform.SetParent(parent, false);

        // Empty layout containers need to be created with RectTransform explicitly;
        // otherwise Unity gives them a regular Transform that cannot drive UI layout.
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

    private static void SetAnchoredRect
    (
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 position,
        Vector2 size
    )
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static EmployeeManager? GetEmployeeManager()
    {
        return Singleton<EmployeeManager>.Instance;
    }
}
