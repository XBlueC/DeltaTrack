namespace DeltaTrack;

public readonly struct ChangeInfo(int slot, long dirtyFlags, ITrackable source)
{
    public int Slot { get; } = slot;
    public long DirtyFlags { get; } = dirtyFlags;
    public ITrackable Source { get; } = source;
}