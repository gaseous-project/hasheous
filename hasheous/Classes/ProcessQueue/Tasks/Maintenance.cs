namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task for performing daily maintenance operations.
    /// </summary>
    public class DailyMaintenance : IQueueTask
    {
        /// <inheritdoc/>
        public string TaskName { get; set; } = "DailyMaintenance";

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
        public string TaskName { get; set; } = "WeeklyMaintenance";

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            Maintenance wMaintenance = new Maintenance();
            await wMaintenance.RunWeeklyMaintenance();

            return null; // Assuming the method returns void, we return null here.
        }
    }
}