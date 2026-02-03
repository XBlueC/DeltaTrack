namespace DirtyTrackable;

public interface IDirtyTrackable
{
    bool IsDirty();
    IReadOnlyCollection<string> GetDirtyFields();
    void MarkFieldDirty(string field);
    void MarkClean();
    event Action DirtyStateChanged;
}