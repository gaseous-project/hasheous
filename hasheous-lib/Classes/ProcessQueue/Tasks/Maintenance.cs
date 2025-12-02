namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task for performing hourly maintenance operations on the frontend.
    /// </summary>
    public class HourlyMaintenance_Frontend : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>
        {

        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            Maintenance dMaintenance = new Maintenance();
            await dMaintenance.RunHourlyMaintenance_Frontend();

            return null; // Assuming the method returns void, we return null here.
        }
    }

    /// <summary>
    /// Represents a queue task for performing daily maintenance operations.
    /// </summary>
    public class DailyMaintenance : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>
        {
            QueueItemType.All
        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            Maintenance dMaintenance = new Maintenance();
            await dMaintenance.RunDailyMaintenance();

            return null; // Assuming the method returns void, we return null here.
        }
    }

    /// <summary>
    /// Represents a queue task for performing weekly maintenance operations.
    /// </summary>
    public class WeeklyMaintenance : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>
        {
            QueueItemType.All
        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            Maintenance wMaintenance = new Maintenance();
            await wMaintenance.RunWeeklyMaintenance();

            return null; // Assuming the method returns void, we return null here.
        }
    }
}