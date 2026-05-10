using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UIPanZoom: attach to a UI Image/RawImage that displays the simulation texture.
/// - Drag to pan (supports wraparound via texture repeat)
/// - Scroll to zoom, zoom centers on cursor position
/// Works with RawImage (uses uvRect) or Image with a material (uses main texture offset/scale).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIPanZoom : UIBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler
{
    [Header("Zoom")]
    public float zoom = 1f;
    public float minZoom = 0.25f;
    public float maxZoom = 8f;
    public float zoomSpeed = 0.1f; // multiplier per scroll tick

    [Header("Panning")]
    public bool enableWrapping = true;
    public bool invertDragY = true;

    // internal state
    RectTransform rectTransform;
    RawImage rawImage;
    Graphic graphicImage; // may be Image
    Material runtimeMaterial; // instance if needed

    Vector2 uvOffset = Vector2.zero; // 0..1

    // drag bookkeeping
    Vector2 lastPointerLocal;

    protected override void Start()
    {
        base.Start();
        rectTransform = GetComponent<RectTransform>();
        rawImage = GetComponent<RawImage>();
        graphicImage = GetComponent<Graphic>();

        if (rawImage == null && graphicImage == null)
            Debug.LogWarning("UIPanZoom: no RawImage or Image found on GameObject.");

        if (rawImage != null)
        {
            // ensure texture wrap mode allows repeat
            if (rawImage.texture != null)
            {
                try
                {
                    rawImage.texture.wrapMode = TextureWrapMode.Repeat;
                }
                catch { }
            }
        }
        else if (graphicImage != null)
        {
            if (graphicImage.material != null)
            {
                // use a material instance so we don't overwrite shared material
                runtimeMaterial = new Material(graphicImage.material);
                graphicImage.material = runtimeMaterial;
                // try to set main texture wrap if available
                try
                {
                    var tex = runtimeMaterial.mainTexture;
                    if (tex != null) tex.wrapMode = TextureWrapMode.Repeat;
                }
                catch { }
            }
        }

        // apply initial transform
        ApplyUV();
    }

    void ApplyUV()
    {
        float invScale = 1f / Mathf.Max(0.0001f, zoom);
        Vector2 scale = new Vector2(invScale, invScale);

        if (rawImage != null)
        {
            // RawImage uses uvRect: x,y = offset; width,height = scale
            var r = rawImage.uvRect;
            r.x = Mod01(uvOffset.x);
            r.y = Mod01(uvOffset.y);
            r.width = scale.x;
            r.height = scale.y;
            rawImage.uvRect = r;
        }
        else if (runtimeMaterial != null)
        {
            // try to set main texture offset/scale
            runtimeMaterial.mainTextureOffset = new Vector2(Mod01(uvOffset.x), Mod01(uvOffset.y));
            runtimeMaterial.mainTextureScale = scale;
            // some URP/HDRP shaders use _BaseMap instead of main texture; set those if present
            if (runtimeMaterial.HasProperty("_BaseMap_ST"))
            {
                runtimeMaterial.SetTextureScale("_BaseMap", scale);
                runtimeMaterial.SetTextureOffset("_BaseMap", new Vector2(Mod01(uvOffset.x), Mod01(uvOffset.y)));
            }
        }
        else if (graphicImage != null && graphicImage.materialForRendering != null)
        {
            // fallback: try to modify materialForRendering (may not persist)
            var mat = graphicImage.materialForRendering;
            try { mat.mainTextureOffset = new Vector2(Mod01(uvOffset.x), Mod01(uvOffset.y)); } catch { }
            try { mat.mainTextureScale = scale; } catch { }
        }
    }

    static float Mod01(float v) => v - Mathf.Floor(v);

    public void OnPointerDown(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out lastPointerLocal);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (rectTransform == null) return;
        Vector2 currentLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out currentLocal);
        Vector2 delta = currentLocal - lastPointerLocal;
        lastPointerLocal = currentLocal;

        // convert delta pixels to UV space (0..1) using rect size
        Vector2 size = rectTransform.rect.size;
        if (size.x <= 0 || size.y <= 0) return;

        Vector2 uvDelta = new Vector2(delta.x / size.x, delta.y / size.y);
        if (invertDragY) uvDelta.y = -uvDelta.y;

        // when zoomed, movement of image corresponds to uv scaled by current scale
        uvDelta /= zoom;

        uvOffset -= uvDelta;

        if (!enableWrapping)
        {
            // clamp so texture doesn't leave area (when no wrap). clamp to [0..1-scale]
            float invScale = 1f / Mathf.Max(0.0001f, zoom);
            float maxOffset = Mathf.Max(0f, 1f - invScale);
            uvOffset.x = Mathf.Clamp(uvOffset.x, 0f, maxOffset);
            uvOffset.y = Mathf.Clamp(uvOffset.y, 0f, maxOffset);
        }

        ApplyUV();
    }

    public void OnScroll(PointerEventData eventData)
    {
        float delta = eventData.scrollDelta.y;
        if (Mathf.Abs(delta) < 0.0001f) return;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return;

        // store previous values
        float prevZoom = zoom;
        float prevScale = 1f / Mathf.Max(0.0001f, prevZoom);

        // adjust zoom
        // we'll use multiplicative zoom for smoothness
        float factor = 1f + delta * zoomSpeed;
        if (factor <= 0f) factor = 0.01f;
        zoom = Mathf.Clamp(zoom * factor, minZoom, maxZoom);

        float newScale = 1f / zoom;

        // compute pointer normalized position within rect (0..1)
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out localPoint);
        Vector2 size = rectTransform.rect.size;
        Vector2 normalized = new Vector2((localPoint.x + size.x * 0.5f) / size.x, (localPoint.y + size.y * 0.5f) / size.y);

        // adjust offset so that the pixel under cursor remains at same world position
        // change in displayed uv extents = prevScale - newScale
        Vector2 offsetChange = normalized * (prevScale - newScale);
        uvOffset += offsetChange;

        if (!enableWrapping)
        {
            float maxOffset = Mathf.Max(0f, 1f - newScale);
            uvOffset.x = Mathf.Clamp(uvOffset.x, 0f, maxOffset);
            uvOffset.y = Mathf.Clamp(uvOffset.y, 0f, maxOffset);
        }

        ApplyUV();
    }

    /// <summary>
    /// Reset view to default (centered, zoom = 1)
    /// </summary>
    public void ResetView()
    {
        zoom = 1f;
        uvOffset = Vector2.zero;
        ApplyUV();
    }

    /// <summary>
    /// Set zoom and optionally keep center at current center.
    /// </summary>
    public void SetZoom(float value)
    {
        value = Mathf.Clamp(value, minZoom, maxZoom);
        zoom = value;
        ApplyUV();
    }
}
