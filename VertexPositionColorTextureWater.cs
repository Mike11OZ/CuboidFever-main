using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// Water vertex: the usual position/color/uv plus the two values the water shader needs
// per vertex. Replaces the old, unused VertexPositionColorTextureWave.
public struct VertexPositionColorTextureWater : IVertexType
{
    public Vector3 Position;
    public Color Color;
    public Vector2 TextureCoordinate;
    public Vector2 Water; // x = wave weight (0 = shore, 1 = free, 2 = down-only), y = face index 0..5

    public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
        new VertexElement(0,  VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Color,   VertexElementUsage.Color, 0),
        new VertexElement(16, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 1)
    );

    public VertexPositionColorTextureWater(
        Vector3 position, Color color, Vector2 uv, float waveWeight, float face)
    {
        Position = position;
        Color = color;
        TextureCoordinate = uv;
        Water = new Vector2(waveWeight, face);
    }

    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}
