using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

public struct VertexPositionColorTextureWave : IVertexType
{
    public Vector3 Position;
    public Color Color;
    public Vector2 TextureCoordinate;
    public float IsTopFace; // 1.0f if top face, 0.0f otherwise

    public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Color, VertexElementUsage.Color, 0),
        new VertexElement(16, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(24, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 1)
    );

    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}