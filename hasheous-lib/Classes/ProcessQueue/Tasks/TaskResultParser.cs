using System.Data;
using hasheous_server.Models.Tasks;
using HasheousClient.Models;
using Newtonsoft.Json;

namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that parses task results.
    /// </summary>
    public class TaskResultParser : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>
        {
            QueueItemType.MetadataMatchSearch,
            QueueItemType.MetadataMapDump
        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            // Find all tasks submitted more than 10 minutes ago
            string sql = "SELECT * FROM Task_Queue WHERE status = @status AND completion_time < @time;";
            DataTable submittedTasks = await Config.database.ExecuteCMDAsync(sql, new Dictionary<string, object>
            {
                { "@status", QueueItemStatus.Submitted},
                { "@time", DateTime.UtcNow.AddMinutes(-10) }
            });

            hasheous_server.Classes.DataObjects dataObjects = new hasheous_server.Classes.DataObjects();

            foreach (DataRow row in submittedTasks.Rows)
            {
                // load into the task object
                QueueItemModel taskItem = new QueueItemModel(row);
                if (taskItem == null)
                {
                    continue;
                }

                try
                {
                    if (taskItem.ErrorMessage != null && taskItem.ErrorMessage.Length > 0)
                    {
                        taskItem.Status = QueueItemStatus.Failed;
                        taskItem.CompletedAt = DateTime.UtcNow;
                        await taskItem.Commit();
                        continue;
                    }

                    // get the related data object
                    hasheous_server.Models.DataObjectItem? dataObject = null;
                    if (taskItem.DataObjectId > 0)
                    {
                        dataObject = await dataObjects.GetDataObject(taskItem.DataObjectId);
                    }

                    // process the task result
                    switch (taskItem.TaskName)
                    {
                        case TaskType.AIDescriptionAndTagging:
                            // parse result from json into a Dictionary
                            AITaskResult? aiResults = JsonConvert.DeserializeObject<AITaskResult>(taskItem.Result ?? "");

                            // update data object with description and tags
                            if (dataObject != null && aiResults != null)
                            {
                                // process description
                                DataTable aiDt = await Config.database.ExecuteCMDAsync("UPDATE DataObject_Attributes SET AttributeValue = @description WHERE DataObjectId = @dataObjectId AND AttributeType = @attributeType AND AttributeName = @attributeName; SELECT ROW_COUNT() AS rowsAffected;",
                                    new Dictionary<string, object>
                                    {
                                        { "@description", aiResults.Description ?? "" },
                                        { "@dataObjectId", dataObject.Id },
                                        { "@attributeType", hasheous_server.Models.AttributeItem.AttributeType.LongString },
                                        { "@attributeName", hasheous_server.Models.AttributeItem.AttributeName.AIDescription }
                                    });
                                if ((long)aiDt.Rows[0][0] == 0)
                                {
                                    // insert new attribute
                                    await Config.database.ExecuteCMDAsync("INSERT INTO DataObject_Attributes (DataObjectId, AttributeType, AttributeName, AttributeValue, AttributeRelationType) VALUES (@dataObjectId, @attributeType, @attributeName, @attributeValue, @attributeRelationType);",
                                        new Dictionary<string, object>
                                        {
                                            { "@dataObjectId", dataObject.Id },
                                            { "@attributeType", hasheous_server.Models.AttributeItem.AttributeType.LongString },
                                            { "@attributeName", hasheous_server.Models.AttributeItem.AttributeName.AIDescription },
                                            { "@attributeValue", aiResults.Description ?? "" },
                                            { "@attributeRelationType", 100 }
                                        });
                                }

                                // process tags
                                // delete existing AI tags
                                await Config.database.ExecuteCMDAsync("DELETE FROM DataObject_Tags WHERE DataObjectId = @dataObjectId AND AIAssigned = @aiAssigned;",
                                    new Dictionary<string, object>
                                    {
                                        { "@dataObjectId", dataObject.Id },
                                        { "@aiAssigned", true }
                                    });
                                if (aiResults.Tags != null)
                                {
                                    var existingTags = await dataObjects.GetTags();
                                    foreach (var suppliedTagCategory in aiResults.Tags)
                                    {
                                        hasheous_server.Models.DataObjectItemTags.TagType? tagType = null;
                                        switch (suppliedTagCategory.Key.ToLower())
                                        {
                                            case "type":
                                                tagType = hasheous_server.Models.DataObjectItemTags.TagType.PlatformType;
                                                break;
                                            case "era":
                                                tagType = hasheous_server.Models.DataObjectItemTags.TagType.PlatformEra;
                                                break;
                                            case "hardware generation":
                                                tagType = hasheous_server.Models.DataObjectItemTags.TagType.PlatformHardwareGeneration;
                                                break;
                                            case "hardware specs":
                                                tagType = hasheous_server.Models.DataObjectItemTags.TagType.PlatformHardwareSpecs;
                                                break;
                                            case "connectivity":
                                                tagType = hasheous_server.Models.DataObjectItemTags.TagType.PlatformConnectivity;
                                                break;
                                            case "input methods":
                                                tagType = hasheous_server.Models.DataObjectItemTags.TagType.PlatformInputMethod;
                                                break;
                                            case "genre":
                                                tagType = hasheous_server.Models.DataObjectItemTags.TagType.GameGenre;
                                                break;
                                            case "gameplay":
                                                tagType = hasheous_server.Models.DataObjectItemTags.TagType.GameGameplay;
                                                break;
                                            case "features":
                                                tagType = hasheous_server.Models.DataObjectItemTags.TagType.GameFeature;
                                                break;
                                            case "theme":
                                                tagType = hasheous_server.Models.DataObjectItemTags.TagType.GameTheme;
                                                break;
                                            case "perspective":
                                                tagType = hasheous_server.Models.DataObjectItemTags.TagType.GamePerspective;
                                                break;
                                            case "artwork style":
                                                tagType = hasheous_server.Models.DataObjectItemTags.TagType.GameArtStyle;
                                                break;
                                            default:
                                                continue;
                                        }
                                        if (tagType != null)
                                        {
                                            foreach (string tagString in suppliedTagCategory.Value)
                                            {
                                                if (tagString.Length > 0)
                                                {
                                                    // check if the tag exists
                                                    hasheous_server.Models.DataObjectItemTags.TagModel? matchingTag = null;
                                                    if (existingTags.ContainsKey((hasheous_server.Models.DataObjectItemTags.TagType)tagType))
                                                    {
                                                        matchingTag = existingTags[(hasheous_server.Models.DataObjectItemTags.TagType)tagType].Tags
                                                        .FirstOrDefault(t => t.Text.Equals(tagString, StringComparison.OrdinalIgnoreCase));
                                                    }
                                                    long tagId;
                                                    if (matchingTag != null)
                                                    {
                                                        // tag exists, map it to the data object
                                                        tagId = matchingTag.Id;
                                                        await Config.database.ExecuteCMDAsync("INSERT INTO DataObject_Tags (DataObjectId, TagId, AIAssigned) VALUES (@dataObjectId, @tagId, @aiAssigned);",
                                                            new Dictionary<string, object>
                                                            {
                                                            { "@dataObjectId", dataObject.Id },
                                                            { "@tagId", tagId },
                                                            { "@aiAssigned", true }
                                                            });
                                                    }
                                                    else
                                                    {
                                                        // tag does not exist, create it and map it
                                                        DataTable newTagDt = await Config.database.ExecuteCMDAsync("INSERT INTO Tags (`type`, `name`) VALUES (@tagType, @name); SELECT LAST_INSERT_ID() AS NewTagId;",
                                                            new Dictionary<string, object>
                                                            {
                                                            { "@tagType", (int)tagType },
                                                            { "@name", tagString.ToLower() }
                                                            });
                                                        tagId = Convert.ToInt64(newTagDt.Rows[0]["NewTagId"]);
                                                        await Config.database.ExecuteCMDAsync("INSERT INTO DataObject_Tags (DataObjectId, TagId, AIAssigned) VALUES (@dataObjectId, @tagId, @aiAssigned);",
                                                            new Dictionary<string, object>
                                                            {
                                                            { "@dataObjectId", dataObject.Id },
                                                            { "@tagId", tagId },
                                                            { "@aiAssigned", true }
                                                            });
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            break;
                    }

                    // mark task as completed
                    taskItem.Status = QueueItemStatus.Completed;
                    taskItem.CompletedAt = DateTime.UtcNow;
                    await taskItem.Commit();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing task ID {row["id"]}: {ex.Message}");
                    taskItem.Status = QueueItemStatus.Failed;
                    taskItem.CompletedAt = DateTime.UtcNow;
                    await taskItem.Commit();
                }
            }

            // prune stale clients
            var clients = await hasheous_server.Classes.Tasks.Clients.ClientManagement.GetAllClients();
            foreach (var client in clients)
            {
                if (client.IsStale)
                {
                    await client.Unregister();
                }
            }

            return null; // Assuming the method returns void, we return null here.
        }

        private class AITaskResult
        {
            public string? Description { get; set; }
            public Dictionary<string, string[]>? Tags { get; set; }
        }
    }
}