using hasheous_server.Classes;

namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that performs metadata match searches for various data object types.
    /// </summary>
    public class MetadataMatchSearch : IQueueTask
    {
        /// <inheritdoc/>
        public string TaskName { get; set; } = "MetadataMatchSearch";

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            hasheous_server.Classes.DataObjects dataObjects = new hasheous_server.Classes.DataObjects();

            await dataObjects.DataObjectMetadataSearch(DataObjects.DataObjectType.Platform);
            await dataObjects.DataObjectMetadataSearch(DataObjects.DataObjectType.Game, true);
            await dataObjects.DataObjectMetadataSearch(DataObjects.DataObjectType.Company, true);

            return null; // Assuming the method returns void, we return null here.
        }
    }
}