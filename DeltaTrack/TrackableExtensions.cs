namespace DeltaTrack;

public static class TrackableExtensions
{
    extension(ITrackable trackable)
    {
        public bool HasChanges()
        {
            return trackable.GetChangeTracker().HasChanges();
        }
        
        public IReadOnlyCollection<string> GetChangedFields()
        {
            return trackable.GetChangeTracker().GetChangedFields();
        }
        
        public void MarkClean(bool recursive = false)
        {
            trackable.GetChangeTracker().MarkClean(recursive);
        }
        
        public void MarkChanged(string field)
        {
            trackable.GetChangeTracker().MarkChanged(field);
        }
        
        public IDisposable SubscribeToChanges(Action handler)
        {
            var tracker = trackable.GetChangeTracker();
            tracker.OnChanged += handler;
        
            return new ChangeSubscription(tracker, handler);
        }
    }
    
    private class ChangeSubscription(IChangeTracker tracker, Action handler) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            tracker.OnChanged -= handler;
            _disposed = true;
        }
    }
}