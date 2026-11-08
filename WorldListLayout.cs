using System;

namespace CuboidFever;

// Where everything on the world list screen goes, and the scrolling arithmetic. Kept apart from the drawing
// code (WorldListScreen) so it can be tested without a graphics device. All values are in screen pixels.
//
//   +--------------------------------+
//   |  header (title)                |
//   +--------------------------------+
//   |  [thumb] World name         || |   <- the viewport: rows scroll inside it, scrollbar on the right
//   |  [thumb] World name         || |
//   |  [thumb] World name            |
//   +--------------------------------+
//   |  footer (Back button)          |
//   +--------------------------------+
public readonly struct ListMetrics
{
    public int Left { get; init; }          // left edge of the rows
    public int Top { get; init; }           // top of the viewport
    public int Width { get; init; }         // width of a row
    public int Height { get; init; }        // height of the viewport
    public int RowHeight { get; init; }
    public int RowGap { get; init; }
    public int ScrollbarWidth { get; init; }
    public int ScrollbarGap { get; init; }  // space between the rows and the scrollbar
    public int HeaderHeight { get; init; }
    public int FooterHeight { get; init; }

    public int Stride => RowHeight + RowGap;
    public int Bottom => Top + Height;
    public int ScrollbarLeft => Left + Width + ScrollbarGap;
}

public static class WorldListLayout
{
    // Sizes as they would be in an 800x480 window; multiplied by the GUI scale when drawn
    public const int DesignRowHeight = 72;
    public const int DesignRowGap = 8;
    public const int DesignRowWidth = 440;
    public const int DesignScrollbarWidth = 8;
    public const int DesignScrollbarGap = 6;
    public const int DesignHeaderHeight = 72;
    public const int DesignFooterHeight = 64;
    public const int ScreenMargin = 20;

    // Thumbnails are 16:9, as tall as the row
    public static int ThumbnailWidth(int rowHeight) => rowHeight * 16 / 9;

    public static ListMetrics Metrics(int screenWidth, int screenHeight, int scale)
    {
        scale = Math.Max(1, scale);

        int scrollbarWidth = DesignScrollbarWidth * scale;
        int scrollbarGap = DesignScrollbarGap * scale;
        int header = DesignHeaderHeight * scale;
        int footer = DesignFooterHeight * scale;

        // Leave room on both sides for the scrollbar, so the rows stay centered on the screen
        int sideSpace = ScreenMargin + scrollbarWidth + scrollbarGap;
        int width = Math.Max(1, Math.Min(DesignRowWidth * scale, screenWidth - 2 * sideSpace));

        return new ListMetrics
        {
            Left = (screenWidth - width) / 2,
            Top = header,
            Width = width,
            Height = Math.Max(1, screenHeight - header - footer),
            RowHeight = DesignRowHeight * scale,
            RowGap = DesignRowGap * scale,
            ScrollbarWidth = scrollbarWidth,
            ScrollbarGap = scrollbarGap,
            HeaderHeight = header,
            FooterHeight = footer
        };
    }

    // =====================  SCROLLING  =====================

    // Total height of all rows with the gaps between them
    public static int ContentHeight(int itemCount, ListMetrics m) =>
        itemCount <= 0 ? 0 : itemCount * m.RowHeight + (itemCount - 1) * m.RowGap;

    // How far the list can be scrolled: 0 when everything fits
    public static int MaxScroll(int itemCount, ListMetrics m) =>
        Math.Max(0, ContentHeight(itemCount, m) - m.Height);

    public static int ClampScroll(int scroll, int itemCount, ListMetrics m) =>
        Math.Clamp(scroll, 0, MaxScroll(itemCount, m));

    // The row under a mouse position, or -1 for the gaps between rows, the scrollbar, and anywhere outside
    // the viewport (a row partly hidden by the viewport edge only counts where it is visible).
    public static int RowAt(int mouseX, int mouseY, int scroll, int itemCount, ListMetrics m)
    {
        if (mouseX < m.Left || mouseX >= m.Left + m.Width) return -1;
        if (mouseY < m.Top || mouseY >= m.Bottom) return -1;

        int inList = mouseY - m.Top + scroll;
        int index = inList / m.Stride;
        int withinRow = inList % m.Stride;

        if (withinRow >= m.RowHeight) return -1; // in the gap below the row
        if (index < 0 || index >= itemCount) return -1;

        return index;
    }

    // The range of rows that are at least partly visible: first index, and how many.
    public static (int first, int count) VisibleRows(int scroll, int itemCount, ListMetrics m)
    {
        if (itemCount <= 0) return (0, 0);

        int first = Math.Clamp(scroll / m.Stride, 0, itemCount - 1);
        int last = Math.Clamp((scroll + m.Height - 1) / m.Stride, 0, itemCount - 1);

        return (first, last - first + 1);
    }

    // =====================  SCROLLBAR  =====================

    // The draggable part of the scrollbar: how far below the top of the track it starts, and how long it is.
    // The track is as tall as the viewport. Length is proportional to how much of the list is visible, but never
    // shorter than minLength. Null when everything fits and there is nothing to scroll.
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

    // The scroll position that puts the middle of the thumb at the given mouse height, for dragging.
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

    // =====================  DELETE BUTTON  =====================

    // The "Delete" button inside a row: right-aligned, vertically centered. Takes the row's rectangle and the
    // GUI scale, and returns x, y, width, height.
    public static (int x, int y, int width, int height) DeleteButtonRect(int rowX, int rowY, int rowWidth, int rowHeight, int scale)
    {
        scale = Math.Max(1, scale);

        int border = 2 * scale;  // the row's border
        int margin = 8 * scale;
        int width = 60 * scale;
        int height = 24 * scale;

        return (rowX + rowWidth - border - margin - width, rowY + (rowHeight - height) / 2, width, height);
    }

    // =====================  TEXT  =====================

    // The name to show for a world: surrounding spaces removed, and "(unnamed)" for a blank name.
    public static string DisplayName(string name)
    {
        name = (name ?? string.Empty).Trim();
        return name.Length == 0 ? "(unnamed)" : name;
    }
}
