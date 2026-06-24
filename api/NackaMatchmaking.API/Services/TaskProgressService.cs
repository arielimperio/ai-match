using System.Collections.Concurrent;

namespace NackaMatchmaking.API.Services
{
    public class TaskProgressService
    {
        private readonly ConcurrentDictionary<string, double> _progress = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationSources = new();
        private readonly ConcurrentDictionary<string, DateTime> _startTimes = new();
        private readonly ConcurrentDictionary<string, (int Processed, int Total)> _itemCounts = new();

        public bool IsTaskActive(string key)
        {
            return _cancellationSources.ContainsKey(key);
        }

        public void UpdateProgress(string key, double percentage, int processed = 0, int total = 0)
        {
            _progress[key] = Math.Clamp(percentage, 0, 100);
            if (total > 0)
            {
                _itemCounts[key] = (processed, total);
            }
        }

        public double GetProgress(string key)
        {
            return _progress.TryGetValue(key, out var val) ? val : 0;
        }

        public (int Processed, int Total) GetItemCounts(string key)
        {
            return _itemCounts.TryGetValue(key, out var val) ? val : (0, 0);
        }

        public double? GetEstimatedTimeRemainingSeconds(string key)
        {
            if (!_startTimes.TryGetValue(key, out var startTime) || !_progress.TryGetValue(key, out var progress) || progress <= 1)
            {
                return null;
            }

            var elapsed = DateTime.UtcNow - startTime;
            if (elapsed.TotalSeconds < 2) return null;

            var totalEstimatedSeconds = elapsed.TotalSeconds / (progress / 100.0);
            return Math.Max(0, totalEstimatedSeconds - elapsed.TotalSeconds);
        }

        public void ResetProgress(string key)
        {
            _progress.TryRemove(key, out _);
            _startTimes.TryRemove(key, out _);
            _itemCounts.TryRemove(key, out _);
            if (_cancellationSources.TryRemove(key, out var cts))
            {
                cts.Dispose();
            }
        }

        public void CompleteTask(string key)
        {
            _progress[key] = 100;
            _startTimes.TryRemove(key, out _);
            // We keep _itemCounts[key] so the caller can read the final count
            if (_cancellationSources.TryRemove(key, out var cts))
            {
                cts.Dispose();
            }
        }

        public CancellationToken StartTask(string key)
        {
            if (_cancellationSources.TryRemove(key, out var oldCts))
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }

            var cts = new CancellationTokenSource();
            _cancellationSources[key] = cts;
            _progress[key] = 0;
            _itemCounts[key] = (0, 0);
            _startTimes[key] = DateTime.UtcNow;
            return cts.Token;
        }

        public void CancelTask(string key)
        {
            if (_cancellationSources.TryRemove(key, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
            _progress[key] = 0;
            _startTimes.TryRemove(key, out _);
        }
    }
}
