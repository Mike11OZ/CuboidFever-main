using CuboidFever;
using Microsoft.Xna.Framework;
using Xunit;

namespace CuboidFever.Tests;

public class BlockShapesTests
{
    private const float T = BlockShapes.DoorThickness;

    private static void AssertBox(Vector3 min, Vector3 max, float x0, float x1, float z0, float z1)
    {
        Assert.Equal(x0, min.X, 4);
        Assert.Equal(0f, min.Y, 4);
        Assert.Equal(z0, min.Z, 4);
        Assert.Equal(x1, max.X, 4);
        Assert.Equal(1f, max.Y, 4);
        Assert.Equal(z1, max.Z, 4);
    }

    [Fact]
    public void FacingFollowsTheLookDirection()
    {
        Assert.Equal(0, BlockShapes.FacingFromDirection(0.1f, -1f));  // north is -Z
        Assert.Equal(1, BlockShapes.FacingFromDirection(1f, 0.2f));   // east is +X
        Assert.Equal(2, BlockShapes.FacingFromDirection(-0.3f, 1f));  // south is +Z
        Assert.Equal(3, BlockShapes.FacingFromDirection(-1f, -0.2f)); // west is -X
    }

    [Fact]
    public void ClosedDoorLiesOnTheEdgeBehindThePlayer()
    {
        BlockShapes.DoorBox(0, false, false, out var min, out var max); // looking north, so the door is on the south edge
        AssertBox(min, max, 0f, 1f, 1f - T, 1f);

        BlockShapes.DoorBox(1, false, false, out min, out max);          // looking east: west edge
        AssertBox(min, max, 0f, T, 0f, 1f);

        BlockShapes.DoorBox(2, false, false, out min, out max);          // looking south: north edge
        AssertBox(min, max, 0f, 1f, 0f, T);

        BlockShapes.DoorBox(3, false, false, out min, out max);          // looking west: east edge
        AssertBox(min, max, 1f - T, 1f, 0f, 1f);
    }

    [Fact]
    public void OpenDoorSwingsToThePlayersLeft()
    {
        BlockShapes.DoorBox(0, true, false, out var min, out var max);   // looking north, left is west
        AssertBox(min, max, 0f, T, 0f, 1f);

        BlockShapes.DoorBox(1, true, false, out min, out max);           // looking east, left is north
        AssertBox(min, max, 0f, 1f, 0f, T);

        BlockShapes.DoorBox(2, true, false, out min, out max);           // looking south, left is east
        AssertBox(min, max, 1f - T, 1f, 0f, 1f);

        BlockShapes.DoorBox(3, true, false, out min, out max);           // looking west, left is south
        AssertBox(min, max, 0f, 1f, 1f - T, 1f);
    }

    [Fact]
    public void RightHingedOpenDoorSwingsToThePlayersRight()
    {
        BlockShapes.DoorBox(0, true, true, out var min, out var max);   // looking north, right is east
        AssertBox(min, max, 1f - T, 1f, 0f, 1f);

        BlockShapes.DoorBox(1, true, true, out min, out max);           // looking east, right is south
        AssertBox(min, max, 0f, 1f, 1f - T, 1f);

        BlockShapes.DoorBox(2, true, true, out min, out max);           // looking south, right is west
        AssertBox(min, max, 0f, T, 0f, 1f);

        BlockShapes.DoorBox(3, true, true, out min, out max);           // looking west, right is north
        AssertBox(min, max, 0f, 1f, 0f, T);
    }

    [Fact]
    public void HingeSideDoesNotChangeAClosedDoor()
    {
        for (int facing = 0; facing < 4; facing++)
        {
            BlockShapes.DoorBox(facing, false, false, out var minL, out var maxL);
            BlockShapes.DoorBox(facing, false, true, out var minR, out var maxR);

            Assert.True(minL == minR && maxL == maxR);
        }
    }

    [Fact]
    public void LeftOfPointsTowardThePlayersLeft()
    {
        Assert.Equal((-1, 0), BlockShapes.LeftOf(0)); // looking north, left is west (-X)
        Assert.Equal((0, -1), BlockShapes.LeftOf(1)); // looking east, left is north (-Z)
        Assert.Equal((1, 0), BlockShapes.LeftOf(2));  // looking south, left is east (+X)
        Assert.Equal((0, 1), BlockShapes.LeftOf(3));  // looking west, left is south (+Z)
    }

    [Fact]
    public void WorldBoxIsTheLocalBoxMovedToTheBlock()
    {
        var info = new BlockInfo { IsDoor = true, DoorFacing = 0, DoorOpen = false };

        BlockShapes.DoorBox(in info, 10, 64, -3, out var min, out var max);

        Assert.Equal(10f, min.X, 4);
        Assert.Equal(64f, min.Y, 4);
        Assert.Equal(-3f + 1f - T, min.Z, 4);
        Assert.Equal(11f, max.X, 4);
        Assert.Equal(65f, max.Y, 4);
        Assert.Equal(-2f, max.Z, 4);
    }

    [Fact]
    public void RayHitsTheSlabButPassesThroughTheGap()
    {
        // Closed door facing north: slab at z in [13/16, 1] of block (0, 0, 0)
        BlockShapes.DoorBox(0, false, false, out var min, out var max);

        Assert.True(BlockShapes.RayHitsBox(new Vector3(0.5f, 0.5f, 3f), new Vector3(0, 0, -1), min, max, 5f));
        Assert.False(BlockShapes.RayHitsBox(new Vector3(0.5f, 0.5f, 3f), new Vector3(0, 0, -1), min, max, 1.5f)); // too short
        Assert.False(BlockShapes.RayHitsBox(new Vector3(0.5f, 0.5f, -3f), new Vector3(0, 0, -1), min, max, 5f)); // pointing away
        Assert.False(BlockShapes.RayHitsBox(new Vector3(0.5f, 0.5f, 0.5f), new Vector3(1, 0, 0), min, max, 5f)); // runs alongside it
    }

    [Fact]
    public void RayParallelToAnAxisIsHandled()
    {
        BlockShapes.DoorBox(0, false, false, out var min, out var max);

        // Travelling along the slab's wide face, inside its thickness
        Assert.True(BlockShapes.RayHitsBox(new Vector3(-2f, 0.5f, 0.95f), new Vector3(1, 0, 0), min, max, 5f));
        // Same direction, but outside the thickness
        Assert.False(BlockShapes.RayHitsBox(new Vector3(-2f, 0.5f, 0.5f), new Vector3(1, 0, 0), min, max, 5f));
    }
}
