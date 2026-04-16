namespace DeltaTrack;

public interface ITrackable
{
    bool HasChanges();
    IReadOnlyList<string> GetChangedProperties();
    void MarkClean(bool recursive = false);
    event Action OnChanged;
}