using System;
using System.IO;
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

    public void PlayMusic(string assetName, float delaySeconds)
    {
        try
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, "assets", "cuboidfever", "sounds", $"{assetName}.ogg");

            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"[Audio] File not found: {fullPath}");
                CurrentSongName = "None (File Not Found)";
                return;
            }

            _currentSong = Song.FromUri(assetName, new Uri(fullPath, UriKind.Absolute));
            CurrentSongName = $"{assetName}.ogg"; // Track name set here!
            _delaySeconds = MathHelper.Max(0f, delaySeconds);
            _currentDelayTimer = 0f;
            _isWaitingForDelay = false;
            _isPlaying = true;

            MediaPlayer.Volume = _volume;
            MediaPlayer.Play(_currentSong);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] Failed to load music track '{assetName}': {ex.Message}");
            CurrentSongName = "None (Error)";
        }
    }

    public void Update(GameTime gameTime)
    {
        if (!_isPlaying || _currentSong == null) return;

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_isWaitingForDelay)
        {
            _currentDelayTimer += dt;
            
            if (_currentDelayTimer >= _delaySeconds)
            {
                _isWaitingForDelay = false;
                MediaPlayer.Play(_currentSong);
            }
        }
        else
        {
            if (MediaPlayer.State == MediaState.Stopped)
            {
                if (_delaySeconds > 0f)
                {
                    _isWaitingForDelay = true;
                    _currentDelayTimer = 0f;
                }
                else
                {
                    MediaPlayer.Play(_currentSong);
                }
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