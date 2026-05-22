using MeinKraft;

/// <summary>
/// Mod that manages audio loading, playback, positional updates, looping, and cleanup
/// for all in-game sounds. Runs once per frame after assets are fully loaded.
/// </summary>
public sealed class ModAudio : ModBase
{
    private readonly Dictionary<string, AudioData> _audioCache = new();
    private readonly IAudioService _audioService;
    private readonly IAssetManager _assetManager;

    private bool _preloaded;

    /// <param name="audioService">Audio backend used for all playback operations.</param>
    public ModAudio(IAudioService audioService, IAssetManager assetManager, IGame game) : base(game)
    {
        _audioService = audioService;
        _assetManager = assetManager;
    }

    /// <inheritdoc/>
    public override void OnFrame(float dt)
    {
        if (_assetManager.Assets.Count == 0)
        {
            return;
        }

        if (!_preloaded)
        {
            _preloaded = true;
            Preload();
        }

        ProcessSounds();
    }

    /// <inheritdoc/>
    public override void OnUpdate(float dt)
    {
        if (Game.GuiState == GameState.MapLoading)
        {
            return;
        }

        float orientationX = MathF.Sin(Game.Player.Position.RotY);
        float orientationZ = -MathF.Cos(Game.Player.Position.RotY);
        _audioService.UpdateListener(
            Game.Player.Position.X, Game.Player.Position.Y, Game.Player.Position.Z,
            orientationX, 0f, orientationZ);
    }

    // ── Per-frame sound processing ────────────────────────────────────────────

    private void ProcessSounds()
    {
        for (int i = 0; i < _audioService.SoundsCount; i++)
        {
            Sound? sound = _audioService.Sounds[i];
            if (sound is null)
            {
                continue;
            }

            TryLoad(sound);
            TryUpdatePosition(sound);
            TryStop(i, sound);
            TryFinish(i, sound);
        }
    }

    /// <summary>
    /// Creates and starts playback for a sound whose <see cref="AudioTask"/> has not
    /// yet been allocated. Sets <see cref="AudioTask.Loop"/> before playing so the
    /// backend handles looping internally without requiring task recreation.
    /// </summary>
    private void TryLoad(Sound sound)
    {
        if (sound.Task is not null)
        {
            return;
        }

        AudioData data = GetOrLoadAudioData(sound.Name);
        if (!_audioService.IsAudioDataLoaded(data))
        {
            return;
        }

        AudioTask task = _audioService.CreateAudio(data);
        task.Loop = sound.Loop;
        sound.Task = task;
        _audioService.Play(task);
    }

    /// <summary>Synchronises the OpenAL source position with the sound's world position.</summary>
    private void TryUpdatePosition(Sound sound)
    {
        if (sound.Task is null)
        {
            return;
        }

        _audioService.SetPosition(sound.Task, sound.X, sound.Y, sound.Z);
    }

    /// <summary>Destroys and clears any sound flagged for stopping.</summary>
    private void TryStop(int i, Sound sound)
    {
        if (sound.Task is null || !sound.Stop)
        {
            return;
        }

        _audioService.DestroyAudio(sound.Task);
        _audioService.Sounds[i] = null;
    }

    /// <summary>
    /// Clears finished one-shot sounds. Looping sounds are handled internally
    /// by <see cref="AudioTask.Loop"/> and do not need recreation here.
    /// </summary>
    private void TryFinish(int i, Sound sound)
    {
        if (sound.Task is null)
        {
            return;
        }

        if (!_audioService.IsFinished(sound.Task))
        {
            return;
        }

        _audioService.Sounds[i] = null;
    }

    // ── Asset helpers ─────────────────────────────────────────────────────────

    /// <summary>Preloads all <c>.ogg</c> assets so the first playback request is instant.</summary>
    private void Preload()
    {
        foreach (Asset asset in _assetManager.Assets)
        {
            if (asset.name.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            {
                GetOrLoadAudioData(asset.name);
            }
        }
    }

    /// <summary>
    /// Returns cached <see cref="AudioData"/> for <paramref name="name"/>,
    /// decoding the asset on first access.
    /// </summary>
    private AudioData GetOrLoadAudioData(string name)
    {
        if (!_audioCache.TryGetValue(name, out AudioData? data))
        {
            data = _audioService.CreateAudioData(
                Game.GetAssetFile(name),
                Game.GetAssetFileLength(name));
            _audioCache[name] = data;
        }

        return data;
    }
}


// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Represents an active or pending sound instance in the world.
/// </summary>
public sealed class Sound
{
    /// <summary>Asset name of the audio file to play (e.g. <c>"hit.ogg"</c>).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>World-space X coordinate of the sound source.</summary>
    public float X { get; set; }

    /// <summary>World-space Y coordinate of the sound source.</summary>
    public float Y { get; set; }

    /// <summary>World-space Z coordinate of the sound source.</summary>
    public float Z { get; set; }

    /// <summary>When <see langword="true"/>, the sound plays continuously until stopped.</summary>
    public bool Loop { get; init; }

    /// <summary>When <see langword="true"/>, the sound will be destroyed on the next frame.</summary>
    public bool Stop { get; set; }

    /// <summary>The active playback task; <see langword="null"/> until the asset is loaded.</summary>
    internal AudioTask? Task { get; set; }
}