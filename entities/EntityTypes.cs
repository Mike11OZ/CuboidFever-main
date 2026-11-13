using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// Every kind of entity the game knows, loaded once at startup.
// To add one: drop name.bbmodel in assets/cuboidfever/models/entity and name.png in
// assets/cuboidfever/textures/entity, then add a Register line below.
public static class EntityTypes
{
    private static readonly Dictionary<string, EntityType> Types = new(StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<string> Ids => Types.Keys;

    // Null if there is no such type (or it failed to load, which was reported at startup)
    public static EntityType Get(string id) => Types.TryGetValue(id, out var t) ? t : null;

    public static void Initialize(GraphicsDevice device)
    {
        Types.Clear();
        Register(device, "scorpion", (type, pos, yaw) => new ScorpionEntity(type, pos, yaw), width: 0.8f, height: 0.7f);
    }

    private static void Register(GraphicsDevice device, string id,
                                 Func<EntityType, Microsoft.Xna.Framework.Vector3, float, Entity> factory,
                                 float width = 0.6f, float height = 0.6f, float scale = 1f)
    {
        string modelPath = Path.Combine(GamePaths.EntityModelRoot, id + ".bbmodel");
        string texturePath = Path.Combine(GamePaths.EntityTextureRoot, id + ".png");

        // A broken entity must not stop the game from starting, so problems are reported and the type is left out
        try
        {
            var texture = TextureLoader.LoadFromFile(device, texturePath);
            if (texture == null) return; // TextureLoader has already said why

            var model = new EntityModel(BbModel.LoadFile(modelPath));

            Types[id] = new EntityType { Id = id, Model = model, Texture = texture, Scale = scale, Width = width, Height = height, Factory = factory };
            Console.WriteLine($"[Entities] Loaded '{id}': {model.Source.Bones.Count} bones, {model.TriangleCount} triangles, " +
                              $"{model.Source.Animations.Count} animations.");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[Entities] Could not load '{id}': {e.Message}");
        }
    }
}
