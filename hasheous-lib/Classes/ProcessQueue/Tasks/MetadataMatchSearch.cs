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

            await dataObjects.DataObjectMetadataSearch(DataObjects.DataObjectType.Platform);
            await dataObjects.DataObjectMetadataSearch(DataObjects.DataObjectType.Game);
            await dataObjects.DataObjectMetadataSearch(DataObjects.DataObjectType.Company);

            return null; // Assuming the method returns void, we return null here.
        }
    }
}