using System.Collections;

namespace DirtyTrackable;

public class DirtyTracker : BaseDirtyTracker
{
    private readonly IDirtyTrackable _owner;
    public DirtyTracker(IDirtyTrackable owner)
    {
        _owner = owner;
    }

    public void Subscribe(object item, Action onChange)
    {
        if (item == null) return;

        switch (item)
        {
            case IDictionary dictionary:
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Value is IDirtyTrackable trackable)
                    {
                        SubscribeChild(trackable, onChange);
                    }
                }

                break;
            }
            case ICollection collection:
            {
                foreach (var element in collection)
                {
                    if (element is IDirtyTrackable trackable)
                    {
                        SubscribeChild(trackable, onChange);
                    }
                }

                break;
            }
            case IDirtyTrackable trackable:
            {
                SubscribeChild(trackable, onChange);
                break;
            }
        }
    }

    public void Unsubscribe(object item, Action onChange)
    {
        switch (item)
        {
            case IDictionary dictionary:
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Value is IDirtyTrackable trackable)
                    {
                        UnsubscribeChild(trackable, onChange);
                    }
                }

                break;
            }
            case ICollection collection:
            {
                foreach (var element in collection)
                {
                    if (element is IDirtyTrackable trackable)
                    {
                        UnsubscribeChild(trackable, onChange);
                    }
                }

                break;
            }
            case IDirtyTrackable trackable:
            {
                UnsubscribeChild(trackable, onChange);
                break;
            }
        }
    }
}