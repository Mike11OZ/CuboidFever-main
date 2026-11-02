using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Media;

namespace CuboidFever;

public class AudioManager
{
    private Song _currentSong = null!;
    private float _delaySeconds;
    private float _currentDelayTimer;
    
    private bool _isWaitingForDelay;
    private bool _isPlaying;
    private float _volume = 0.8f;
    private readonly Random _random = new();

    // Expose the current track name for UI/Debug overlays
    public string CurrentSongName { get; private set; } = "None";

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = MathHelper.Clamp(value, 0f, 1f);
            MediaPlayer.Volume = _volume;
        }
    }

    /// <summary>
    /// Starts the background music system, initiating the initial random spawn delay (1 to 5 minutes).
    /// </summary>
    public void StartPlaylist()
    {
        _isPlaying = true;
        _isWaitingForDelay = true;
        _currentDelayTimer = 0f;
        // Random delay between 1 and 5 minutes (60 to 300 seconds)
        _delaySeconds = (float)(_random.NextDouble() * 240.0 + 60.0);
        CurrentSongName = "None (Waiting for music...)";
    }

    public void PlayRandomSong()
    {
        try
        {
            string soundsDir = Path.Combine(AppContext.BaseDirectory, "assets", "cuboidfever", "sounds");

            if (!Directory.Exists(soundsDir))
            {
                Console.WriteLine($"[Audio] Sounds directory not found: {soundsDir}");
                CurrentSongName = "None (Directory Not Found)";
                return;
            }

            // Find all .ogg files, excluding "cuboid" (case-insensitive)
            var oggFiles = Directory.GetFiles(soundsDir, "*.ogg")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.Equals(name, "cuboid", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (oggFiles.Length == 0)
            {
                Console.WriteLine("[Audio] No music tracks found (excluding 'cuboid').");
                CurrentSongName = "None (No Tracks Available)";
                return;
            }

            // Pick a random song from the filtered list
            string assetName = oggFiles[_random.Next(oggFiles.Length)];
            string fullPath = Path.Combine(soundsDir, $"{assetName}.ogg");

            _currentSong = Song.FromUri(assetName, new Uri(fullPath, UriKind.Absolute));
            CurrentSongName = $"{assetName}.ogg";
            
            _isPlaying = true;
            _isWaitingForDelay = false;

            MediaPlayer.Volume = _volume;
            MediaPlayer.Play(_currentSong);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] Failed to play random music track: {ex.Message}");
            CurrentSongName = "None (Error)";
        }
    }

    public void Update(GameTime gameTime)
    {
        if (!_isPlaying) return;

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_isWaitingForDelay)
        {
            _currentDelayTimer += dt;
            
            if (_currentDelayTimer >= _delaySeconds)
            {
                PlayRandomSong();
            }
        }
        else
        {
            // If the current song has finished playing, start waiting for the next random 1-5 minute delay
            if (MediaPlayer.State == MediaState.Stopped)
            {
                _isWaitingForDelay = true;
                _currentDelayTimer = 0f;
                // Generate a new random delay between 1 and 5 minutes (60 to 300 seconds)
                _delaySeconds = (float)(_random.NextDouble() * 240.0 + 60.0);
                CurrentSongName = "None (Between Songs)";
            }
        }
    }

    public void Stop()
    {
        _isPlaying = false;
        _isWaitingForDelay = false;
        CurrentSongName = "None (Stopped)";
        MediaPlayer.Stop();
    }
}