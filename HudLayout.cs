using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace CuboidFever;

// Which point of the screen a HUD element is attached to. The same point of the element is placed on it, so
// BottomRight puts the element's own bottom-right corner on the screen's bottom-right corner, Center centers it, etc.
public enum HudAnchor
{
    TopLeft, Top, TopRight,
    Left, Center, Right,
    BottomLeft, Bottom, BottomRight
}

// Where one HUD element goes and how big it is. Everything is in design pixels (as in an 800x480 window) and is
// multiplied by GuiScale when resolved, like the rest of the HUD.
public sealed class HudElementLayout
{
    public HudAnchor Anchor { get; set; } = HudAnchor.TopLeft;

    // Added to the anchored position: positive X moves right, positive Y moves down.
    // So an element anchored to the right or bottom edge needs a negative offset to sit inside the screen.
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }

    public float Width { get; set; }
    public float Height { get; set; }

    public HudElementLayout Clone() => (HudElementLayout)MemberwiseClone();
}

// The ids of the HUD elements that take their position from the layout. To add one: add an id here, add its default
// to HudLayout.Defaults, and ask for it with HudLayout.Resolve. (data/cuboidfever/hud_layout.json can then move it.)
public static class HudElements
{
    public const string HealthBar = "healthBar";
}

// One place that says where the HUD elements sit. Loaded from data/cuboidfever/hud_layout.json.
//
// The code carries a default for every element, so a missing, damaged or incomplete file never breaks the HUD:
// whatever the file does not say falls back to the default.
public sealed class HudLayout
{
    // Used for anything the json file does not mention.
    private static readonly Dictionary<string, HudElementLayout> Defaults = new()
    {
        [HudElements.HealthBar] = new HudElementLayout
        {
            Anchor = HudAnchor.TopLeft, OffsetX = 16, OffsetY = 16, Width = 200, Height = 14
        },
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,   // the file may carry // comments
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }    // "anchor": "TopLeft"
    };

    // Shape of the json file
    private sealed class LayoutFile
    {
        public Dictionary<string, HudElementLayout> Elements { get; set; }
    }

    private readonly Dictionary<string, HudElementLayout> _elements = new();

    private HudLayout()
    {
        foreach (var pair in Defaults) _elements[pair.Key] = pair.Value.Clone();
    }

    // The built-in layout, with nothing read from disk.
    public static HudLayout CreateDefault() => new();

    // Reads the layout file. If it is missing or cannot be read, the defaults are used and a warning is printed.
    public static HudLayout Load(string path)
    {
        try
        {
            if (File.Exists(path)) return FromJson(File.ReadAllText(path));
            Console.WriteLine($"[Warning] HUD layout file not found ({path}); using the built-in layout.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warning] Could not read the HUD layout ({path}); using the built-in layout. ({ex.Message})");
        }
        return CreateDefault();
    }

    public static HudLayout FromJson(string json)
    {
        var layout = new HudLayout();
        var file = JsonSerializer.Deserialize<LayoutFile>(json, JsonOptions);
        if (file?.Elements == null) return layout;

        foreach (var pair in file.Elements)
        {
            if (pair.Value == null) continue;
            if (!Defaults.ContainsKey(pair.Key))
            {
                Console.WriteLine($"[Warning] hud_layout.json: unknown HUD element '{pair.Key}' ignored.");
                continue;
            }
            layout._elements[pair.Key] = pair.Value;
        }
        return layout;
    }

    // The element's rectangle in screen pixels, for a viewport of the given size and the current GuiScale.
    public Rectangle Resolve(string id, int viewportWidth, int viewportHeight, float guiScale)
    {
        if (!_elements.TryGetValue(id, out var e))
            throw new ArgumentException($"HUD element '{id}' has no layout. Add a default for it in HudLayout.Defaults.", nameof(id));

        int width = GuiScale.Pixels(e.Width * guiScale);
        int height = GuiScale.Pixels(e.Height * guiScale);

        (float fx, float fy) = AnchorFraction(e.Anchor);
        int x = GuiScale.Pixels(viewportWidth * fx - width * fx + e.OffsetX * guiScale);
        int y = GuiScale.Pixels(viewportHeight * fy - height * fy + e.OffsetY * guiScale);

        return new Rectangle(x, y, width, height);
    }

    // How far across (fx) and down (fy) the screen or the element the anchor point is: 0, half, or all the way.
    private static (float fx, float fy) AnchorFraction(HudAnchor anchor) => anchor switch
    {
        HudAnchor.TopLeft => (0f, 0f),
        HudAnchor.Top => (0.5f, 0f),
        HudAnchor.TopRight => (1f, 0f),
        HudAnchor.Left => (0f, 0.5f),
        HudAnchor.Center => (0.5f, 0.5f),
        HudAnchor.Right => (1f, 0.5f),
        HudAnchor.BottomLeft => (0f, 1f),
        HudAnchor.Bottom => (0.5f, 1f),
        HudAnchor.BottomRight => (1f, 1f),
        _ => (0f, 0f)
    };
}
