using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace CuboidFever;

public class BlockDefinition
{
    public string Name { get; set; } = "";
    public bool Solid { get; set; } = true;
    public bool Transparent { get; set; }
    public bool Replaceable { get; set; }
    public int LightAttenuation { get; set; } = 0;
    public bool Emissive { get; set; }

    // Whether this block occupies an entire voxel.
    public bool FullBlock { get; set; } = true;

    public string RenderLayer { get; set; } = "opaque";
    public string RenderType { get; set; } = "cube";

    public string Fluid { get; set; }
    public int FluidLevel { get; set; }

    // Falls when the block below is not solid (sand). See GravityBlocks. A block can be a fluid or have gravity, not both.
    public bool Gravity { get; set; }

    public EmissionData Emission { get; set; } = new();
    public ColorData Tint { get; set; } = new ColorData(255, 255, 255);

    // "grass" or "foliage": also multiply the tint by the local climate's colour
    public string BiomeTint { get; set; } = "";

    public string Texture { get; set; }
    public Dictionary<string, string> Textures { get; set; } = new();

    // ---- Block states ----
    // A block that lists properties (e.g. "open": ["false", "true"]) is expanded when the registry loads into one
    // runtime block per combination, so every state is an ordinary one-byte block ID. Chunks, saves, lighting and
    // the palette need no changes. See BlockStates.
    public Dictionary<string, List<string>> Properties { get; set; } = new();

    // Overrides applied to the states they match, in file order (later entries win). Today that is textures only.
    public List<BlockVariant> Variants { get; set; } = new();

    // Filled in by BlockStates.Expand for each generated state; empty for blocks without properties.
    [JsonIgnore] public string BaseName { get; set; }
    [JsonIgnore] public SortedDictionary<string, string> State { get; set; } = new(StringComparer.Ordinal);
}

// "when" lists property values (all must match); "textures" replaces those texture keys for the matching states.
public class BlockVariant
{
    public Dictionary<string, string> When { get; set; } = new();
    public Dictionary<string, string> Textures { get; set; } = new();
}

public class EmissionData
{
    public int R { get; set; }
    public int G { get; set; }
    public int B { get; set; }
}

[JsonConverter(typeof(ColorDataConverter))]
public class ColorData
{
    public int R { get; set; } = 255;
    public int G { get; set; } = 255;
    public int B { get; set; } = 255;

    public ColorData() { }
    public ColorData(int r, int g, int b) => (R, G, B) = (r, g, b);

    public Color ToXnaColor() => new Color(R, G, B);
}

public class ColorDataConverter : JsonConverter<ColorData>
{
    public override ColorData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            int r = 255, g = 255, b = 255;
            if (reader.Read() && reader.TokenType == JsonTokenType.Number) r = reader.GetInt32();
            if (reader.Read() && reader.TokenType == JsonTokenType.Number) g = reader.GetInt32();
            if (reader.Read() && reader.TokenType == JsonTokenType.Number) b = reader.GetInt32();
            while (reader.TokenType != JsonTokenType.EndArray)
            {
                reader.Read();
            }
            return new ColorData(r, g, b);
        }
        else if (reader.TokenType == JsonTokenType.StartObject)
        {
            int r = 255, g = 255, b = 255;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) break;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string propName = reader.GetString();
                    reader.Read();
                    if (propName.Equals("r", StringComparison.OrdinalIgnoreCase)) r = reader.GetInt32();
                    else if (propName.Equals("g", StringComparison.OrdinalIgnoreCase)) g = reader.GetInt32();
                    else if (propName.Equals("b", StringComparison.OrdinalIgnoreCase)) b = reader.GetInt32();
                }
            }
            return new ColorData(r, g, b);
        }
        return new ColorData();
    }

    public override void Write(Utf8JsonWriter writer, ColorData value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("r", value.R);
        writer.WriteNumber("g", value.G);
        writer.WriteNumber("b", value.B);
        writer.WriteEndObject();
    }
}

public class TagFile
{
    public List<string> Values { get; set; }
}

public class HotbarConfig
{
    public List<string> Slots { get; set; }
}