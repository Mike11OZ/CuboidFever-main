using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;

namespace CuboidFever;

// Reads a Blockbench project (.bbmodel) into plain data: bones, boxes and animations.
// It knows nothing about the GPU, so it can be tested without a graphics device.
//
// Units are Blockbench's: 1 unit = 1 pixel = 1/16 of a block. +Y is up and the model's front is -Z,
// which is the same orientation the game uses, so no axis flipping is needed.
//
// Supported: boxes with per-face UVs, groups (bones) nested to any depth, box rotation and inflate,
// rotation and position keyframes. Not supported (skipped, with a warning where it would matter):
// meshes, scale keyframes, face UV rotation, Molang expressions in keyframes (read as 0).

public enum BbFaceDir { North, East, South, West, Up, Down }

// One face's rectangle in the texture, in pixels. (U1,V1) is the corner at the top-left of the face as seen
// from outside; (U2,V2) is the bottom-right. U2 < U1 or V2 < V1 means the picture is mirrored on that face.
public struct BbFace
{
    public bool Visible;
    public float U1, V1, U2, V2;
}

public sealed class BbCube
{
    public string Name = "";
    public Vector3 From, To;          // opposite corners, with the cube's inflate already applied
    public Vector3 Origin;            // the point the cube's own rotation turns around
    public Vector3 RotationDeg;       // degrees
    public readonly BbFace[] Faces = new BbFace[6]; // indexed by BbFaceDir
}

// A group in Blockbench. Moving or rotating it moves everything inside it, including child bones.
public sealed class BbBone
{
    public string Name = "";
    public string Uuid = "";
    public int Parent = -1;           // index into BbModel.Bones, or -1 for a top-level bone
    public Vector3 Origin;            // pivot the bone's rotation turns around (model units)
    public Vector3 RotationDeg;       // the bone's resting rotation, in degrees
    public readonly List<BbCube> Cubes = new();
}

public readonly record struct BbKey(float Time, Vector3 Value);

// Keyframes for one bone in one animation. Values are ADDED to the bone's resting rotation / position.
public sealed class BbTrack
{
    public readonly List<BbKey> Rotation = new(); // degrees
    public readonly List<BbKey> Position = new(); // model units

    // Straight-line blend between the two keys around 'time'. Before the first key it holds the first value,
    // after the last it holds the last one. No keys at all means no change.
    public static Vector3 Sample(List<BbKey> keys, float time)
    {
        if (keys.Count == 0) return Vector3.Zero;
        if (time <= keys[0].Time) return keys[0].Value;

        for (int i = 1; i < keys.Count; i++)
        {
            if (time > keys[i].Time) continue;

            BbKey a = keys[i - 1], b = keys[i];
            float span = b.Time - a.Time;
            float f = span <= 0f ? 1f : (time - a.Time) / span;
            return Vector3.Lerp(a.Value, b.Value, f);
        }

        return keys[^1].Value;
    }
}

public enum BbLoop { Loop, Once, Hold }

public sealed class BbAnimation
{
    public string Name = "";
    public float Length;              // seconds
    public BbLoop Loop;
    public readonly Dictionary<int, BbTrack> Tracks = new(); // key: index into BbModel.Bones

    // Turns an ever-growing clock into a time inside the animation: looping ones wrap around,
    // the others stop on their last frame.
    public float Wrap(float time)
    {
        if (Length <= 0f) return 0f;
        if (Loop == BbLoop.Loop)
        {
            float t = time % Length;
            return t < 0f ? t + Length : t;
        }
        return MathHelper.Clamp(time, 0f, Length);
    }

    // True once a non-looping animation has played to its end.
    public bool IsFinished(float time) => Loop != BbLoop.Loop && time >= Length;
}

public sealed class BbModel
{
    public string Name = "";
    public int TextureWidth = 64, TextureHeight = 64;   // the size the face UVs are written against

    // Parents always come before their children, so a pose can be worked out in one pass from the top.
    public readonly List<BbBone> Bones = new();

    public readonly Dictionary<string, BbAnimation> Animations = new(StringComparer.OrdinalIgnoreCase);

    private int _looseRoot = -1; // bone made for boxes that sit outside every group, if there are any

    private static readonly string[] FaceNames = { "north", "east", "south", "west", "up", "down" };

    public static BbModel LoadFile(string path) => Parse(File.ReadAllText(path));

    public static BbModel Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        var model = new BbModel { Name = Str(root, "name", "model") };

        if (root.TryGetProperty("resolution", out var res))
        {
            model.TextureWidth = (int)NumProp(res, "width", 64);
            model.TextureHeight = (int)NumProp(res, "height", 64);
        }

        // Boxes by id. The outliner (the tree of groups) refers to them by id.
        var cubes = new Dictionary<string, BbCube>();
        if (root.TryGetProperty("elements", out var elements))
        {
            foreach (var el in elements.EnumerateArray())
            {
                string type = Str(el, "type", "cube");
                if (type != "cube")
                {
                    Console.WriteLine($"[Model] '{model.Name}': skipped '{Str(el, "name", "?")}' because {type} elements are not supported.");
                    continue;
                }
                if (el.TryGetProperty("export", out var exp) && exp.ValueKind == JsonValueKind.False) continue;

                cubes[Str(el, "uuid", "")] = ReadCube(model, el);
            }
        }

        // Groups by id. Newer files keep each group's pivot and rotation here and only the tree in the outliner.
        var groups = new Dictionary<string, JsonElement>();
        if (root.TryGetProperty("groups", out var groupList) && groupList.ValueKind == JsonValueKind.Array)
        {
            foreach (var g in groupList.EnumerateArray())
                if (g.ValueKind == JsonValueKind.Object)
                    groups[Str(g, "uuid", "")] = g;
        }

        if (root.TryGetProperty("outliner", out var outliner))
            model.ReadOutliner(outliner, -1, groups, cubes);

        if (root.TryGetProperty("animations", out var anims))
            model.ReadAnimations(anims);

        return model;
    }

    // =====================  BOXES  =====================

    private static BbCube ReadCube(BbModel model, JsonElement el)
    {
        var cube = new BbCube { Name = Str(el, "name", "cube") };

        float inflate = NumProp(el, "inflate", 0f);
        cube.From = Vec3Prop(el, "from") - new Vector3(inflate);
        cube.To = Vec3Prop(el, "to") + new Vector3(inflate);
        cube.Origin = Vec3Prop(el, "origin");
        cube.RotationDeg = Vec3Prop(el, "rotation");

        if (!el.TryGetProperty("faces", out var faces))
            throw new InvalidDataException($"Box '{cube.Name}' has no faces. Re-save the model from Blockbench.");

        for (int i = 0; i < FaceNames.Length; i++)
        {
            if (!faces.TryGetProperty(FaceNames[i], out var f)) continue;
            if (!f.TryGetProperty("uv", out var uv) || uv.ValueKind != JsonValueKind.Array || uv.GetArrayLength() < 4) continue;

            // "texture": null (or false) is how Blockbench marks a face that is switched off
            if (f.TryGetProperty("texture", out var tex) &&
                (tex.ValueKind == JsonValueKind.Null || tex.ValueKind == JsonValueKind.False)) continue;

            if (NumProp(f, "rotation", 0f) != 0f)
                Console.WriteLine($"[Model] '{model.Name}': face rotation on '{cube.Name}' is ignored.");

            cube.Faces[i] = new BbFace
            {
                Visible = true,
                U1 = Num(uv[0]), V1 = Num(uv[1]), U2 = Num(uv[2]), V2 = Num(uv[3])
            };
        }

        return cube;
    }

    // =====================  BONES  =====================

    private void ReadOutliner(JsonElement list, int parent, Dictionary<string, JsonElement> groups,
                              Dictionary<string, BbCube> cubes)
    {
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                // A box. Not found means it was skipped above (a mesh, or not exported).
                if (!cubes.TryGetValue(item.GetString() ?? "", out var cube)) continue;

                int owner = parent >= 0 ? parent : LooseRoot();
                Bones[owner].Cubes.Add(cube);
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object) continue;

            string uuid = Str(item, "uuid", "");
            JsonElement data = item;
            if (!item.TryGetProperty("name", out _) && groups.TryGetValue(uuid, out var g)) data = g;

            var bone = new BbBone
            {
                Name = Str(data, "name", "bone"),
                Uuid = uuid,
                Parent = parent,
                Origin = Vec3Prop(data, "origin"),
                RotationDeg = Vec3Prop(data, "rotation")
            };
            Bones.Add(bone);
            int index = Bones.Count - 1;

            if (item.TryGetProperty("children", out var kids) && kids.ValueKind == JsonValueKind.Array)
                ReadOutliner(kids, index, groups, cubes);
        }
    }

    private int LooseRoot()
    {
        if (_looseRoot < 0)
        {
            Bones.Add(new BbBone { Name = "_root" });
            _looseRoot = Bones.Count - 1;
        }
        return _looseRoot;
    }

    // =====================  ANIMATIONS  =====================

    private void ReadAnimations(JsonElement anims)
    {
        var boneByUuid = new Dictionary<string, int>();
        for (int i = 0; i < Bones.Count; i++)
            if (Bones[i].Uuid.Length > 0) boneByUuid[Bones[i].Uuid] = i;

        foreach (var a in anims.EnumerateArray())
        {
            var anim = new BbAnimation
            {
                Name = Str(a, "name", "animation"),
                Length = NumProp(a, "length", 0f),
                Loop = Str(a, "loop", "once") switch
                {
                    "loop" => BbLoop.Loop,
                    "hold" => BbLoop.Hold,
                    _ => BbLoop.Once
                }
            };

            if (a.TryGetProperty("animators", out var animators) && animators.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in animators.EnumerateObject())
                {
                    // Other animators (the "effects" track, for one) aren't bones
                    if (!boneByUuid.TryGetValue(prop.Name, out int bone)) continue;
                    if (!prop.Value.TryGetProperty("keyframes", out var keyframes)) continue;

                    var track = new BbTrack();
                    foreach (var kf in keyframes.EnumerateArray())
                    {
                        if (!kf.TryGetProperty("data_points", out var pts) || pts.GetArrayLength() == 0) continue;

                        var key = new BbKey(NumProp(kf, "time", 0f), new Vector3(
                            NumProp(pts[0], "x", 0f), NumProp(pts[0], "y", 0f), NumProp(pts[0], "z", 0f)));

                        switch (Str(kf, "channel", ""))
                        {
                            case "rotation": track.Rotation.Add(key); break;
                            case "position": track.Position.Add(key); break;
                            // "scale" is not supported
                        }
                    }

                    track.Rotation.Sort((x, y) => x.Time.CompareTo(y.Time));
                    track.Position.Sort((x, y) => x.Time.CompareTo(y.Time));

                    if (track.Rotation.Count > 0 || track.Position.Count > 0)
                        anim.Tracks[bone] = track;
                }
            }

            Animations[anim.Name] = anim;
        }
    }

    // =====================  JSON HELPERS  =====================

    // Blockbench writes numbers either as numbers or as text ("5"). Anything else (a Molang formula) reads as 0.
    private static float Num(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Number => e.GetSingle(),
        JsonValueKind.String => float.TryParse(e.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f,
        _ => 0f
    };

    private static float NumProp(JsonElement e, string name, float fallback) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p) ? Num(p) : fallback;

    private static Vector3 Vec3Prop(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Array || p.GetArrayLength() < 3)
            return Vector3.Zero;
        return new Vector3(Num(p[0]), Num(p[1]), Num(p[2]));
    }

    private static string Str(JsonElement e, string name, string fallback) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? fallback : fallback;
}
