using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace CuboidFever;

public class AudioManager
{
    // ---- Configuration ----
    private const float MinDelaySeconds = 0.0f; // Minimum delay between songs (in seconds)
    private const float MaxDelaySeconds = 30.0f; // Maximum delay between songs (in seconds)
    // Tip: If you want a fixed, constant delay instead of a random range, 
    // simply set MinDelaySeconds and MaxDelaySeconds to the exact same value (e.g., 5.0f).

    private Dictionary<string, Song> _songs = new();
    private Dictionary<string, SoundEffect> _soundEffects = new();
    private List<string> _availableTrackNames = new();
    
    private float _delaySeconds;
    private float _currentDelayTimer;
    
    private bool _isWaitingForDelay;
    private bool _isPlaying;
    private float _volume = 0.8f;
    private readonly Random _random = new();

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
    /// Loads tracks specified in data/cuboidfever/music_list.json and sound effects (.wav) into memory upfront.
    /// Call this once during game startup.
    /// </summary>
    public void InitializePlaylist()
    {
        try
        {
            string jsonPath = Path.Combine(AppContext.BaseDirectory, "data", "cuboidfever", "music_list.json");
            string soundsDir = Path.Combine(AppContext.BaseDirectory, "assets", "cuboidfever", "sounds");

            if (!File.Exists(jsonPath))
            {
                Console.WriteLine($"[Audio] Music list JSON not found: {jsonPath}");
                CurrentSongName = "None (Music List Not Found)";
                return;
            }

            if (!Directory.Exists(soundsDir))
            {
                Console.WriteLine($"[Audio] Sounds directory not found: {soundsDir}");
                CurrentSongName = "None (Directory Not Found)";
                return;
            }

            // ---- Load Music Tracks (.ogg) from JSON list ----
            string jsonContent = File.ReadAllText(jsonPath);
            List<string> trackNames = System.Text.Json.JsonSerializer.Deserialize<List<string>>(jsonContent);

            if (trackNames != null)
            {
                foreach (var assetName in trackNames)
                {
                    string fullPath = Path.Combine(soundsDir, $"{assetName}.ogg");
                    if (!File.Exists(fullPath))
                    {
                        Console.WriteLine($"[Audio] Track file not found: {fullPath}");
                        continue;
                    }

                    try
                    {
                        Song song = Song.FromUri(assetName, new Uri(fullPath, UriKind.Absolute));
                        _songs[assetName] = song;
                        _availableTrackNames.Add(assetName);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Audio] Failed to load track {assetName}: {ex.Message}");
                    }
                }
            }

            // ---- Load Sound Effects (.wav) automatically from sounds directory ----
            var wavFiles = Directory.GetFiles(soundsDir, "*.wav")
                .Select(Path.GetFileNameWithoutExtension)
                .ToArray();

            foreach (var assetName in wavFiles)
            {
                string fullPath = Path.Combine(soundsDir, $"{assetName}.wav");
                try
                {
                    using var stream = File.OpenRead(fullPath);
                    SoundEffect sfx = SoundEffect.FromStream(stream);
                    _soundEffects[assetName] = sfx;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Audio] Failed to load sound effect {assetName}: {ex.Message}");
                }
            }

            Console.WriteLine($"[Audio] Successfully preloaded {_songs.Count} music tracks and {_soundEffects.Count} sound effects.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] Failed to initialize playlist: {ex.Message}");
        }
    }

    public void PlaySound(string soundName, float volumeScale = 1.0f)
    {
        if (_soundEffects.TryGetValue(soundName, out var sfx))
        {
            try
            {
                sfx.Play(_volume * volumeScale, 0f, 0f);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Audio] Failed to play sound {soundName}: {ex.Message}");
            }
        }
    }

    public void StartPlaylist()
    {
        if (_availableTrackNames.Count == 0)
        {
            CurrentSongName = "None (No Tracks Available)";
            return;
        }

        _isPlaying = true;
        _isWaitingForDelay = true;
        _currentDelayTimer = 0f;
        _delaySeconds = MinDelaySeconds >= MaxDelaySeconds 
            ? MinDelaySeconds 
            : (float)(_random.NextDouble() * (MaxDelaySeconds - MinDelaySeconds) + MinDelaySeconds);
        CurrentSongName = "None (Waiting for music...)";
    }

    public void PlayRandomSong()
    {
        if (_availableTrackNames.Count == 0) return;

        try
        {
            string assetName = _availableTrackNames[_random.Next(_availableTrackNames.Count)];
            Song songToPlay = _songs[assetName];

            CurrentSongName = $"{assetName}.ogg";
            _isPlaying = true;
            _isWaitingForDelay = false;

            MediaPlayer.Volume = _volume;
            MediaPlayer.Play(songToPlay);
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
            if (MediaPlayer.State == MediaState.Stopped)
            {
                _isWaitingForDelay = true;
                _currentDelayTimer = 0f;
                _delaySeconds = MinDelaySeconds >= MaxDelaySeconds 
                    ? MinDelaySeconds 
                    : (float)(_random.NextDouble() * (MaxDelaySeconds - MinDelaySeconds) + MinDelaySeconds);
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