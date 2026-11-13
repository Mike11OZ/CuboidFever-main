using CuboidFever;
using Microsoft.Xna.Framework;
using Xunit;

namespace CuboidFever.Tests;

public class HudLayoutTests
{
    private static HudLayout WithHealthBar(string element) =>
        HudLayout.FromJson("{ \"elements\": { \"healthBar\": " + element + " } }");

    [Fact]
    public void Default_HealthBarSitsInTheTopLeft()
    {
        var bar = HudLayout.CreateDefault().Resolve(HudElements.HealthBar, 800, 480, 1f);

        Assert.Equal(new Rectangle(16, 16, 200, 14), bar);
    }

    [Fact]
    public void OffsetsAndSizesScaleWithTheGuiScale()
    {
        var bar = HudLayout.CreateDefault().Resolve(HudElements.HealthBar, 1600, 960, 2f);

        Assert.Equal(new Rectangle(32, 32, 400, 28), bar);
    }

    [Fact]
    public void BottomRight_PutsTheElementsOwnCornerOnTheScreenCorner()
    {
        var layout = WithHealthBar("{ \"anchor\": \"BottomRight\", \"offsetX\": -10, \"offsetY\": -10, \"width\": 100, \"height\": 20 }");

        Assert.Equal(new Rectangle(690, 450, 100, 20), layout.Resolve(HudElements.HealthBar, 800, 480, 1f));
    }

    [Fact]
    public void Center_CentersTheElement()
    {
        var layout = WithHealthBar("{ \"anchor\": \"Center\", \"width\": 100, \"height\": 20 }");

        Assert.Equal(new Rectangle(350, 230, 100, 20), layout.Resolve(HudElements.HealthBar, 800, 480, 1f));
    }

    [Fact]
    public void AnchorNamesIgnoreCase_AndCommentsAreAllowed()
    {
        var layout = HudLayout.FromJson("{ // moved\n \"elements\": { \"healthBar\": { \"anchor\": \"bottomleft\", \"width\": 50, \"height\": 10 } } }");

        Assert.Equal(new Rectangle(0, 470, 50, 10), layout.Resolve(HudElements.HealthBar, 800, 480, 1f));
    }

    [Fact]
    public void AnElementTheFileLeavesOut_KeepsItsDefault()
    {
        var layout = HudLayout.FromJson("{ \"elements\": { } }");

        Assert.Equal(new Rectangle(16, 16, 200, 14), layout.Resolve(HudElements.HealthBar, 800, 480, 1f));
    }

    [Fact]
    public void UnknownElementsInTheFile_AreIgnored()
    {
        var layout = HudLayout.FromJson("{ \"elements\": { \"mana\": { \"width\": 5, \"height\": 5 } } }");

        Assert.Throws<System.ArgumentException>(() => layout.Resolve("mana", 800, 480, 1f));
    }

    [Fact]
    public void MissingFile_FallsBackToTheDefaults()
    {
        var layout = HudLayout.Load("this/file/does/not/exist.json");

        Assert.Equal(new Rectangle(16, 16, 200, 14), layout.Resolve(HudElements.HealthBar, 800, 480, 1f));
    }
}
