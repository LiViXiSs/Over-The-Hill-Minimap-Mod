using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace OverTheHill;

internal sealed class PlayerMarkers
{
    private readonly Dictionary<int, Marker> markers = new();
    private float nextScan;
    private sealed class Marker
    {
        public FusionPlayer Player;
        public RectTransform Root;
        public Image[] ColorParts;
        public Image Outline;
    }

    public void Reset()
    {
        foreach (var marker in markers.Values)
            if (marker.Root) UnityEngine.Object.Destroy(marker.Root.gameObject);
        markers.Clear();
        nextScan = 0;
    }

    public void Update(RectTransform parent, NativeMapMirror native, Rect crop, float width, MapManager manager)
    {
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.5f;
            var present = new HashSet<int>();
            foreach (var player in UnityEngine.Object.FindObjectsOfType<FusionPlayer>())
            {
                if (!player || !player.Object) continue;
                int id = player.GetInstanceID();
                present.Add(id);
                if (!markers.TryGetValue(id, out var marker))
                {
                    marker = Create(player, parent);
                    markers.Add(id, marker);
                }
                // Use the game's assignment, rather than assuming connection order or a fixed palette.
                var color = UIManager.GetPlayerColor(player.Object.InputAuthority);
                color.a = 1;
                foreach (var part in marker.ColorParts) part.color = color;
                marker.Outline.color = player.Object.HasInputAuthority ? Color.white : new Color(0, 0, 0, 0.9f);
            }
            foreach (var id in markers.Keys.Where(id => !present.Contains(id)).ToArray())
            {
                if (markers[id].Root) UnityEngine.Object.Destroy(markers[id].Root.gameObject);
                markers.Remove(id);
            }
        }
        foreach (var marker in markers.Values)
        {
            var player = marker.Player;
            bool valid = player && player.Object;
            marker.Root.gameObject.SetActive(valid);
            if (!valid) continue;
            Vector2 position;
            if (native.TryGetCrop(player.transform.position, 1, out var playerCrop)) position = playerCrop.center;
            else
            {
                var grid = manager.FogOfWarManager.WorldToGridPosition(player.transform.position);
                position = grid / FogOfWarManager.FowArraySize;
            }
            marker.Root.anchoredPosition = new Vector2((position.x - crop.x) / crop.width * width,
                (position.y - crop.y) / crop.height * width);
            marker.Root.localRotation = Quaternion.Euler(0, 0, -player.transform.eulerAngles.y);
            marker.Root.SetAsLastSibling();
        }
    }

    private static Marker Create(FusionPlayer player, Transform parent)
    {
        var go = new GameObject("Player marker " + player.GetInstanceID());
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(18, 22);
        var outline = Bar("Outline", rect, Vector2.zero, new Vector2(12, 18));
        var body = Bar("Player color", rect, Vector2.zero, new Vector2(8, 14));
        var nose = Bar("Heading", rect, new Vector2(0, 7), new Vector2(14, 4));
        return new Marker { Player = player, Root = rect, ColorParts = new[] { body, nose }, Outline = outline };
    }

    private static Image Bar(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = go.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }
}
