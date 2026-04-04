namespace DeltaTrack;

public interface IChangeTracker : IDisposable
{
    bool HasChanges();
    IReadOnlyCollection<string> GetChangedProperties();
    void MarkChanged(string property);
    void MarkClean(bool recursive = false);
    event Action OnChanged;
    event Action<bool> OnClean;
}