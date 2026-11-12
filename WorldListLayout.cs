using System;

namespace CuboidFever;

// Where everything on the world list screen goes, and the scrolling arithmetic. Kept apart from the drawing
// code (WorldListScreen) so it can be tested without a graphics device. All values are in screen pixels.
public readonly struct ListMetrics
{
    public int Left { get; init; }
    public int Top { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int RowHeight { get; init; }
    public int RowGap { get; init; }
    public int ScrollbarWidth { get; init; }
    public int ScrollbarGap { get; init; }
    public int HeaderHeight { get; init; }
    public int FooterHeight { get; init; }

    public int Stride => RowHeight + RowGap;
    public int Bottom => Top + Height;
    public int ScrollbarLeft => Left + Width + ScrollbarGap;
}

public static class WorldListLayout
{
    public const int DesignRowHeight = 72;
    public const int DesignRowGap = 8;
    public const int DesignRowWidth = 440;
    public const int DesignScrollbarWidth = 8;
    public const int DesignScrollbarGap = 6;
    public const int DesignHeaderHeight = 72;
    public const int DesignFooterHeight = 64;
    public const int ScreenMargin = 20;

    public static int ThumbnailWidth(int rowHeight) => rowHeight * 16 / 9;

    // Scale remains fractional until each screen-space dimension is rounded to a pixel.
    public static ListMetrics Metrics(int screenWidth, int screenHeight, float scale)
    {
        scale = Math.Max(1f, scale);

        int scrollbarWidth = GuiScale.Pixels(DesignScrollbarWidth * scale);
        int scrollbarGap = GuiScale.Pixels(DesignScrollbarGap * scale);
        int header = GuiScale.Pixels(DesignHeaderHeight * scale);
        int footer = GuiScale.Pixels(DesignFooterHeight * scale);

        int sideSpace = ScreenMargin + scrollbarWidth + scrollbarGap;
        int width = Math.Max(1, Math.Min(GuiScale.Pixels(DesignRowWidth * scale), screenWidth - 2 * sideSpace));

        return new ListMetrics
        {
            Left = (screenWidth - width) / 2,
            Top = header,
            Width = width,
            Height = Math.Max(1, screenHeight - header - footer),
            RowHeight = Math.Max(1, GuiScale.Pixels(DesignRowHeight * scale)),
            RowGap = Math.Max(0, GuiScale.Pixels(DesignRowGap * scale)),
            ScrollbarWidth = scrollbarWidth,
            ScrollbarGap = scrollbarGap,
            HeaderHeight = header,
            FooterHeight = footer
        };
    }

    public static int ContentHeight(int itemCount, ListMetrics m) =>
        itemCount <= 0 ? 0 : itemCount * m.RowHeight + (itemCount - 1) * m.RowGap;

    public static int MaxScroll(int itemCount, ListMetrics m) =>
        Math.Max(0, ContentHeight(itemCount, m) - m.Height);

    public static int ClampScroll(int scroll, int itemCount, ListMetrics m) =>
        Math.Clamp(scroll, 0, MaxScroll(itemCount, m));

    public static int RowAt(int mouseX, int mouseY, int scroll, int itemCount, ListMetrics m)
    {
        if (mouseX < m.Left || mouseX >= m.Left + m.Width) return -1;
        if (mouseY < m.Top || mouseY >= m.Bottom) return -1;

        int inList = mouseY - m.Top + scroll;
        int index = inList / m.Stride;
        int withinRow = inList % m.Stride;

        if (withinRow >= m.RowHeight) return -1;
        if (index < 0 || index >= itemCount) return -1;

        return index;
    }

    public static (int first, int count) VisibleRows(int scroll, int itemCount, ListMetrics m)
    {
        if (itemCount <= 0) return (0, 0);

        int first = Math.Clamp(scroll / m.Stride, 0, itemCount - 1);
        int last = Math.Clamp((scroll + m.Height - 1) / m.Stride, 0, itemCount - 1);

        return (first, last - first + 1);
    }

    public static (int offset, int length)? Thumb(int itemCount, int scroll, ListMetrics m, int minLength)
    {
        int maxScroll = MaxScroll(itemCount, m);
        if (maxScroll <= 0) return null;

        int content = ContentHeight(itemCount, m);
        int length = (int)Math.Clamp((long)m.Height * m.Height / content, Math.Min(minLength, m.Height), m.Height);

        int travel = m.Height - length;
        int offset = (int)((long)Math.Clamp(scroll, 0, maxScroll) * travel / maxScroll);

        return (offset, length);
    }

    public static int ScrollForThumbCenter(int mouseY, int itemCount, ListMetrics m, int minLength)
    {
        var thumb = Thumb(itemCount, 0, m, minLength);
        if (thumb == null) return 0;

        int length = thumb.Value.length;
        int travel = m.Height - length;
        if (travel <= 0) return 0;

        int thumbTop = Math.Clamp(mouseY - m.Top - length / 2, 0, travel);
        return (int)((long)thumbTop * MaxScroll(itemCount, m) / travel);
    }

    public static (int x, int y, int width, int height) DeleteButtonRect(
        int rowX, int rowY, int rowWidth, int rowHeight, float scale, int labelWidth = 0)
    {
        scale = Math.Max(1f, scale);

        int border = GuiScale.Pixels(2 * scale);
        int margin = GuiScale.Pixels(8 * scale);
        int width = Math.Max(GuiScale.Pixels(60 * scale), labelWidth + GuiScale.Pixels(16 * scale));
        int height = GuiScale.Pixels(24 * scale);

        return (rowX + rowWidth - border - margin - width, rowY + (rowHeight - height) / 2, width, height);
    }

    // The largest rectangle with a picture's shape that fits, centered, inside a box. The picture is shrunk or
    // enlarged evenly and never stretched, so a square picture in a wide box gets empty space either side.
    // Used where the picture is not known to be the box's shape (a resource pack's pack.png).
    public static (int x, int y, int width, int height) FitInside(
        int boxX, int boxY, int boxWidth, int boxHeight, int pictureWidth, int pictureHeight)
    {
        if (boxWidth <= 0 || boxHeight <= 0)
            return (boxX, boxY, Math.Max(0, boxWidth), Math.Max(0, boxHeight));

        // A picture with no size cannot be fitted; fill the box rather than divide by zero
        if (pictureWidth <= 0 || pictureHeight <= 0)
            return (boxX, boxY, boxWidth, boxHeight);

        double fit = Math.Min(boxWidth / (double)pictureWidth, boxHeight / (double)pictureHeight);
        int width = Math.Clamp((int)Math.Round(pictureWidth * fit), 1, boxWidth);
        int height = Math.Clamp((int)Math.Round(pictureHeight * fit), 1, boxHeight);

        return (boxX + (boxWidth - width) / 2, boxY + (boxHeight - height) / 2, width, height);
    }

    public static string DisplayName(string name)
    {
        name = (name ?? string.Empty).Trim();
        return name.Length == 0 ? "(unnamed)" : name;
    }
}
