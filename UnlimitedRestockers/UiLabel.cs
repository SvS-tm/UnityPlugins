using System;
using UnityEngine;
using Utilities;

namespace UnlimitedRestockers;

public class UiLabel(IntPtr ptr) : MonoBehaviour(ptr)
{
    public string Text { get; private set; } = string.Empty;
    public Color Color { get; private set; } = Color.blue;
    public Vector3 Offset { get; private set; } = new(0f, 0.4f, 0f);
    public bool UseRendererBoundsForHead { get; private set; } = true;
    public bool FaceCamera { get; private set; } = true;
    public bool ScaleWithDistance { get; private set; } = true;
    public float ScaleAtDistance { get; private set; } = 500f;
    public float MinScale { get; private set; } = 0.01f;
    public float MaxScale { get; private set; } = 0.1f;

    private Transform target;
    private Canvas canvas;
    private RectTransform textRoot;
    private UnityEngine.UI.Text uiText;
    private Renderer[] renderers;
    private Camera camera;

    public void Configure
    (
        string text = default,
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
            uiText.text = Text;
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

    public void Awake()
    {
        target = transform.parent;
        camera = Camera.main;

        // Root has RectTransform when Canvas is added
        canvas = gameObject.Il2CppAddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;

        this.textRoot = this.Il2CppGetComponent<RectTransform>();
        this.textRoot.sizeDelta = new Vector2(200, 60);
        this.textRoot.localScale = Vector3.one * MinScale;

        // Add the Text child
        var textContainer = new GameObject("NameText");

        textContainer.transform.SetParent(transform, false);

        uiText = textContainer.Il2CppAddComponent<UnityEngine.UI.Text>();
        uiText.alignment = TextAnchor.MiddleCenter;
        uiText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        uiText.resizeTextForBestFit = true;
        uiText.resizeTextMinSize = 8;
        uiText.resizeTextMaxSize = 32;
        uiText.color = Color;
        uiText.text = Text;

        var textRoot = uiText.Il2CppGetComponent<RectTransform>();

        textRoot.anchorMin = textRoot.anchorMax = new Vector2(0.5f, 0.5f);
        textRoot.anchoredPosition = Vector2.zero;
        textRoot.sizeDelta = new Vector2(200, 60);

        // Optional billboard
        if (FaceCamera && gameObject.Il2CppGetComponent<FaceToCamera>() == null)
        {
            var bfaceToCamera = gameObject.Il2CppAddComponent<FaceToCamera>();

            bfaceToCamera.UseMainCamera = true;
        }

        // Cache target renderers (for head position)
        renderers = target ? target.IL2CppGetComponentsInChildren<Renderer>(true) : [];
    }

    public void LateUpdate()
    {
        if (target == null) 
            return;

        if (camera == null) 
            camera = Camera.main;

        // place above head (bounds-aware)
        var headPos = target.position + Offset;

        if (UseRendererBoundsForHead && renderers.Length > 0)
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
            float distance = Vector3.Distance(camera.transform.position, transform.position);
            float scale = Mathf.Clamp(distance / Mathf.Max(0.01f, ScaleAtDistance), MinScale, MaxScale);

            transform.localScale = Vector3.one * scale;
        }
        else
        {
            transform.localScale = Vector3.one * MinScale;
        }
    }
}
