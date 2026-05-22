public partial class Game
{
    // -------------------------------------------------------------------------
    // One-shot sounds
    // -------------------------------------------------------------------------

    public void PlayAudio(string file)
    {
        if (!AudioEnabled)
        {
            return;
        }

        PlayAudioAt(file, EyesPosX, EyesPosY, EyesPosZ);
    }

    public void PlayAudio(string name, float x, float y, float z)
    {
        if (x == 0 && y == 0 && z == 0)
        {
            PlayAudio(name);
        }
        else
        {
            PlayAudioAt(name, x, z, y);
        }
    }

    public void PlayAudioAt(string file, float x, float y, float z)
    {
        if (file == null || !AudioEnabled || _assetManager.Assets.Count == 0)
        {
            return;
        }

        string file_ = file.Replace(".wav", ".ogg");
        if (GetAssetFileLength(file_) == 0)
        {
            Console.WriteLine(string.Format("File not found: {0}", file));
            return;
        }

        audioService.Add(new Sound { Name = file_, X = x, Y = y, Z = z });
    }

    // -------------------------------------------------------------------------
    // Looping sounds
    // -------------------------------------------------------------------------

    public void AudioPlayLoop(string file, bool play, bool restart)
    {
        if (!AudioEnabled && play)
        {
            return;
        }

        if (_assetManager.Assets.Count == 0)
        {
            return;
        }

        string file_ = file.Replace(".wav", ".ogg");
        if (GetAssetFileLength(file_) == 0)
        {
            Console.WriteLine(string.Format("File not found: {0}", file));
            return;
        }

        if (play)
        {
            Sound s = FindLoopingSound(file_);
            if (s == null)
            {
                s = new Sound { Name = file_, Loop = true };
                audioService.Add(s);
            }

            s.X = EyesPosX;
            s.Y = EyesPosY;
            s.Z = EyesPosZ;
        }
        else
        {
            StopLoopingSound(file_);
        }
    }

    private Sound FindLoopingSound(string file_)
    {
        for (int i = 0; i < audioService.SoundsCount; i++)
        {
            if (audioService.Sounds[i] != null && audioService.Sounds[i].Name == file_)
            {
                return audioService.Sounds[i];
            }
        }

        return null;
    }

    private void StopLoopingSound(string file_)
    {
        for (int i = 0; i < audioService.SoundsCount; i++)
        {
            if (audioService.Sounds[i] != null && audioService.Sounds[i].Name == file_)
            {
                audioService.Sounds[i].Stop = true;
            }
        }
    }
}