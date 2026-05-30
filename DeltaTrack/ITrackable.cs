namespace DeltaTrack;

public interface ITrackable
{
    bool HasChanges();
    IReadOnlyList<string> GetChangedProperties();
    void MarkClean(bool recursive = false);
    event Action OnChanged;
    event Action<ChangeInfo> OnChangedDetailed;
    IDisposable SuspendTracking();
    IReadOnlyDictionary<string, object> GetDelta();
    void ApplyDelta(IReadOnlyDictionary<string, object> delta);
}