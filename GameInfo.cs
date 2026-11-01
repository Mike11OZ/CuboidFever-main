namespace CuboidFever;

public static class GameInfo
{
    public const string Title = "CuboidFever";
    public const string Version = "v1.0.0-alpha";
    public const string BuildType = "Dev";

    // A helper property if you want a formatted header string
    public static string FullTitle => $"{Title} | {Version} ({BuildType})";
}