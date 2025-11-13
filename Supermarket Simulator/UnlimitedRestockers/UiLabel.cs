using System;
using System.Collections;
using System.Linq;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
using Utilities;

namespace UnlimitedRestockers;

public class UiLabel(IntPtr ptr) : MonoBehaviour(ptr)
{
    private static readonly string ContainerId = Guid.NewGuid().ToString();

    public Func<string> Text { get; private set; } = () => string.Empty;
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
    private Renderer[] renderers = default!;
    private Camera camera = default!;

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

    private IEnumerator Init()
    {
        yield return new WaitForEndOfFrame();

        target = transform.parent;
        camera = Camera.main;

        // Cache target renderers
        renderers = target ? target.IL2CppGetComponentsInChildren<Renderer>(true) : [];
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

        // place above target (bounds-aware)
        var headPos = target.position + Offset;

        if (UseRendererBoundsForHead && renderers.Length > 0 && renderers[0] != null)
        {
            var bounds = new Bounds(renderers[0].bounds.center, Vector3.zero);

            for (int index = 1; index < renderers.Length; index++) 
                bounds.Encapsulate(renderers[index].bounds);

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
}
