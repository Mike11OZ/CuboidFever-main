using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

public class StartupScreen
{
    private Texture2D _logo;
    
    // Hex #14121d converted to RGB (Red: 20, Green: 18, Blue: 29)
    private readonly Color _bgColor = new Color(20, 18, 29);

    public void LoadContent(GraphicsDevice graphics)
    {
        string logoPath = Path.Combine(AppContext.BaseDirectory, "assets", "oz.png");
        
        if (File.Exists(logoPath))
        {
            using var stream = File.OpenRead(logoPath);
            _logo = Texture2D.FromStream(graphics, stream);
        }
        else
        {
            Console.WriteLine($"[Warning] Startup logo not found at: {logoPath}");
        }
    }

    public void Draw(SpriteBatch sprites, GraphicsDevice graphics)
    {
        graphics.Clear(_bgColor);

        if (_logo == null) return;

        // Change from LinearClamp to PointClamp for crisp, pixel-perfect scaling
        sprites.Begin(samplerState: SamplerState.PointClamp);

        int screenWidth = graphics.Viewport.Width;
        int screenHeight = graphics.Viewport.Height;

        float targetSize = screenHeight * 0.5f;
        float scale = targetSize / _logo.Height;

        int targetWidth = (int)(_logo.Width * scale);
        int targetHeight = (int)(_logo.Height * scale);

        int x = (screenWidth - targetWidth) / 2;
        int y = (screenHeight - targetHeight) / 2;

        sprites.Draw(_logo, new Rectangle(x, y, targetWidth, targetHeight), Color.White);

        sprites.End();
    }
}