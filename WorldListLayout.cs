using System;

namespace CuboidFever;

// The arithmetic behind the world list screen, kept apart from the drawing code so it can be tested.
// The list shows a few worlds at a time (ButtonMenu has no scrolling, and a long list would run off the screen).
public static class WorldListLayout
{
    public const int PageSize = 4;

    // Always at least 1, so an empty list still has a page to show and nothing divides by zero
    public static int PageCount(int itemCount) =>
        Math.Max(1, (itemCount + PageSize - 1) / PageSize);

    // The slice of the list that belongs on a page. A page number out of range is clamped to the nearest real one.
    public static (int start, int count) PageRange(int itemCount, int page)
    {
        page = Math.Clamp(page, 0, PageCount(itemCount) - 1);

        int start = page * PageSize;
        return (start, Math.Max(0, Math.Min(PageSize, itemCount - start)));
    }

    // A world name short enough for a button: surrounding spaces removed, and "..." in place of the end of a long
    // name. A blank name is shown as "(unnamed)".
    public static string Shorten(string name, int maxLength)
    {
        name = (name ?? string.Empty).Trim();

        if (name.Length == 0) return "(unnamed)";
        if (name.Length <= maxLength) return name;

        return name.Substring(0, Math.Max(0, maxLength - 3)).TrimEnd() + "...";
    }
}
