using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;

namespace CuboidFever;

public enum BiomeTintKind : byte { None, Grass, Foliage }

public struct BlockInfo
{
    public bool Transparent, Solid, FullBlock, Emissive, Occludes, IsCross;
    public int LightAttenuation;
    public bool IsAir, IsWater, IsGlass, IsLeaves;
    public float TintR, TintG, TintB;
    public BiomeTintKind BiomeTint;

    public int FluidLevel;

    // Doors: a thin slab whose position comes from the block state (see BlockShapes). DoorFacing is 0..3.
    public bool IsDoor, DoorOpen, DoorUpper;
    public byte DoorFacing;

    // Which update rules the block follows when it or a neighbour changes (see BlockTickScheduler)
    public TickKind Tick;
}

// Rendering and block metadata are baked once during initialization.
// Do not mutate registry data or replace the registry while world
// generation or asynchronous chunk meshing is active.
public sealed class BlockRegistry
{
    public static BlockRegistry Instance { get; private set; }

    public readonly BlockInfo[] Info = new BlockInfo[256];
    private readonly TextureRegion[] _faceRegions = new TextureRegion[256 * 6];
    private readonly TextureRegion[] _edgeRegions = new TextureRegion[256];

    public float InsetU { get; private set; }
    public float InsetV { get; private set; }

    public TextureRegion FastRegion(byte id, int face) => _faceRegions[id * 6 + face];

    // The narrow-edge texture of a door (its "edge" texture key); other blocks just return their face 0 texture.
    public TextureRegion EdgeRegion(byte id) => _edgeRegions[id];

    private void Bake()
    {
        var atlas = TextureAtlas.Instance
            ?? throw new InvalidOperationException("Initialize TextureAtlas before BlockRegistry.");

        InsetU = 0.05f / atlas.Width;
        InsetV = 0.05f / atlas.Height;

        foreach (var (id, def) in _definitions)
        {
            Color tint = def.Tint?.ToXnaColor() ?? Color.White;
            string name = def.Name;

            bool isWater = string.Equals(def.Fluid, "water", StringComparison.OrdinalIgnoreCase);
            if (isWater && def.Gravity)
                throw new InvalidDataException($"Block '{name}' cannot be both water and gravity-affected.");

            bool isDoor = def.RenderType.Equals("door", StringComparison.OrdinalIgnoreCase);
            int doorFacing = 0;
            if (isDoor)
            {
                if (!def.State.TryGetValue("facing", out string facingName) ||
                    (doorFacing = BlockShapes.FacingFromName(facingName)) < 0 ||
                    !def.State.TryGetValue("half", out string half) || (half != "lower" && half != "upper") ||
                    !def.State.TryGetValue("open", out string open) || (open != "false" && open != "true"))
                {
                    throw new InvalidDataException(
                        $"Block '{name}' is a door, so it needs the properties facing (north/east/south/west), " +
                        "half (lower/upper) and open (false/true).");
                }
            }

            Info[id] = new BlockInfo
            {
                Transparent = def.Transparent,
                Solid = def.Solid,
                FullBlock = def.FullBlock,
                LightAttenuation = def.LightAttenuation,
                Emissive = def.Emissive,
                Occludes = def.Solid && !def.Transparent && def.RenderType.Equals("cube", StringComparison.OrdinalIgnoreCase),
                IsCross = def.RenderType.Equals("cross", StringComparison.OrdinalIgnoreCase), // <-- Added mapping
                IsDoor = isDoor,
                DoorFacing = (byte)doorFacing,
                DoorOpen = isDoor && def.State["open"] == "true",
                DoorUpper = isDoor && def.State["half"] == "upper",
                IsAir = name.Equals("cuboidfever:air", StringComparison.OrdinalIgnoreCase),
                IsWater = isWater,
                FluidLevel = isWater ? def.FluidLevel : -1,
                Tick = isWater ? TickKind.Water : def.Gravity ? TickKind.Gravity : TickKind.None,
                IsGlass = name.Equals("cuboidfever:glass", StringComparison.OrdinalIgnoreCase),
                IsLeaves = name.Equals("cuboidfever:leaves", StringComparison.OrdinalIgnoreCase),
                TintR = tint.R / 255f, TintG = tint.G / 255f, TintB = tint.B / 255f,
                BiomeTint = (def.BiomeTint ?? "").ToLowerInvariant() switch
                {
                    "grass" => BiomeTintKind.Grass,
                    "foliage" => BiomeTintKind.Foliage,
                    _ => BiomeTintKind.None
                }
            };
            for (int f = 0; f < 6; f++)
                _faceRegions[id * 6 + f] = GetTextureRegion(id, f);

            _edgeRegions[id] = def.Textures != null &&
                               def.Textures.TryGetValue("edge", out string edge) &&
                               !string.IsNullOrWhiteSpace(edge)
                ? TextureAtlas.Instance.GetRegion(edge)
                : _faceRegions[id * 6];
        }

        BuildWaterTable();
    }

    public const int WaterLevels = 9;
    public const int WaterSourceLevel = 0;
    public const int WaterFallingLevel = 8;

    public readonly byte[] WaterByLevel = new byte[WaterLevels];
    public byte WaterSource => WaterByLevel[WaterSourceLevel];

    private void BuildWaterTable()
    {
        var seen = new bool[WaterLevels];

        foreach (var (id, def) in _definitions)
        {
            if (!string.Equals(def.Fluid, "water", StringComparison.OrdinalIgnoreCase)) continue;

            if (def.FluidLevel < 0 || def.FluidLevel >= WaterLevels)
                throw new InvalidDataException(
                    $"Block '{def.Name}' has invalid fluidLevel {def.FluidLevel}. Expected 0 to {WaterLevels}.");

            if (seen[def.FluidLevel])
                throw new InvalidDataException(
                    $"Block '{def.Name}' reuses water fluidLevel {def.FluidLevel}.");

            seen[def.FluidLevel] = true;
            WaterByLevel[def.FluidLevel] = id;
        }

        for (int level = 0; level < WaterLevels; level++)
            if (!seen[level])
                throw new InvalidDataException(
                    $"No water block defines fluidLevel {level}.");
    }
    public static void Initialize(string dataRoot)
    {
        if (Instance != null)
            throw new InvalidOperationException(
                "BlockRegistry has already been initialized.");

        Instance = new BlockRegistry(dataRoot);
    }

    private readonly Dictionary<string, byte> _ids =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<byte, BlockDefinition> _definitions = new();

    // Plain name of a block with states -> its default state, so "cuboidfever:wooden_door" still works
    private readonly Dictionary<string, byte> _defaultStates =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, HashSet<byte>> _tags =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TextureRegion GetTextureRegion(byte id, int face)
    {
        var definition = Get(id);
        var textures = definition.Textures;

        string[] faceKeys = face switch
        {
            0 => new[] { "east", "side", "all" },
            1 => new[] { "west", "side", "all" },
            2 => new[] { "top", "all" },
            3 => new[] { "bottom", "all" },
            4 => new[] { "south", "side", "all" },
            5 => new[] { "north", "side", "all" },
            _ => throw new ArgumentOutOfRangeException(nameof(face))
        };

        string textureName = null;

        foreach (string key in faceKeys)
        {
            if (textures != null &&
                textures.TryGetValue(key, out string value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                textureName = value;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(textureName))
            textureName = definition.Texture;

        if (string.IsNullOrWhiteSpace(textureName))
            textureName = "cuboidfever:dirt";

        return TextureAtlas.Instance.GetRegion(textureName);
    }

    public int GetTile(byte id, int face) =>
        GetTextureRegion(id, face).Index;

    public byte[] HotbarIds { get; private set; } = Array.Empty<byte>();

    private BlockRegistry(string dataRoot)
    {
        string blockDir = Path.Combine(dataRoot, "block");
        string tagDir = Path.Combine(dataRoot, "tags");

        if (!Directory.Exists(blockDir))
            throw new DirectoryNotFoundException(blockDir);

        var definitions = Directory.GetFiles(blockDir, "*.json")
            .Select(file => JsonSerializer.Deserialize<BlockDefinition>(
                File.ReadAllText(file), JsonOptions)
                ?? throw new InvalidDataException($"Invalid block JSON: {file}"))
            .ToList();

        // Blocks with properties become one definition per state (default state first within each block)
        var defaultStates = new List<BlockDefinition>();
        var expanded = new List<BlockDefinition>();
        foreach (var blockDefinition in definitions)
        {
            var states = BlockStates.Expand(blockDefinition);
            expanded.AddRange(states);
            if (states[0].BaseName != null) defaultStates.Add(states[0]);
        }
        definitions = expanded;

        var air = definitions.SingleOrDefault(d =>
            d.Name.Equals("cuboidfever:air",
                StringComparison.OrdinalIgnoreCase));

        if (air == null)
            throw new InvalidDataException(
                "Missing block definition: cuboidfever:air");

        definitions.Remove(air);
        definitions = definitions
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        definitions.Insert(0, air);

        if (definitions.Count > 256)
            throw new InvalidDataException("Maximum of 256 block IDs exceeded.");

        for (int i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];

            if (definition.LightAttenuation < 0 || definition.LightAttenuation > 15)
            {
                throw new InvalidDataException(
                    $"Block '{definition.Name}' has invalid lightAttenuation " +
                    $"{definition.LightAttenuation}. Expected a value from 0 to 15.");
            }

            if (string.IsNullOrWhiteSpace(definition.Name))
                throw new InvalidDataException("A block has no name.");

            byte id = (byte)i;

            if (!_ids.TryAdd(definition.Name, id))
                throw new InvalidDataException(
                    $"Duplicate block name: {definition.Name}");

            _definitions.Add(id, definition);
        }

        foreach (var def in defaultStates)
            _defaultStates[def.BaseName] = _ids[def.Name];

        if (Directory.Exists(tagDir))
        {
            foreach (string file in Directory.GetFiles(tagDir, "*.json"))
            {
                string tagName = Path.GetFileNameWithoutExtension(file);
                var tagFile = JsonSerializer.Deserialize<TagFile>(
                    File.ReadAllText(file), JsonOptions)
                    ?? throw new InvalidDataException($"Invalid tag JSON: {file}");

                var members = new HashSet<byte>();

                foreach (string name in tagFile.Values ?? new())
                {
                    members.Add(GetId(name));
                }

                _tags.Add($"cuboidfever:{tagName}", members);
            }
        }

        string hotbarPath = Path.Combine(dataRoot, "hotbar.json");
        var hotbar = JsonSerializer.Deserialize<HotbarConfig>(
            File.ReadAllText(hotbarPath), JsonOptions)
            ?? throw new InvalidDataException("Invalid hotbar.json");

        HotbarIds = (hotbar.Slots ?? new List<string>())
            .Select(GetId)
            .ToArray();
        Bake();
    }

    public Color GetTint(byte id)
    {
        return Get(id).Tint?.ToXnaColor() ?? Color.White;
    }
    public byte GetId(string name)
    {
        if (TryGetId(name, out byte id))
            return id;

        throw new KeyNotFoundException($"Unknown block: {name}");
    }

    public string GetName(byte id) => Get(id).Name;

    // ---- Block states ----

    // The block's name without its state: cuboidfever:wooden_door for any door state, same as GetName otherwise.
    public string GetBaseName(byte id)
    {
        var def = Get(id);
        return def.BaseName ?? def.Name;
    }

    // The block's default state, e.g. the closed lower north door for any door state; other blocks return themselves.
    public byte GetDefaultState(byte id)
    {
        var def = Get(id);
        return def.BaseName != null && _defaultStates.TryGetValue(def.BaseName, out byte d) ? d : id;
    }

    // The value of one property of the block ("open" -> "true"), or null if the block has no such property.
    public string GetState(byte id, string property) =>
        Get(id).State.TryGetValue(property, out string value) ? value : null;

    // The block with the same base block and state as 'id' except for one property.
    // Throws if the property or value does not exist for the block.
    public byte WithState(byte id, string property, string value)
    {
        var def = Get(id);
        if (def.BaseName == null || !def.State.ContainsKey(property))
            throw new ArgumentException($"Block '{def.Name}' has no property '{property}'.", nameof(property));

        var state = new Dictionary<string, string>(def.State) { [property] = value };
        string name = BlockStates.StateName(def.BaseName, state);

        return _ids.TryGetValue(name, out byte result)
            ? result
            : throw new ArgumentException($"Block '{def.BaseName}' has no state '{property}={value}'.", nameof(value));
    }

    // How many blocks are defined. Runtime IDs 0 .. Count - 1 are in use.
    public int Count => _definitions.Count;

    // Like GetId, but returns false instead of throwing. The save system uses it to spot blocks that a
    // saved world mentions and this build no longer has.
    // A plain name that belongs to a block with states gives its default state.
    public bool TryGetId(string name, out byte id) =>
        _ids.TryGetValue(name, out id) || _defaultStates.TryGetValue(name, out id);

    public BlockDefinition Get(byte id)
    {
        if (_definitions.TryGetValue(id, out var definition))
            return definition;

        throw new KeyNotFoundException($"Unknown block ID: {id}");
    }

    public bool HasTag(byte id, string tag)
    {
        return _tags.TryGetValue(tag, out var members)
            && members.Contains(id);
    }
}