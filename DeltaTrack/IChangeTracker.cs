namespace DeltaTrack;

public interface IChangeTracker
{
    bool IsChanged();
    IReadOnlyCollection<string> GetChangedFields();
    void MarkFieldChanged(string field);
    void MarkClean(bool recursive = false);
    event Action ChangeStateChanged;
    event Action<bool> ChangeStateClear;
}