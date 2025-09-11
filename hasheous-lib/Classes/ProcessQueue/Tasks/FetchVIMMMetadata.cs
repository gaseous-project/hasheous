using hasheous_server.Classes;
using hasheous_server.Models;

namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that fetches VIMM metadata for all platforms.
    /// </summary>
    public class FetchVIMMMetadata : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>
        {

        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            hasheous_server.Classes.DataObjects dataObjects = new hasheous_server.Classes.DataObjects();

            // get all platforms
            DataObjectsList Platforms = new DataObjectsList();

            // get VIMMSLair manual metadata for each platform
            Platforms = dataObjects.GetDataObjects(DataObjects.DataObjectType.Platform).Result;
            foreach (DataObjectItem Platform in Platforms.Objects)
            {
                AttributeItem? VIMMPlatformName = Platform.Attributes != null
                    ? Platform.Attributes.Find(x => x.attributeName == AttributeItem.AttributeName.VIMMPlatformName)
                    : null;
                if (VIMMPlatformName != null)
                {
                    VIMMSLair.ManualDownloader tDownloader = new VIMMSLair.ManualDownloader(VIMMPlatformName.Value?.ToString() ?? string.Empty);
                    await tDownloader.Download();

                    // if we have a manual metadata file, load it into an object and process it
                    if (!string.IsNullOrEmpty(tDownloader.LocalFileName))
                    {
                        // search for the game
                        await VIMMSLair.ManualSearch.MatchManuals(tDownloader.LocalFileName, Platform);
                    }
                }
            }

            return null; // Assuming the method returns void, we return null here.
        }
    }
}