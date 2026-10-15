using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(OverTheHill.Minimap), "Minimap", "0.2.1", "Minimap contributors")]
[assembly: MelonGame("Funselektor", "over the hill")]

namespace OverTheHill;

public sealed class Minimap : MelonMod
{
    private MelonPreferences_Entry<bool> enabled = null!;
    private MelonPreferences_Entry<float> size = null!;
    private MelonPreferences_Entry<float> range = null!;
    private FusionPlayer vehicle;
    private float nextSearch;
    private float nextError;
    private Texture2D map;
    private Texture2D fog;
    private Rect uv;
    private float heading;
    private GameObject overlay;
    private RectTransform panel;
    private RectTransform viewport;
    private RawImage mapImage;
    private RawImage fogImage;
    private RectTransform marker;
    private readonly NativeMapMirror nativeMap = new();

    public override void OnInitializeMelon()
    {
        var config = MelonPreferences.CreateCategory("Minimap");
        enabled = config.CreateEntry("Enabled", true);
        size = config.CreateEntry("Size", 260f, description: "Map size in pixels at 1080p.");
        range = config.CreateEntry("WorldSpan", 1000f, description: "Width of the visible map in world units.");
        LoggerInstance.Msg("Client-only minimap loaded. F6: toggle. Page Up/Down: zoom. No network patches.");
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        vehicle = null;
        nativeMap.Reset();
        map = fog = null;
        if (overlay) overlay.SetActive(false);
        nextSearch = 0;
    }

    public override void OnUpdate()
    {
        bool show = false;
        try
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.f6Key.wasPressedThisFrame) { enabled.Value = !enabled.Value; MelonPreferences.Save(); }
                if (keyboard.pageUpKey.wasPressedThisFrame) { range.Value = Mathf.Clamp(range.Value / 1.25f, 100f, 10000f); MelonPreferences.Save(); }
                if (keyboard.pageDownKey.wasPressedThisFrame) { range.Value = Mathf.Clamp(range.Value * 1.25f, 100f, 10000f); MelonPreferences.Save(); }
            }
            if (!enabled.Value) return;
            var manager = MapManager.Instance;
            if (!manager || !manager.MapTexture || manager.MapWidth <= 0) return;
            if (!vehicle && Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 2;
                foreach (var candidate in UnityEngine.Object.FindObjectsOfType<FusionPlayer>())
                {
                    if (candidate && candidate.Object && candidate.Object.HasInputAuthority)
                    { vehicle = candidate; break; }
                }
            }
            if (!vehicle || !vehicle.Object || !vehicle.Object.HasInputAuthority) { vehicle = null; return; }
            var fow = manager.FogOfWarManager;
            // Use the game's own coordinate conversion, including its terrain offset.
            if (fow == null || !fow.FogOfWarTexture) return;
            map = manager.MapTexture;
            fog = fow.FogOfWarTexture;
            var grid = fow.WorldToGridPosition(vehicle.transform.position);
            float span = Mathf.Clamp(range.Value / manager.MapWidth, 0.005f, 1f);
            // WorldToGridPosition uses the 1024-unit FowArraySize, NOT the 512px texture.
            uv = new Rect(grid.x / FogOfWarManager.FowArraySize - span / 2,
                grid.y / FogOfWarManager.FowArraySize - span / 2, span, span);
            if (nativeMap.TryGetCrop(vehicle.transform.position, range.Value, out var nativeCrop)) uv = nativeCrop;
            heading = vehicle.transform.eulerAngles.y;
            UpdateCanvas();
            show = true;
        }
        catch (Exception ex)
        {
            if (Time.unscaledTime >= nextError)
            {
                nextError = Time.unscaledTime + 30;
                LoggerInstance.Warning("Minimap waiting for game state: " + ex.Message);
            }
        }
        finally
        {
            if (overlay && overlay.activeSelf != show) overlay.SetActive(show);
        }
    }

    // Native UGUI renders textures; never use IMGUI DrawTextureWithTexCoords on this build.
    private void UpdateCanvas()
    {
        if (!overlay) CreateCanvas();
        float scale = Mathf.Clamp(Screen.height / 1080f, 0.65f, 2f);
        float width = Mathf.Max(80, Mathf.Min(Mathf.Clamp(size.Value, 160, 480) * scale,
            Mathf.Min(Screen.width, Screen.height) - 48));
        panel.sizeDelta = new Vector2(width + 8, width + 8);
        panel.anchoredPosition = new Vector2(-20, -20);
        viewport.sizeDelta = new Vector2(width, width);
        float left = Mathf.Max(0, uv.x), bottom = Mathf.Max(0, uv.y);
        float right = Mathf.Min(1, uv.xMax), top = Mathf.Min(1, uv.yMax);
        bool visible = right > left && top > bottom;
        mapImage.gameObject.SetActive(visible);
        fogImage.gameObject.SetActive(visible);
        if (visible)
        {
            var clipped = new Rect(left, bottom, right - left, top - bottom);
            var position = new Vector2((left - uv.x) / uv.width * width, (bottom - uv.y) / uv.height * width);
            var dimensions = new Vector2(clipped.width / uv.width * width, clipped.height / uv.height * width);
            SetImage(mapImage, map, clipped, position, dimensions);
            SetImage(fogImage, fog, clipped, position, dimensions);
        }
        marker.localRotation = Quaternion.Euler(0, 0, -heading);
        bool mirrored = nativeMap.Update(viewport, mapImage, fogImage, uv, width);
        marker.gameObject.SetActive(!mirrored);
    }

    private static void SetImage(RawImage image, Texture texture, Rect coordinates, Vector2 position, Vector2 dimensions)
    {
        image.texture = texture;
        image.uvRect = coordinates;
        image.rectTransform.anchoredPosition = position;
        image.rectTransform.sizeDelta = dimensions;
    }

    private void CreateCanvas()
    {
        overlay = new GameObject("OverTheHill.Minimap.Canvas");
        overlay.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(overlay);
        var canvas = overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        panel = MakeRect("Minimap border", overlay.transform, new Vector2(1, 1), new Vector2(1, 1));
        var background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(0.06f, 0.08f, 0.09f, 0.94f);
        background.raycastTarget = false;
        viewport = MakeRect("Viewport", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        viewport.gameObject.AddComponent<RectMask2D>();
        mapImage = MakeImage("Map", viewport);
        fogImage = MakeImage("Fog of war", viewport);
        marker = MakeRect("Vehicle heading", viewport, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        AddBar("Heading", marker, new Vector2(0, 2), new Vector2(6, 16));
        AddBar("Nose", marker, new Vector2(0, 6), new Vector2(14, 4));
        LoggerInstance.Msg("Native canvas minimap created (IMGUI texture drawing disabled).");
    }

    private static RectTransform MakeRect(string name, Transform parent, Vector2 anchor, Vector2 pivot)
    {
        var go = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    private static RawImage MakeImage(string name, Transform parent)
    {
        var rect = MakeRect(name, parent, Vector2.zero, Vector2.zero);
        var image = rect.gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        return image;
    }

    private static void AddBar(string name, Transform parent, Vector2 position, Vector2 dimensions)
    {
        var rect = MakeRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.2f, 1f, 0.85f);
        image.raycastTarget = false;
    }

    public override void OnDeinitializeMelon()
    {
        nativeMap.Reset();
        if (overlay) UnityEngine.Object.Destroy(overlay);
    }
}
