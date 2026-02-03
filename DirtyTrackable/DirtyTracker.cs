using System.Collections;

namespace DirtyTrackable;

public class DirtyTracker
{
    private readonly HashSet<string> _dirtyFields = new();
    private readonly IDirtyTrackable _self;

    public DirtyTracker(IDirtyTrackable self)
    {
        _self = self;
    }

    public bool IsDirty => _dirtyFields.Count > 0;

    public IReadOnlyCollection<string> DirtyFields => _dirtyFields.ToList().AsReadOnly();

    public void MarkFieldDirty(string fieldName)
    {
        _dirtyFields.Add(fieldName);
    }

    public void MarkClean()
    {
        _dirtyFields.Clear();
    }

    public void Subscribe(object item, Action action)
    {
        if (item == null) return;

        switch (item)
        {
            case IDictionary dictionary:
            {
                foreach (DictionaryEntry o in dictionary)
                    if (o.Value is IDirtyTrackable trackable)
                        trackable.DirtyStateChanged += action;

                break;
            }
            case ICollection collection:
            {
                foreach (var o in collection)
                    if (o is IDirtyTrackable trackable)
                        trackable.DirtyStateChanged += action;

                break;
            }
            case IDirtyTrackable trackable:
            {
                trackable.DirtyStateChanged += action;
                break;
            }
        }
    }

    public void Unsubscribe(object item, Action action)
    {
        switch (item)
        {
            case IDictionary dictionary:
            {
                foreach (DictionaryEntry o in dictionary)
                    if (o.Value is IDirtyTrackable trackable)
                        trackable.DirtyStateChanged -= action;

                break;
            }
            case ICollection collection:
            {
                foreach (var o in collection)
                    if (o is IDirtyTrackable trackable)
                        trackable.DirtyStateChanged -= action;

                break;
            }
            case IDirtyTrackable trackable:
            {
                trackable.DirtyStateChanged -= action;
                break;
            }
        }
    }
}