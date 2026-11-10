using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CuboidFever;

// Block states, after Minecraft's blockstates, but flattened: a block with properties becomes one block ID per
// combination of property values, named like
//
//   cuboidfever:wooden_door[facing=north,half=lower,open=false]
//
// (properties sorted by name). Because each state is a plain block ID, nothing downstream has to know about
// states: the palette saves the full state name, lighting and meshing read the baked BlockInfo, and changing a
// state is just SetBlock with a different ID (BlockRegistry.WithState).
//
// The plain name, cuboidfever:wooden_door, stays usable and means the default state: the first value of every
// property as listed in the JSON.
public static class BlockStates
{
    // How many IDs one block may expand to. The world holds 256 IDs in total, so this is a sanity check.
    public const int MaxStatesPerBlock = 64;

    public static string StateName(string baseName, IReadOnlyDictionary<string, string> state)
    {
        if (state.Count == 0) return baseName;

        // Sorted, so the same state always gets the same name whatever order the caller listed it in
        var parts = state.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}");
        return $"{baseName}[{string.Join(",", parts)}]";
    }

    // Returns the block's states: the block itself if it has no properties, otherwise one copy per combination,
    // default state first. Throws InvalidDataException for definitions that make no sense.
    public static List<BlockDefinition> Expand(BlockDefinition def)
    {
        if (def.Properties == null || def.Properties.Count == 0)
        {
            if (def.Variants is { Count: > 0 })
                throw new InvalidDataException($"Block '{def.Name}' has variants but no properties.");
            return new List<BlockDefinition> { def };
        }

        var names = def.Properties.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

        long total = 1;
        foreach (string name in names)
        {
            var values = def.Properties[name];
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(new[] { '[', ']', ',', '=' }) >= 0)
                throw new InvalidDataException($"Block '{def.Name}' has an invalid property name '{name}'.");
            if (values == null || values.Count == 0)
                throw new InvalidDataException($"Block '{def.Name}' property '{name}' has no values.");
            if (values.Any(v => string.IsNullOrWhiteSpace(v) || v.IndexOfAny(new[] { '[', ']', ',', '=' }) >= 0))
                throw new InvalidDataException($"Block '{def.Name}' property '{name}' has an invalid value.");
            if (values.Distinct(StringComparer.Ordinal).Count() != values.Count)
                throw new InvalidDataException($"Block '{def.Name}' property '{name}' lists a value twice.");

            total *= values.Count;
            if (total > MaxStatesPerBlock)
                throw new InvalidDataException(
                    $"Block '{def.Name}' has more than {MaxStatesPerBlock} states; the world only has 256 block IDs.");
        }

        foreach (var variant in def.Variants ?? new())
        {
            foreach (var (prop, value) in variant.When ?? new())
            {
                if (!def.Properties.TryGetValue(prop, out var allowed) || !allowed.Contains(value))
                    throw new InvalidDataException(
                        $"Block '{def.Name}' has a variant for unknown state '{prop}={value}'.");
            }
        }

        var result = new List<BlockDefinition>((int)total);

        // Odometer over the properties; the last property changes fastest. The first combination is the default.
        var index = new int[names.Count];
        while (true)
        {
            var state = new SortedDictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < names.Count; i++)
                state[names[i]] = def.Properties[names[i]][index[i]];

            result.Add(MakeState(def, state));

            int p = names.Count - 1;
            while (p >= 0 && ++index[p] == def.Properties[names[p]].Count)
                index[p--] = 0;
            if (p < 0) break;
        }

        return result;
    }

    private static BlockDefinition MakeState(BlockDefinition def, SortedDictionary<string, string> state)
    {
        var textures = new Dictionary<string, string>(def.Textures ?? new());

        foreach (var variant in def.Variants ?? new())
        {
            bool matches = (variant.When ?? new()).All(w => state[w.Key] == w.Value);
            if (!matches) continue;

            foreach (var (key, value) in variant.Textures ?? new())
                textures[key] = value;
        }

        return new BlockDefinition
        {
            Name = StateName(def.Name, state),
            BaseName = def.Name,
            State = state,
            Solid = def.Solid,
            Transparent = def.Transparent,
            Replaceable = def.Replaceable,
            LightAttenuation = def.LightAttenuation,
            Emissive = def.Emissive,
            FullBlock = def.FullBlock,
            RenderLayer = def.RenderLayer,
            RenderType = def.RenderType,
            Fluid = def.Fluid,
            FluidLevel = def.FluidLevel,
            Gravity = def.Gravity,
            Emission = def.Emission,
            Tint = def.Tint,
            BiomeTint = def.BiomeTint,
            Texture = def.Texture,
            Textures = textures,
        };
    }
}
