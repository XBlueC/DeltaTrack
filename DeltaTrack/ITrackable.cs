namespace DeltaTrack;

public interface ITrackable
{
    IChangeTracker GetChangeTracker();
}