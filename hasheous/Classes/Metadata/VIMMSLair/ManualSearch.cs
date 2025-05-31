using System.Security.Cryptography.Xml;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Classes;
using hasheous_server.Classes;
using hasheous_server.Models;
using IGDB.Models;
using Newtonsoft.Json;

namespace VIMMSLair
{
    public class ManualSearch
    {
        public static string? Search(string PlatformName, string GameName)
        {
            Logging.Log(Logging.LogType.Information, "VIMMSLair", "Searching for manual for " + GameName + " on " + PlatformName);

            // download the manual list
            var manualDownloader = new ManualDownloader(PlatformName);
            manualDownloader.Download();

            // load the json into a ManualObject
            var manualObject = new ManualObject();
            string json = File.ReadAllText(manualDownloader.LocalFileName);
            manualObject = JsonConvert.DeserializeObject<ManualObject>(json);

            // search for the game
            foreach (var manual in manualObject.manuals)
            {
                foreach (var datName in manual.datNames)
                {
                    if (datName.ToLower() == GameName.ToLower())
                    {
                        Logging.Log(Logging.LogType.Information, "VIMMSLair", "Found manual for " + GameName + " on " + PlatformName);
                        return manual.id.ToString();
                    }
                }
            }

            Logging.Log(Logging.LogType.Information, "VIMMSLair", "No manual found for " + GameName + " on " + PlatformName);
            return "";
        }

        public static async Task MatchManuals(string manualFile, DataObjectItem platformDataObject)
        {
            // load the json into a ManualObject
            var manualObject = new ManualObject();
            string json = File.ReadAllText(manualFile);
            manualObject = JsonConvert.DeserializeObject<ManualObject>(json);

            // search for the game
            hasheous_server.Classes.DataObjects DataObjects = new hasheous_server.Classes.DataObjects();
            foreach (ManualObject.ManualObjectItem manual in manualObject.manuals)
            {
                // search for the game
                foreach (string datName in manual.datNames)
                {
                    // define search candidates list
                    List<string> searchCandidates = new List<string>();

                    // clean up the datname
                    string datSearchName = datName;

                    // remove everything after brackets
                    int bracketIndex = datSearchName.IndexOf('(');
                    if (bracketIndex != -1)
                    {
                        datSearchName = datSearchName.Substring(0, bracketIndex);
                    }
                    bracketIndex = datSearchName.IndexOf('[');
                    if (bracketIndex != -1)
                    {
                        datSearchName = datSearchName.Substring(0, bracketIndex);
                    }
                    bracketIndex = datSearchName.IndexOf('{');
                    if (bracketIndex != -1)
                    {
                        datSearchName = datSearchName.Substring(0, bracketIndex);
                    }
                    bracketIndex = datSearchName.IndexOf('<');
                    if (bracketIndex != -1)
                    {
                        datSearchName = datSearchName.Substring(0, bracketIndex);
                    }

                    // remove version numbers
                    datSearchName = Regex.Replace(datSearchName, @"v\d+", "", RegexOptions.IgnoreCase);
                    datSearchName = Regex.Replace(datSearchName, @"ver\d+", "", RegexOptions.IgnoreCase);
                    datSearchName = Regex.Replace(datSearchName, @"version\d+", "", RegexOptions.IgnoreCase);

                    // remove revision numbers
                    datSearchName = Regex.Replace(datSearchName, @"r\d+", "", RegexOptions.IgnoreCase);
                    datSearchName = Regex.Replace(datSearchName, @"rev\d+", "", RegexOptions.IgnoreCase);
                    datSearchName = Regex.Replace(datSearchName, @"revision\d+", "", RegexOptions.IgnoreCase);
                    datSearchName = Regex.Replace(datSearchName, @"build\d+", "", RegexOptions.IgnoreCase);

                    // remove dashes
                    string datSearchName_Dashless = datSearchName.Replace("-", " ");
                    datSearchName_Dashless = datSearchName_Dashless.Replace(" - ", " ");

                    // remove trailing full stops
                    string datSearchName_FullStopless = datSearchName.TrimEnd('.');

                    // add the search candidates
                    searchCandidates.Add(datSearchName.Trim());
                    if (!searchCandidates.Contains(datSearchName_Dashless.Trim()))
                    {
                        searchCandidates.Add(datSearchName_Dashless.Trim());
                    }
                    if (!searchCandidates.Contains(datSearchName_FullStopless.Trim()))
                    {
                        searchCandidates.Add(datSearchName_FullStopless.Trim());
                    }

                    // generate a name with "the" at the end for each search candidate
                    foreach (string searchCandidate in searchCandidates)
                    {
                        if (searchCandidate.ToLower().StartsWith("the "))
                        {
                            string nameWithoutThe = "The " + searchCandidate.Substring(4).Trim();
                            if (!searchCandidates.Contains(nameWithoutThe))
                            {
                                searchCandidates.Add(nameWithoutThe);
                            }
                        }
                    }

                    // get the game
                    foreach (string searchCandidate in searchCandidates)
                    {
                        bool found = false;

                        hasheous_server.Models.DataObjectsList dataObjectItems = await DataObjects.GetDataObjects(DataObjects.DataObjectType.Game, 0, 0, searchCandidate);
                        foreach (DataObjectItem dataObjectItem in dataObjectItems.Objects)
                        {
                            // don't add the manual if it already has one
                            AttributeItem existingManualAttribute = dataObjectItem.Attributes.Find(x => x.attributeName == AttributeItem.AttributeName.VIMMManualId);
                            if (existingManualAttribute != null)
                            {
                                continue;
                            }

                            // check the game name matches the search candidate - we do this to eliminate false positives such as games with partial matches
                            if (dataObjectItem.Name.ToLower() != searchCandidate.ToLower())
                            {
                                continue;
                            }

                            // check the returned game is on the right platform
                            AttributeItem platformAttribute = dataObjectItem.Attributes.Find(x => x.attributeName == AttributeItem.AttributeName.Platform);
                            if (platformAttribute != null)
                            {
                                RelationItem platformRelation = (RelationItem)platformAttribute.Value;
                                if (platformRelation.relationType == DataObjects.DataObjectType.Platform && platformRelation.relationId == platformDataObject.Id)
                                {
                                    // add the manual to the game
                                    AttributeItem manualAttribute = new AttributeItem()
                                    {
                                        attributeName = AttributeItem.AttributeName.VIMMManualId,
                                        attributeType = AttributeItem.AttributeType.ShortString,
                                        attributeRelationType = DataObjects.DataObjectType.Game,
                                        Value = manual.id
                                    };
                                    DataObjects.AddAttribute(dataObjectItem.Id, manualAttribute);

                                    found = true;
                                }
                            }
                        }

                        if (found == true)
                        {
                            break;
                        }
                    }
                }
            }
        }
    }
}