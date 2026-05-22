/// <summary>
/// Plays footstep sounds based on the block under the player and movement state.
/// </summary>
public class ModWalkSound : ModBase
{
    private const float StepSoundDuration = 0.4f;
    private const float RandomSoundChance = 40;

    private float walkSoundTimer;
    private int lastWalkSound;
    private readonly IBlockRegistry blockTypeRegistry;
    private readonly Random random;

    public ModWalkSound(IGame game, IBlockRegistry blockTypeRegistry) : base(game)
    {
        this.blockTypeRegistry = blockTypeRegistry;
        random = new Random();
    }

    public override void OnUpdate(float args)
    {
        if (Game.FollowId() != null)
        {
            return;
        }

        if (Game.soundnow)
        {
            UpdateWalkSound(StepSoundDuration / 2);
        }

        if (Game.IsPlayerOnGround && (Game.Controls.MovedX != 0 || Game.Controls.MovedY != 0))
        {
            UpdateWalkSound(args);
        }
    }

    internal void UpdateWalkSound(float dt)
    {
        walkSoundTimer += dt;

        string[] sounds = CurrentWalkSounds();
        int soundCount = GetSoundCount(sounds);
        if (soundCount == 0)
        {
            return;
        }

        if (walkSoundTimer < StepSoundDuration)
        {
            return;
        }

        walkSoundTimer = 0;
        lastWalkSound = (lastWalkSound + 1) % soundCount;

        if (random.Next() % 100 < RandomSoundChance)
        {
            lastWalkSound = random.Next() % soundCount;
        }

        Game.PlayAudio(sounds[lastWalkSound]);
    }

    internal string[] CurrentWalkSounds()
    {
        int b = Game.BlockUnderPlayer();
        return blockTypeRegistry.WalkSound[b != -1 ? b : 0];
    }

    internal static int GetSoundCount(string[] sounds)
    {
        int count = 0;
        for (int i = 0; i < BlockRegistry.SoundCount; i++)
        {
            if (sounds[i] != null)
            {
                count++;
            }
        }

        return count;
    }
}