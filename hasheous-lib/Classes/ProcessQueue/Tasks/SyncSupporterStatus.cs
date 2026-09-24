using Classes.Supporters;

namespace Classes.ProcessQueue
{
    /// <summary>
    /// Synchronizes supporter recognition state from configured payment providers.
    /// </summary>
    public class SyncSupporterStatus : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>
        {
        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync(object? options = null)
        {
            SupporterRecognitionService supporterRecognitionService = new SupporterRecognitionService();
            await supporterRecognitionService.SyncAllAsync();
            return null;
        }
    }
}
