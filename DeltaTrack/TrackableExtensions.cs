namespace DeltaTrack;

public static class TrackableExtensions
{
    extension(ITrackable trackable)
    {
        public IDisposable SubscribeToChanges(Action handler)
        {
            trackable.OnChanged += handler;
            return new ChangeSubscription(trackable, handler);
        }
    }

    private class ChangeSubscription(ITrackable trackable, Action handler) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            trackable.OnChanged -= handler;
            _disposed = true;
        }
    }
}