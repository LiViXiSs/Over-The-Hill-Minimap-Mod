using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace OverTheHill;

// Discovered POIs use world coordinates and fixed-size glyphs, independent of the closed map's layout.
internal sealed class PoiMarkers
{
    private readonly Dictionary<int, Marker> markers = new();
    private float nextRefresh;
    private bool reported;
    private float nextPositionReport;
    private float lastReportedSpan = -1;
    private sealed class Marker
    {
        public PointOfInterest Poi;
        public PointOfInterestMapIcon Native;
        public RectTransform Root;
        public Image Background;
        public Image Icon;
    }

    public void Reset()
    {
        foreach (var marker in markers.Values)
            if (marker.Root) UnityEngine.Object.Destroy(marker.Root.gameObject);
        markers.Clear();
        nextRefresh = 0;
        reported = false;
        nextPositionReport = 0;
        lastReportedSpan = -1;
    }

    public void Update(RectTransform parent, NativeMapMirror native, RawImage renderedMap, Rect crop, float width)
    {
        var source = native.Source;
        // The full-map UI may release its dictionary when closed. Only sprite/data
        // reconciliation depends on that UI; existing markers must still move on zoom.
        if (Time.unscaledTime >= nextRefresh && source && source.POIMapIconsByPOI != null)
        {
            nextRefresh = Time.unscaledTime + 0.2f;
            var present = new HashSet<int>();
            int discovered = 0;
            foreach (var pair in source.POIMapIconsByPOI)
            {
                var poi = pair.Key;
                var original = pair.Value;
                if (!poi || !original) continue;
                int id = poi.GetInstanceID();
                present.Add(id);
                if (!markers.TryGetValue(id, out var marker))
                {
                    var root = Rect("POI " + id, parent, Vector2.zero, new Vector2(26, 26));
                    markers.Add(id, marker = new Marker { Poi = poi, Native = original, Root = root,
                        Background = Image("Background", root, new Vector2(26, 26)),
                        Icon = Image("Icon", root, new Vector2(21, 21)) });
                }
                marker.Native = original;
                var data = original.PointOfInterestData;
                // Only show POIs already visible in the player's saved discovery state.
                bool visible = data != null ? data.CurrentState != PointOfInterestData.State.Hidden : marker.Root.gameObject.activeSelf;
                var glyph = original.iconImage;
                var sprite = glyph ? (glyph.overrideSprite ? glyph.overrideSprite : glyph.sprite) : null;
                if (!sprite) sprite = marker.Icon.sprite;
                visible &= sprite;
                marker.Root.gameObject.SetActive(visible);
                if (!visible) continue;
                discovered++;
                marker.Icon.sprite = sprite;
                var color = glyph ? glyph.color : marker.Icon.color;
                color.a = 1;
                marker.Icon.color = color;
                var background = original.backgroundImage;
                bool showBackground = original.PointOfInterestInfo != null && original.PointOfInterestInfo.ShowIconBackground && background;
                marker.Background.gameObject.SetActive(showBackground);
                if (showBackground)
                {
                    marker.Background.sprite = background.overrideSprite ? background.overrideSprite : background.sprite;
                    var bgColor = background.color;
                    bgColor.a = 1;
                    marker.Background.color = bgColor;
                }
            }
            // Closing/rebuilding the full map may temporarily empty its dictionary.
            // Keep cached POIs until their world object is destroyed or the scene resets.
            foreach (var id in markers.Keys.Where(id => !markers[id].Poi).ToArray())
            {
                if (markers[id].Root) UnityEngine.Object.Destroy(markers[id].Root.gameObject);
                markers.Remove(id);
            }
            if (!reported && markers.Count > 0)
            {
                reported = true;
                MelonLoader.MelonLogger.Msg($"[Minimap] Direct POI markers: {markers.Count} total, {discovered} discovered with sprites. Fixed size 21px.");
            }
        }
        int inView = 0;
        foreach (var marker in markers.Values)
        {
            if (!marker.Poi || !marker.Root || !marker.Root.gameObject.activeSelf) continue;
            // Matches native MapView.UpdateObjectives: POI.transform.position ->
            // WorldToUIPosition. Never sample or cache the full map's zoomed UI transforms.
            Vector2 position;
            if (native.TryGetCrop(marker.Poi.transform.position, 1, out var location)) position = location.center;
            else
            {
                var manager = MapManager.Instance;
                if (!manager || manager.FogOfWarManager == null) continue;
                var grid = manager.FogOfWarManager.WorldToGridPosition(marker.Poi.transform.position);
                position = grid / FogOfWarManager.FowArraySize;
            }
            // Attach the marker to the actual rendered map quad. Do not calculate a
            // second screen-space crop: its pivot/scale/origin can differ from the image.
            var rect = renderedMap.rectTransform.rect;
            var pointOnImage = new Vector3(rect.xMin + position.x * rect.width,
                rect.yMin + position.y * rect.height, 0);
            var transform = renderedMap.rectTransform;
            marker.Root.localPosition = transform.localPosition + transform.localRotation *
                Vector3.Scale(pointOnImage, transform.localScale);
            marker.Root.localRotation = Quaternion.Inverse(parent.localRotation);
            if (crop.Contains(position)) inView++;
        }
        if ((Time.unscaledTime >= nextPositionReport || Mathf.Abs(lastReportedSpan - crop.width) > 0.00001f) && markers.Count > 0)
        {
            lastReportedSpan = crop.width;
            nextPositionReport = Time.unscaledTime + 30;
            MelonLoader.MelonLogger.Msg($"[Minimap] POIs using world projection: {inView} within current crop, {markers.Count} total; crop={crop}.");
            var imageRect = renderedMap.rectTransform;
            MelonLoader.MelonLogger.Msg($"[Minimap] Rendered map quad: rect={imageRect.rect}, position={imageRect.localPosition}, scale={imageRect.localScale}, uv={renderedMap.uvRect}.");
            foreach (var entry in markers.Values.Where(m => m.Poi && m.Root && m.Root.gameObject.activeSelf)
                .OrderBy(m => m.Root.localPosition.sqrMagnitude).Take(3))
            {
                MelonLoader.MelonLogger.Msg($"[Minimap] POI sample {entry.Poi.GetInstanceID()}: world={entry.Poi.transform.position}, nativeAnchor={entry.Native?.rectTransform.anchoredPosition}, rendered={entry.Root.localPosition}, sprite={entry.Icon.sprite?.name}.");
            }
        }
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 size)
    {
        var go = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image Image(string name, Transform parent, Vector2 size)
    {
        var rect = Rect(name, parent, new Vector2(0.5f, 0.5f), size);
        var image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        // Default UGUI material avoids full-map-specific shader/mask dependencies.
        return image;
    }
}
