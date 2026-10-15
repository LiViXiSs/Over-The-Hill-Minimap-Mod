using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace OverTheHill;

// Copy graphics only: never clone map controllers, input handlers, or networking components.
internal sealed class NativeMapMirror
{
    private MapView source;
    private float nextSearch;
    private float nextRefresh;
    private readonly Dictionary<int, Copy> copies = new();
    private readonly Dictionary<IntPtr, Matrix4x4> mapAncestors = new();
    private readonly HashSet<int> seen = new();
    private RectTransform below;
    private RectTransform above;
    private bool reported;
    private bool geometryReported;
    private sealed class Copy
    {
        public Graphic Original;
        public Graphic Graphic;
    }

    public void Reset()
    {
        source = null;
        nextSearch = nextRefresh = 0;
        reported = false;
        geometryReported = false;
        Clear();
    }

    private void Clear()
    {
        foreach (var copy in copies.Values)
            if (copy.Graphic) UnityEngine.Object.Destroy(copy.Graphic.gameObject);
        copies.Clear();
    }

    private void FindSource()
    {
        if (source && source.IsInitialized) return;
        if (Time.unscaledTime < nextSearch) return;
        nextSearch = Time.unscaledTime + 2;
        foreach (var candidate in Resources.FindObjectsOfTypeAll<MapView>())
        {
            if (!candidate || !candidate.gameObject.scene.IsValid() || !candidate.IsInitialized || candidate.IsSpawningMap) continue;
            if (!candidate.mapRawImage || !candidate.mapRawImage.texture) continue;
            if (!source || source.Pointer != candidate.Pointer) Clear();
            source = candidate;
            break;
        }
    }

    public bool TryGetCrop(Vector3 world, float worldSpan, out Rect crop)
    {
        crop = default;
        FindSource();
        if (!source || !source.IsInitialized || !source.mapRawImage || source.mapWidth <= 0) return false;
        var rect = source.mapRawImage.rectTransform.rect;
        if (rect.width <= 0 || rect.height <= 0) return false;
        // Native conversion includes the map texture's border padding, unlike the fog grid.
        var position = source.WorldToUIPosition(world);
        float x = (position.x - rect.xMin) / rect.width;
        float y = (position.y - rect.yMin) / rect.height;
        var px = source.WorldToUIPosition(world + new Vector3(worldSpan, 0, 0));
        var pz = source.WorldToUIPosition(world + new Vector3(0, 0, worldSpan));
        float sx = Mathf.Max(0.001f, Mathf.Abs(px.x - position.x) / rect.width);
        float sy = Mathf.Max(0.001f, Mathf.Abs(pz.y - position.y) / rect.height);
        crop = new Rect(x - sx / 2, y - sy / 2, sx, sy);
        if (!geometryReported)
        {
            geometryReported = true;
            MelonLoader.MelonLogger.Msg($"[Minimap] Native alignment: world={world}, UI={position}, rect={rect}, crop={crop}, border={source.borderSizeInPixels}.");
        }
        return true;
    }

    public bool Update(RectTransform viewport, RawImage map, RawImage fog, Rect crop, float width)
    {
        if (!below)
        {
            below = Layer("Native icons below fog", viewport);
            below.SetSiblingIndex(fog.transform.GetSiblingIndex());
            above = Layer("Native icons above fog", viewport);
        }
        FindSource();
        if (!source || !source.IsInitialized || !source.mapRawImage)
        {
            below.gameObject.SetActive(false);
            above.gameObject.SetActive(false);
            return false;
        }
        below.gameObject.SetActive(true);
        above.gameObject.SetActive(true);

        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + 0.1f;
            // The closed map no longer runs Update. Refresh only its display routines;
            // never run MapView.Update (cursor/input), OnEnable, or a map controller.
            if (!source.gameObject.activeInHierarchy)
            {
                source.UpdatePlayerIndicators();
                source.UpdateMapMovingIndicators();
                if (MapNetworkManager.Instance) source.UpdatePlayersPinpoints();
                if (source.POIMapIconsByPOI != null)
                    foreach (var pair in source.POIMapIconsByPOI)
                        if (pair.Value) pair.Value.UpdateIconAndVisibility();
            }
            Reconcile(source.behindFogOfWarTransform, below);
            Reconcile(source.aboveFogOfWarTransform, above, false);
            // Some map prefabs keep player/pin containers outside the POI layers.
            if (source.playerIndicators != null)
                foreach (var player in source.playerIndicators)
                    if (player && player.MovingParent) Reconcile(player.MovingParent, above, false);
            if (source.pinpointIndicators != null)
                foreach (var pin in source.pinpointIndicators)
                    if (pin && pin.rectTransform) Reconcile(pin.rectTransform, above, false);
            foreach (var id in copies.Keys.Where(id => !seen.Contains(id)).ToArray())
            {
                if (copies[id].Graphic) UnityEngine.Object.Destroy(copies[id].Graphic.gameObject);
                copies.Remove(id);
            }
            seen.Clear();
        }

        // Reuse the original map imagery and material so road/trail detail matches.
        map.texture = source.mapRawImage.texture;
        map.material = source.mapRawImage.material;
        map.color = source.mapRawImage.color;
        if (source.fogOfWarRawImage)
        {
            fog.texture = source.fogOfWarRawImage.texture;
            fog.material = source.fogOfWarRawImage.material;
            fog.color = source.fogOfWarRawImage.color;
        }
        var mapRect = source.mapRawImage.rectTransform;
        BuildMapAncestors(mapRect);
        // Map and fog need their own geometry: the native map has border padding,
        // and applying a shared texture crop to both displaces the fog/trail detail.
        SyncRawGeometry(source.mapRawImage, map, mapRect, crop, width);
        if (source.fogOfWarRawImage) SyncRawGeometry(source.fogOfWarRawImage, fog, mapRect, crop, width);
        foreach (var copy in copies.Values) Sync(copy, mapRect, crop, width);
        if (!reported)
        {
            reported = true;
            MelonLoader.MelonLogger.Msg($"[Minimap] Mirroring normal map graphics ({copies.Count} images); live marker refresh 10 Hz.");
            var types = source.GetComponentsInChildren<Graphic>(true)
                .GroupBy(g => g.GetIl2CppType().FullName)
                .Select(g => g.Key + ":" + g.Count());
            MelonLoader.MelonLogger.Msg("[Minimap] Native map graphic types: " + string.Join(", ", types));
        }
        return true;
    }

    private void SyncRawGeometry(RawImage original, RawImage target, RectTransform mapRect, Rect crop, float width)
    {
        GetProjectedCorners(original.rectTransform, mapRect, crop, width, out var p0, out var p1, out var p3);
        target.rectTransform.anchoredPosition = p0;
        target.rectTransform.sizeDelta = new Vector2((p3 - p0).magnitude, (p1 - p0).magnitude);
        target.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(p3.y - p0.y, p3.x - p0.x) * Mathf.Rad2Deg);
        target.uvRect = original.uvRect;
    }

    private void Reconcile(RectTransform root, RectTransform destination, bool first = true)
    {
        if (first) seen.Clear();
        if (!root) return;
        foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            if (!graphic || graphic.Pointer == source.mapRawImage.Pointer ||
                (source.fogOfWarRawImage && graphic.Pointer == source.fogOfWarRawImage.Pointer)) continue;
            // Keep cursor/selection tools out of the driving HUD.
            if (source.cursorGameObject && graphic.transform.IsChildOf(source.cursorGameObject.transform)) continue;
            var image = graphic.TryCast<Image>();
            var raw = graphic.TryCast<RawImage>();
            if (!image && !raw) continue;
            int id = graphic.GetInstanceID();
            seen.Add(id);
            if (!copies.TryGetValue(id, out var copy))
            {
                var rect = Layer("Map: " + graphic.name, destination);
                Graphic target = image ? rect.gameObject.AddComponent<Image>() : rect.gameObject.AddComponent<RawImage>();
                target.raycastTarget = false;
                target.Cast<MaskableGraphic>().maskable = true;
                copy = new Copy { Original = graphic, Graphic = target };
                copies.Add(id, copy);
            }
            copy.Graphic.transform.SetParent(destination, false);
            copy.Graphic.transform.SetAsLastSibling();
        }
    }

    private void Sync(Copy copy, RectTransform mapRect, Rect crop, float width)
    {
        var original = copy.Original;
        if (!original || !copy.Graphic) return;
        bool visible = original.enabled;
        // Stop at the map-content layer: its screen/container may be disabled solely
        // because the full map is closed. Individual icon visibility still applies.
        for (Transform parent = original.transform; parent; parent = parent.parent)
        {
            if (parent.Pointer == source.transform.Pointer ||
                (source.behindFogOfWarTransform && parent.Pointer == source.behindFogOfWarTransform.Pointer) ||
                (source.aboveFogOfWarTransform && parent.Pointer == source.aboveFogOfWarTransform.Pointer) ||
                parent.Pointer == mapRect.Pointer) break;
            if (!parent.gameObject.activeSelf) { visible = false; break; }
        }
        copy.Graphic.gameObject.SetActive(visible);
        if (!visible || mapRect.rect.width <= 0 || mapRect.rect.height <= 0) return;
        GetProjectedCorners(original.rectTransform, mapRect, crop, width, out var p0, out var p1, out var p3);
        var target = copy.Graphic.rectTransform;
        target.anchoredPosition = p0;
        target.sizeDelta = new Vector2((p3 - p0).magnitude, (p1 - p0).magnitude);
        target.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(p3.y - p0.y, p3.x - p0.x) * Mathf.Rad2Deg);
        copy.Graphic.color = original.color;
        copy.Graphic.material = original.material;
        var image = original.TryCast<Image>();
        if (image)
        {
            var dest = copy.Graphic.Cast<Image>();
            dest.sprite = image.overrideSprite;
            dest.type = image.type;
            dest.preserveAspect = image.preserveAspect;
            dest.fillAmount = image.fillAmount;
            dest.fillCenter = image.fillCenter;
            dest.fillMethod = image.fillMethod;
            dest.fillOrigin = image.fillOrigin;
            dest.fillClockwise = image.fillClockwise;
            dest.pixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier;
        }
        else
        {
            var raw = original.Cast<RawImage>();
            var dest = copy.Graphic.Cast<RawImage>();
            dest.texture = raw.texture;
            dest.uvRect = raw.uvRect;
        }
    }

    private void BuildMapAncestors(RectTransform map)
    {
        mapAncestors.Clear();
        var matrix = Matrix4x4.identity;
        for (Transform current = map; current; current = current.parent)
        {
            mapAncestors[current.Pointer] = matrix;
            matrix = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * matrix;
        }
    }

    private void GetProjectedCorners(RectTransform original, RectTransform map, Rect crop, float width,
        out Vector2 p0, out Vector2 p1, out Vector2 p3)
    {
        // Cancel shared ancestors before inversion. A closed UI screen may have
        // zero world scale; worldToLocalMatrix cannot recover coordinates then.
        var matrix = Matrix4x4.identity;
        for (Transform current = original; current; current = current.parent)
        {
            if (mapAncestors.TryGetValue(current.Pointer, out var mapToCommon))
            {
                matrix = mapToCommon.inverse * matrix;
                break;
            }
            matrix = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * matrix;
        }
        var rect = original.rect;
        p0 = Project(map, matrix.MultiplyPoint3x4(new Vector3(rect.xMin, rect.yMin, 0)), crop, width);
        p1 = Project(map, matrix.MultiplyPoint3x4(new Vector3(rect.xMin, rect.yMax, 0)), crop, width);
        p3 = Project(map, matrix.MultiplyPoint3x4(new Vector3(rect.xMax, rect.yMin, 0)), crop, width);
    }

    private static Vector2 Project(RectTransform map, Vector3 local, Rect crop, float width)
    {
        var rect = map.rect;
        return new Vector2(((local.x - rect.xMin) / rect.width - crop.x) / crop.width * width,
            ((local.y - rect.yMin) / rect.height - crop.y) / crop.height * width);
    }

    private static RectTransform Layer(string name, Transform parent)
    {
        var go = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }
}
