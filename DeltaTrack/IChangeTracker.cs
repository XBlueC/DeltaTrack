namespace DeltaTrack;

public interface IChangeTracker
{
    bool HasChanges();
    IReadOnlyCollection<string> GetChangedFields();
    void MarkChanged(string field);
    void MarkClean(bool recursive = false);
    event Action OnChanged;
    event Action<bool> OnClean;
}