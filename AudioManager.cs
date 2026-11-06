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
    // ---- Music Configuration ----
    private const float MinDelaySeconds = 0.0f;
    private const float MaxDelaySeconds = 30.0f;

    private readonly Dictionary<string, Song> _songs = new();
    private readonly Dictionary<string, SoundEffect> _soundEffects = new();
    private readonly List<string> _titleTrackNames = new();
    private readonly List<string> _gameTrackNames = new();
    private readonly List<string> _availableTrackNames = new();

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
    /// Loads music tracks and sound effects into memory.
    /// Call once during game startup.
    /// </summary>
    public void InitializePlaylist()
    {
        string soundsDir = Path.Combine(
            AppContext.BaseDirectory,
            "assets",
            "cuboidfever",
            "sounds"
        );

        // Load sound effects independently of music.
        if (Directory.Exists(soundsDir))
        {
            LoadSoundEffects(soundsDir);
        }
        else
        {
            Console.WriteLine($"[Audio] Sounds directory not found: {soundsDir}");
        }

        // Load title and game music lists independently using the DRY helper.
        LoadMusicList(
            Path.Combine(AppContext.BaseDirectory, "data", "cuboidfever", "music_list_title.json"),
            _titleTrackNames,
            soundsDir
        );
        LoadMusicList(
            Path.Combine(AppContext.BaseDirectory, "data", "cuboidfever", "music_list_game.json"),
            _gameTrackNames,
            soundsDir
        );

        Console.WriteLine(
            $"[Audio] Successfully preloaded {_songs.Count} music tracks and {_soundEffects.Count} sound effects."
        );
    }

    private void LoadMusicList(string jsonPath, List<string> targetList, string soundsDir)
    {
        if (!File.Exists(jsonPath))
        {
            Console.WriteLine($"[Audio] Music list not found: {jsonPath}");
            return;
        }

        try
        {
            var trackNames = System.Text.Json.JsonSerializer.Deserialize<List<string>>(File.ReadAllText(jsonPath));
            if (trackNames == null) return;

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
                    targetList.Add(assetName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Audio] Failed to load track {assetName}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] Failed to load music list {jsonPath}: {ex.Message}");
        }
    }

    // ---- Sound Effect Loading ----

    private void LoadSoundEffects(string soundsDir)
    {
        var wavFiles = Directory.GetFiles(
            soundsDir,
            "*.wav",
            SearchOption.AllDirectories
        );

        foreach (var fullPath in wavFiles)
        {
            string relativePath = Path.GetRelativePath(soundsDir, fullPath);

            string assetName = Path.ChangeExtension(relativePath, null)
                .Replace('\\', '/');

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
    }

    // ---- Sound Effect Playback ----

    public void PlaySound(string soundName, float volumeScale = 1.0f)
    {
        if (_soundEffects.TryGetValue(soundName, out var sfx))
        {
            try
            {
                float volume = MathHelper.Clamp(_volume * volumeScale, 0f, 1f);
                sfx.Play(volume, 0f, 0f);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Audio] Failed to play sound {soundName}: {ex.Message}");
            }
        }
    }

    // Dedicated playback method for menu buttons.
    public void PlayMenuSound(MenuButtonSound sound)
    {
        switch (sound)
        {
            case MenuButtonSound.Click:
                PlaySound("ui/click");
                break;

            case MenuButtonSound.Back:
                PlaySound("ui/back");
                break;

            case MenuButtonSound.Start:
                PlaySound("ui/start");
                break;

            case MenuButtonSound.None:
                break;
        }
    }

    // ---- Music Playback ----

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
            string assetName = _availableTrackNames[
                _random.Next(_availableTrackNames.Count)
            ];

            Song songToPlay = _songs[assetName];

            CurrentSongName = $"{assetName}.ogg";
            _isPlaying = true;
            _isWaitingForDelay = false;

            MediaPlayer.IsRepeating = false;
            MediaPlayer.Volume = _volume;
            MediaPlayer.Play(songToPlay);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] Failed to play random music track: {ex.Message}");
            CurrentSongName = "None (Error)";
        }
    }

    /// <summary>
    /// Plays the title-screen track and loops it.
    /// Stops any in-game playlist that was running.
    /// </summary>
    public void PlayTitleMusic()
    {
        _isPlaying = false;
        _isWaitingForDelay = false;

        if (_titleTrackNames.Count == 0)
        {
            CurrentSongName = "None (No title tracks)";
            return;
        }

        string assetName = _titleTrackNames[0];
        if (_songs.TryGetValue(assetName, out var song))
        {
            CurrentSongName = $"{assetName}.ogg";
            MediaPlayer.IsRepeating = true;
            MediaPlayer.Volume = _volume;
            MediaPlayer.Play(song);
        }
        else
        {
            Console.WriteLine($"[Audio] Title track '{assetName}' not found.");
            CurrentSongName = "None (Title track missing)";
        }
    }

    /// <summary>
    /// Starts the normal in-game random playlist (non-looping tracks + delay between songs).
    /// </summary>
    public void PlayGameMusic()
    {
        MediaPlayer.Stop();
        MediaPlayer.IsRepeating = false;

        // Point the playlist system at the game tracks
        _availableTrackNames.Clear();
        _availableTrackNames.AddRange(_gameTrackNames);
        StartPlaylist();
    }

    // ---- Update ----

    public void Update(GameTime gameTime)
    {
        if (!_isPlaying) return;

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_isWaitingForDelay)
        {
            _currentDelayTimer += dt;

            if (_currentDelayTimer >= _delaySeconds)
                PlayRandomSong();
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

    // ---- Stop ----

    public void Stop()
    {
        _isPlaying = false;
        _isWaitingForDelay = false;

        CurrentSongName = "None (Stopped)";
        MediaPlayer.Stop();
    }

    // ---- Cleanup ----

    public void Dispose()
    {
        foreach (var sfx in _soundEffects.Values)
            sfx.Dispose();

        _soundEffects.Clear();

        foreach (var song in _songs.Values)
            song.Dispose();

        _songs.Clear();
        _titleTrackNames.Clear();
        _gameTrackNames.Clear();
        _availableTrackNames.Clear();
    }
}