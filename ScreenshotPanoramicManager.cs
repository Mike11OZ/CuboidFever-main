using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

public sealed class ScreenshotPanoramicManager
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly WorldRenderer _renderer;
    private readonly string _screenshotDirectory;

    public ScreenshotPanoramicManager(GraphicsDevice graphicsDevice, WorldRenderer renderer)
    {
        _graphicsDevice = graphicsDevice;
        _renderer = renderer;

        _screenshotDirectory = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(_screenshotDirectory);
    }

    public void CapturePanoramicScreenshot(Vector3 cameraPosition, Vector3 cameraForward, float totalTime)
    {
        try
        {
            // ---- Cubemap settings ----
            const int faceSize = 2048;          // higher = sharper (1024 / 1536 / 2048)
            int finalWidth  = faceSize * 4;     // ~4:1 cylindrical
            int finalHeight = faceSize;         // same vertical resolution as a face

            // Save original projection so we can restore it
            Matrix originalProjection = _renderer.CurrentProjection;

            // 90° square projection for cube faces
            Matrix cubeProjection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.PiOver2, 1f, 0.1f, 500f);

            // Cube face directions (standard order)
            var faces = new (Vector3 forward, Vector3 up, string name)[]
            {
                (Vector3.Right,   Vector3.Up,      "+X"),
                (Vector3.Left,    Vector3.Up,      "-X"),
                (Vector3.Up,      Vector3.Backward, "+Y"),
                (Vector3.Down,    Vector3.Forward,  "-Y"),
                (Vector3.Forward, Vector3.Up,      "+Z"),
                (Vector3.Backward,Vector3.Up,      "-Z"),
            };

            var faceData = new Color[6][];
            var faceTextures = new Texture2D[6];

            using var rt = new RenderTarget2D(
                _graphicsDevice, faceSize, faceSize, false,
                SurfaceFormat.Color, DepthFormat.Depth24, 0, RenderTargetUsage.DiscardContents);

            var previousTargets = _graphicsDevice.GetRenderTargets();

            _renderer.SetProjection(cubeProjection);

            for (int i = 0; i < 6; i++)
            {
                var (forward, up, _) = faces[i];
                Matrix view = Matrix.CreateLookAt(cameraPosition, cameraPosition + forward, up);

                _graphicsDevice.SetRenderTarget(rt);
                _renderer.DrawCustom(view, cameraPosition, totalTime);

                faceData[i] = new Color[faceSize * faceSize];
                rt.GetData(faceData[i]);

                // Optional: keep a Texture2D if you want to debug individual faces later
                faceTextures[i] = new Texture2D(_graphicsDevice, faceSize, faceSize);
                faceTextures[i].SetData(faceData[i]);
            }

            // Restore original state
            _renderer.SetProjection(originalProjection);
            _graphicsDevice.SetRenderTargets(previousTargets);

            // ---- Convert cubemap → cylindrical panorama ----
            var finalData = new Color[finalWidth * finalHeight];

            for (int y = 0; y < finalHeight; y++)
            {
                // Vertical angle matching a typical game FOV (roughly ±40–45°)
                float v = 1f - (float)y / (finalHeight - 1);          // 1 at top, 0 at bottom
                float pitch = (v - 0.5f) * MathHelper.ToRadians(85f); // adjust 85 if you want more/less vertical coverage

                for (int x = 0; x < finalWidth; x++)
                {
                    float yaw = ((float)x / finalWidth) * MathHelper.TwoPi;

                    // Direction vector
                    float cosPitch = MathF.Cos(pitch);
                    Vector3 dir = new Vector3(
                        MathF.Sin(yaw) * cosPitch,
                        MathF.Sin(pitch),
                        MathF.Cos(yaw) * cosPitch
                    );

                    // Sample the cubemap
                    Color c = SampleCubemap(faceData, faceSize, dir);
                    finalData[x + y * finalWidth] = c;
                }
            }

            // Cleanup temporary face textures
            foreach (var t in faceTextures) t?.Dispose();

            using var panoramic = new Texture2D(_graphicsDevice, finalWidth, finalHeight);
            panoramic.SetData(finalData);

            string path = Path.Combine(_screenshotDirectory,
                $"panorama_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");

            using (var fs = File.OpenWrite(path))
                panoramic.SaveAsPng(fs, finalWidth, finalHeight);

            Console.WriteLine($"[ScreenshotPanoramic] Cubemap → cylindrical saved ({finalWidth}×{finalHeight}) → {path}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ScreenshotPanoramic] Failed: {ex}");
        }
    }

    // High-quality cubemap sampling (selects face + bilinear)
    private static Color SampleCubemap(Color[][] faces, int size, Vector3 dir)
    {
        dir.Normalize();

        // Find dominant axis
        Vector3 abs = new Vector3(MathF.Abs(dir.X), MathF.Abs(dir.Y), MathF.Abs(dir.Z));
        int face;
        float sc, tc, ma;

        if (abs.X >= abs.Y && abs.X >= abs.Z)
        {
            // X face
            ma = abs.X;
            if (dir.X > 0) { face = 0; sc = -dir.Z; tc = -dir.Y; }      // +X
            else           { face = 1; sc =  dir.Z; tc = -dir.Y; }      // -X
        }
        else if (abs.Y >= abs.X && abs.Y >= abs.Z)
        {
            // Y face
            ma = abs.Y;
            if (dir.Y > 0) { face = 2; sc =  dir.X; tc =  dir.Z; }      // +Y
            else           { face = 3; sc =  dir.X; tc = -dir.Z; }      // -Y
        }
        else
        {
            // Z face
            ma = abs.Z;
            if (dir.Z > 0) { face = 4; sc =  dir.X; tc = -dir.Y; }      // +Z
            else           { face = 5; sc = -dir.X; tc = -dir.Y; }      // -Z
        }

        // Convert to [0,1] face UVs
        float s = 0.5f * (sc / ma + 1f);
        float t = 0.5f * (tc / ma + 1f);

        // Bilinear sample
        return SampleBilinear(faces[face], size, s, t);
    }

    private static Color SampleBilinear(Color[] data, int size, float u, float v)
    {
        u = Math.Clamp(u, 0f, 1f) * (size - 1);
        v = Math.Clamp(v, 0f, 1f) * (size - 1);

        int x0 = (int)u;
        int y0 = (int)v;
        int x1 = Math.Min(x0 + 1, size - 1);
        int y1 = Math.Min(y0 + 1, size - 1);

        float fx = u - x0;
        float fy = v - y0;

        Color c00 = data[x0 + y0 * size];
        Color c10 = data[x1 + y0 * size];
        Color c01 = data[x0 + y1 * size];
        Color c11 = data[x1 + y1 * size];

        return Color.Lerp(
            Color.Lerp(c00, c10, fx),
            Color.Lerp(c01, c11, fx),
            fy);
    }
}