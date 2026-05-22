using MeinKraft;

/// <summary>
/// The first <see cref="ServerSystem"/> to run on startup. Use <see cref="Initialize"/>
/// to register anything that must be in place before all other systems initialize.
/// </summary>
public class ServerSystemLoadFirst : ServerSystem
{
    public ServerSystemLoadFirst(IModEvents modEvents) : base(modEvents)
    {
    }

    /// <inheritdoc/>
    protected override void Initialize()
    {
        // Add things that need to be done prior to all other systems here.
    }
}