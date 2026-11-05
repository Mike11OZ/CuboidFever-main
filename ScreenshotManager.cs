using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

public sealed class ScreenshotManager
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly string _screenshotDirectory;

    public ScreenshotManager(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        
        // Save screenshots to a "screenshots" folder next to the executable
        _screenshotDirectory = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(_screenshotDirectory);
    }

    public void CaptureScreenshot()
    {
        try
        {
            int width = _graphicsDevice.PresentationParameters.BackBufferWidth;
            int height = _graphicsDevice.PresentationParameters.BackBufferHeight;

            // Extract pixel data from the backbuffer
            Color[] backBufferData = new Color[width * height];
            _graphicsDevice.GetBackBufferData(backBufferData);

            // Create a temporary texture to hold the pixel data
            using Texture2D texture = new Texture2D(_graphicsDevice, width, height);
            texture.SetData(backBufferData);

            // Generate a unique timestamped filename
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string fileName = $"screenshot_{timestamp}.png";
            string fullPath = Path.Combine(_screenshotDirectory, fileName);

            // Save to disk as PNG
            using (var stream = File.OpenWrite(fullPath))
            {
                texture.SaveAsPng(stream, width, height);
            }

            Console.WriteLine($"[Screenshot] Saved successfully to: {fullPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Screenshot] Failed to save screenshot: {ex.Message}");
        }
    }
}