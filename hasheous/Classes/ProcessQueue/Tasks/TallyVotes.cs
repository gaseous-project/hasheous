using hasheous_server.Classes;

namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that tallies votes from submissions.
    /// </summary>
    public class TallyVotes : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>
        {
            QueueItemType.GetMissingArtwork,
            QueueItemType.MetadataMatchSearch
        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            Submissions submissions = new Submissions();
            await submissions.TallyVotes();

            return null; // Assuming the method returns void, we return null here.
        }
    }
}