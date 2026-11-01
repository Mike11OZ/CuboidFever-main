using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// Solid-block vertex with the two light sources kept apart so the sky can be dimmed at draw time.
//   Color = block light (lamps) * face shade * tint * AO; also carries the alpha used for alpha testing.
//   Sky   = sky light             * face shade * tint * AO; multiplied by the day/night brightness in the shader.
// The shader outputs max(Color.rgb, Sky.rgb * SkyBrightness), which at full brightness is exactly what
// the mesher used to bake into a single vertex color.
public struct VertexPositionColorTextureSky : IVertexType
{
    public Vector3 Position;
    public Color Color;
    public Vector2 TextureCoordinate;
    public Color Sky;

    public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
        new VertexElement(0,  VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Color,   VertexElementUsage.Color, 0),
        new VertexElement(16, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(24, VertexElementFormat.Color,   VertexElementUsage.Color, 1)
    );

    public VertexPositionColorTextureSky(Vector3 position, Color color, Vector2 uv, Color sky)
    {
        Position = position;
        Color = color;
        TextureCoordinate = uv;
        Sky = sky;
    }

    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}