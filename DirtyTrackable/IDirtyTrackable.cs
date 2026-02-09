namespace DirtyTrackable;

public interface IDirtyTrackable
{
    bool IsDirty();
    IReadOnlyCollection<string> GetDirtyFields();
    void MarkFieldDirty(string field);
    void MarkClean(bool recursive = false);
    event Action DirtyStateChanged;
}