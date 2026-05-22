using MeinKraft;

/// <summary>
/// Abstracts audio playback: data loading, task lifecycle, playback control,
/// spatial positioning, and listener updates.
/// </summary>
public interface IAudioService
{
    // ── Playback (called by Game) ─────────────────────────────────────────────

    /// <summary>Live sound slots; null entries are vacant and available for reuse.</summary>
    Sound?[] Sounds { get; }

    /// <summary>Number of slots that have ever been occupied; the scan upper bound.</summary>
    int SoundsCount { get; }

    /// <summary>
    /// Adds <paramref name="sound"/> to the pool. A null slot within the active
    /// range is reused before expanding the pool.
    /// </summary>
    void Add(Sound sound);

    /// <summary>Removes all sounds from the pool without stopping them.</summary>
    void Clear();

    /// <summary>Stops and removes all active sounds.</summary>
    void StopAll();

    /// <summary>Updates the OpenAL listener to the player's current eye position and orientation.</summary>
    void UpdateListener(float posX, float posY, float posZ,
                        float orientX, float orientY, float orientZ);

    // ── Low-level (used internally / by AudioTask) ────────────────────────────

    /// <summary>Decodes raw audio bytes into an <see cref="AudioData"/> instance ready for playback.</summary>
    AudioData CreateAudioData(byte[] data, int dataLength);

    /// <summary>Returns <see langword="true"/> if <paramref name="data"/> has been decoded and is ready for playback.</summary>
    bool IsAudioDataLoaded(AudioData data);

    /// <summary>Allocates a playback task for the given <paramref name="data"/> without starting it.</summary>
    AudioTask CreateAudio(AudioData data);

    /// <summary>Starts or resumes playback of <paramref name="audio"/>.</summary>
    void Play(AudioTask audio);

    /// <summary>Pauses playback of <paramref name="audio"/> without discarding it.</summary>
    void Pause(AudioTask audio);

    /// <summary>Stops and releases all resources held by <paramref name="audio"/>.</summary>
    void DestroyAudio(AudioTask audio);

    /// <summary>Returns <see langword="true"/> if <paramref name="audio"/> has finished playing and its thread has exited.</summary>
    bool IsFinished(AudioTask audio);

    /// <summary>Sets the world-space position of <paramref name="audio"/> for spatial attenuation.</summary>
    void SetPosition(AudioTask audio, float x, float y, float z);
}
