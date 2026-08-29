using System;
using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
using Utilities;

namespace UnlimitedRestockers;

public class UiLabel(IntPtr ptr) : MonoBehaviour(ptr)
{
    private static readonly string ContainerId = Guid.NewGuid().ToString();
    private const float VisualAnchorRefreshInterval = 0.5f;

    public Func<string> Text
    {
        [HideFromIl2Cpp]
        get;
        [HideFromIl2Cpp]
        private set;
    } = () => string.Empty;
    public Color Color { get; private set; } = Color.blue;
    public Vector3 Offset { get; private set; } = new(0f, 0.4f, 0f);
    public bool UseRendererBoundsForHead { get; private set; } = true;
    public bool FaceCamera { get; private set; } = true;
    public bool ScaleWithDistance { get; private set; } = true;
    public float ScaleAtDistance { get; private set; } = 500f;
    public float MinScale { get; private set; } = 0.01f;
    public float MaxScale { get; private set; } = 0.1f;

    private Transform target = default!;
    private Canvas canvas = default!;
    private GameObject textContainer = default!;
    private RectTransform textRoot = default!;
    private UnityEngine.UI.Text uiText = default!;
    private Renderer[] renderers = [];
    private Transform? headBone;
    private Camera camera = default!;
    private float nextVisualAnchorRefreshTime;

    [HideFromIl2Cpp]
    public void Configure
    (
        Func<string>? text = default,
        Color? color = default,
        Vector3? offset = default,
        Vector3? scale = default,
        bool? useRendererBoundsForHead = default,
        bool? faceCamera = default,
        bool? scaleWithDistance = default,
        float? scaleAtDistance = default,
        float? minScale = default,
        float? maxScale = default
    )
    { 
        Text = text ?? Text;
        Color = color ?? Color;

        if (uiText != null)
        { 
            uiText.color = Color;
        }

        Offset = offset ?? Offset;
        UseRendererBoundsForHead = useRendererBoundsForHead ?? UseRendererBoundsForHead;
        FaceCamera = faceCamera ?? FaceCamera;
        ScaleWithDistance = scaleWithDistance ?? ScaleWithDistance;
        ScaleAtDistance = scaleAtDistance ?? ScaleAtDistance;
        MinScale = minScale ?? MinScale;
        MaxScale = maxScale ?? MaxScale;
    }

    [HideFromIl2Cpp]
    private T_Result ThrowAndDisable<T_Result>(Exception exception)
    { 
        enabled = false;

        throw exception;
    }

    public void Awake()
    {
        // Root has RectTransform when Canvas is added
        canvas = gameObject.Il2CppGetOrAddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;

        this.textRoot = this.Il2CppGetComponent<RectTransform>()
            ?? ThrowAndDisable<RectTransform>(new InvalidOperationException("Couldn't fetch ReactTransform for Canvas"));

        this.textRoot.sizeDelta = new Vector2(200, 60);
        this.textRoot.localScale = Vector3.one * MinScale;

        // Add the Text child
        textContainer = new GameObject(ContainerId);

        textContainer.transform.SetParent(transform, false);

        uiText = textContainer.Il2CppAddComponent<UnityEngine.UI.Text>();
        uiText.alignment = TextAnchor.MiddleCenter;
        uiText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        uiText.resizeTextForBestFit = true;
        uiText.resizeTextMinSize = 8;
        uiText.resizeTextMaxSize = 32;
        uiText.color = Color;
        uiText.text = Text();

        var textRoot = uiText.Il2CppGetComponent<RectTransform>()
            ?? ThrowAndDisable<RectTransform>(new InvalidOperationException("Couldn't fetch ReactTransform for UI.Text")); ;

        textRoot.anchorMin = textRoot.anchorMax = new Vector2(0.5f, 0.5f);
        textRoot.anchoredPosition = Vector2.zero;
        textRoot.sizeDelta = new Vector2(200, 60);

        if (FaceCamera && gameObject.Il2CppGetComponent<FaceToCamera>() == null)
        {
            gameObject.Il2CppAddComponent<FaceToCamera>();
        }
    }

    [HideFromIl2Cpp]
    private IEnumerator Init()
    {
        yield return new WaitForEndOfFrame();

        target = transform.parent;
        camera = Camera.main;

        RefreshVisualAnchors();
    }

    public void OnEnable()
    {
        StartCoroutine(Init().WrapToIl2Cpp());
    }

    public void LateUpdate()
    {
        if (target == null) 
            return;

        if (camera == null) 
            camera = Camera.main;

        uiText.text = Text();

        // CharacterModelComponent creates some of the newer employee models after
        // this label initializes. Refresh until their animator/renderers are present.
        if (Time.unscaledTime >= nextVisualAnchorRefreshTime)
            RefreshVisualAnchors();

        // Prefer the humanoid head bone because it follows model-specific height.
        // Active renderer bounds remain a fallback for non-humanoid models.
        var headPos = target.position + Offset;

        if (headBone != null && headBone.gameObject.activeInHierarchy)
        {
            headPos = headBone.position + Offset;
        }
        else if (UseRendererBoundsForHead && TryGetActiveRendererBounds(out var bounds))
        {
            headPos = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) + Offset;
        }

        transform.position = headPos;

        // distance scaling
        if (ScaleWithDistance && camera != null)
        {
            var distance = Vector3.Distance(camera.transform.position, transform.position);
            var scale = Mathf.Clamp(distance / Mathf.Max(0.01f, ScaleAtDistance), MinScale, MaxScale);

            transform.localScale = Vector3.one * scale;
        }
        else
        {
            transform.localScale = Vector3.one * MinScale;
        }
    }

    [HideFromIl2Cpp]
    private void RefreshVisualAnchors()
    {
        nextVisualAnchorRefreshTime = Time.unscaledTime + VisualAnchorRefreshInterval;
        headBone = null;

        if (target == null)
        {
            renderers = [];
            return;
        }

        renderers = target.IL2CppGetComponentsInChildren<Renderer>(false);
        var animators = target.IL2CppGetComponentsInChildren<Animator>(false);

        foreach (var animator in animators)
        {
            if (animator == null || !animator.gameObject.activeInHierarchy)
                continue;

            try
            {
                var candidate = animator.GetBoneTransform(HumanBodyBones.Head);

                if (candidate != null)
                {
                    headBone = candidate;
                    break;
                }
            }
            catch
            {
                // Some employee animators are not humanoid; renderer bounds handle them.
            }
        }
    }

    [HideFromIl2Cpp]
    private bool TryGetActiveRendererBounds(out Bounds bounds)
    {
        bounds = default;
        var foundRenderer = false;

        foreach (var renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            if (foundRenderer)
                bounds.Encapsulate(renderer.bounds);
            else
            {
                bounds = renderer.bounds;
                foundRenderer = true;
            }
        }

        return foundRenderer;
    }
}
