using CuboidFever;
using Microsoft.Xna.Framework;
using Xunit;

namespace CuboidFever.Tests;

public class BbModelTests
{
    // One 2x2x2 box (west face switched off) inside one group that pivots at (1,1,1), and an animation
    // that turns the group 90 degrees around Y over one second, then holds.
    private const string Json = """
    {
      "name": "t",
      "resolution": { "width": 16, "height": 16 },
      "elements": [
        { "name": "c", "type": "cube", "uuid": "c1", "from": [0,0,0], "to": [2,2,2], "origin": [0,0,0], "rotation": [0,0,0],
          "faces": {
            "north": { "uv": [0,0,2,2], "texture": 0 }, "east":  { "uv": [0,0,2,2], "texture": 0 },
            "south": { "uv": [0,0,2,2], "texture": 0 }, "west":  { "uv": [0,0,2,2], "texture": null },
            "up":    { "uv": [0,0,2,2], "texture": 0 }, "down":  { "uv": [0,0,2,2], "texture": 0 } } }
      ],
      "groups": [ { "name": "arm", "uuid": "g1", "origin": [1,1,1], "rotation": [0,0,0] } ],
      "outliner": [ { "uuid": "g1", "children": [ "c1" ] } ],
      "animations": [
        { "name": "swing", "loop": "hold", "length": 1, "animators": { "g1": { "keyframes": [
          { "channel": "rotation", "data_points": [ { "x": "0", "y": "0",  "z": "0" } ], "time": 0 },
          { "channel": "rotation", "data_points": [ { "x": "0", "y": "90", "z": "0" } ], "time": 1 } ] } } }
      ]
    }
    """;

    [Fact]
    public void ReadsBonesBoxesFacesAndAnimations()
    {
        var model = BbModel.Parse(Json);

        Assert.Equal(16, model.TextureWidth);
        var bone = Assert.Single(model.Bones);
        Assert.Equal("arm", bone.Name);
        Assert.Equal(new Vector3(1, 1, 1), bone.Origin);

        var cube = Assert.Single(bone.Cubes);
        Assert.True(cube.Faces[(int)BbFaceDir.North].Visible);
        Assert.False(cube.Faces[(int)BbFaceDir.West].Visible); // "texture": null means the face is off

        Assert.True(model.Animations.ContainsKey("SWING")); // names are not case sensitive
        Assert.Equal(BbLoop.Hold, model.Animations["swing"].Loop);
    }

    [Fact]
    public void OnlyVisibleFacesBecomeTriangles()
    {
        var mesh = new EntityModel(BbModel.Parse(Json));

        Assert.Equal(5 * 4, mesh.VertexCount);
        Assert.Equal(5 * 2, mesh.TriangleCount);
    }

    [Fact]
    public void KeyframesBlendInAStraightLineAndHoldAtTheEnds()
    {
        var track = BbModel.Parse(Json).Animations["swing"].Tracks[0];

        Assert.Equal(45f, BbTrack.Sample(track.Rotation, 0.5f).Y, 3);
        Assert.Equal(0f, BbTrack.Sample(track.Rotation, -1f).Y, 3);
        Assert.Equal(90f, BbTrack.Sample(track.Rotation, 5f).Y, 3);
    }

    [Fact]
    public void LoopingAnimationsWrapAndOthersStop()
    {
        var looping = new BbAnimation { Length = 1f, Loop = BbLoop.Loop };
        var once = new BbAnimation { Length = 1f, Loop = BbLoop.Once };

        Assert.Equal(0.25f, looping.Wrap(1.25f), 3);
        Assert.Equal(1f, once.Wrap(1.25f), 3);
        Assert.True(once.IsFinished(1.25f));
        Assert.False(looping.IsFinished(1.25f));
    }

    [Fact]
    public void RestingPoseLeavesTheBoxWhereItIs()
    {
        var mesh = new EntityModel(BbModel.Parse(Json));
        var v = mesh.BuildPose(null, 0f);

        // The first face built is north; its first corner is the top-left one as seen from outside: (xmax, ymax, zmin)
        Assert.Equal(2f, v[0].Position.X, 3);
        Assert.Equal(2f, v[0].Position.Y, 3);
        Assert.Equal(0f, v[0].Position.Z, 3);
    }

    [Fact]
    public void AnimationTurnsTheBoneAroundItsPivot()
    {
        var mesh = new EntityModel(BbModel.Parse(Json));
        var v = mesh.BuildPose("swing", 1f);

        // (2,2,0) turned 90 degrees around Y about the pivot (1,1,1) lands on (0,2,0)
        Assert.Equal(0f, v[0].Position.X, 3);
        Assert.Equal(2f, v[0].Position.Y, 3);
        Assert.Equal(0f, v[0].Position.Z, 3);
    }

    [Fact]
    public void EveryTriangleIsWoundToFaceOutward()
    {
        var mesh = new EntityModel(BbModel.Parse(Json));
        var v = mesh.BuildPose(null, 0f);
        var centre = new Vector3(1, 1, 1);
        var idx = mesh.Indices;

        for (int t = 0; t < mesh.TriangleCount; t++)
        {
            Vector3 a = v[idx[t * 3]].Position, b = v[idx[t * 3 + 1]].Position, c = v[idx[t * 3 + 2]].Position;
            Vector3 normal = Vector3.Cross(b - a, c - a);
            Vector3 outward = (a + b + c) / 3f - centre;

            // The game culls counter-clockwise triangles, so the visible ones are clockwise from outside:
            // their right-hand normal points INTO the box.
            Assert.True(Vector3.Dot(normal, outward) < 0f, $"triangle {t} faces inward");
        }
    }
}
