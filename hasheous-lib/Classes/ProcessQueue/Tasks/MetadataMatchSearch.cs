using hasheous_server.Classes;

namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that performs metadata match searches for various data object types.
    /// </summary>
    public class MetadataMatchSearch : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>
        {
            QueueItemType.GetMissingArtwork,
            QueueItemType.TallyVotes
        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            hasheous_server.Classes.DataObjects dataObjects = new hasheous_server.Classes.DataObjects();

            Logging.SendReport(Config.LogName, 1, 3, "Metadata Match Search for Platforms...");
            await dataObjects.DataObjectMetadataSearch(DataObjects.DataObjectType.Platform);
            Logging.SendReport(Config.LogName, 2, 3, "Metadata Match Search for Games...");
            await dataObjects.DataObjectMetadataSearch(DataObjects.DataObjectType.Game);
            Logging.SendReport(Config.LogName, 3, 3, "Metadata Match Search for Companies...");
            await dataObjects.DataObjectMetadataSearch(DataObjects.DataObjectType.Company);

            return null; // Assuming the method returns void, we return null here.
        }
    }
}