using System;

namespace Classes
{
    // see: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-5.0&tabs=visual-studio-mac#timed-background-tasks-1
    public class TimedHostedService : IHostedService, IDisposable
    {
        private int executionCount = 0;
        //private readonly ILogger<TimedHostedService> _logger;
        private Timer _timer;

        //public TimedHostedService(ILogger<TimedHostedService> logger)
        //{
        //    _logger = logger;
        //}

        public Task StartAsync(CancellationToken stoppingToken)
        {
            _timer = new Timer(DoWork, null, TimeSpan.Zero,
                TimeSpan.FromSeconds(5));

            return Task.CompletedTask;
        }

        private void DoWork(object state)
        {
            var count = Interlocked.Increment(ref executionCount);

            //_logger.LogInformation(
            //    "Timed Hosted Service is working. Count: {Count}", count);

            List<ProcessQueue.QueueProcessor.QueueItem> ActiveList = new List<ProcessQueue.QueueProcessor.QueueItem>();
            ActiveList.AddRange(ProcessQueue.QueueProcessor.QueueItems);
            foreach (ProcessQueue.QueueProcessor.QueueItem qi in ActiveList)
            {
                if (CheckIfProcessIsBlockedByOthers(qi) == false)
                {
                    qi.BlockedState(false);
                    if (DateTime.UtcNow > qi.NextRunTime || qi.Force == true)
                    {
                        qi.Execute();
                        if (qi.RemoveWhenStopped == true && qi.ItemState == ProcessQueue.QueueProcessor.QueueItemState.Stopped)
                        {
                            ProcessQueue.QueueProcessor.QueueItems.Remove(qi);
                        }
                    }
                }
                else
                {
                    qi.BlockedState(true);
                }
            }
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        private bool CheckIfProcessIsBlockedByOthers(ProcessQueue.QueueProcessor.QueueItem queueItem)
        {
            foreach (ProcessQueue.QueueProcessor.QueueItem qi in ProcessQueue.QueueProcessor.QueueItems)
            {
                if (qi.ItemState == ProcessQueue.QueueProcessor.QueueItemState.Running)
                {
                    // other service is running, check if queueItem is blocked by it
                    if (
                        qi.Blocks.Contains(queueItem.ItemType) ||
                        qi.Blocks.Contains(ProcessQueue.QueueItemType.All)
                    )
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}

