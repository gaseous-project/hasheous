namespace hasheous_taskrunner.Classes.Capabilities
{
    /// <summary>
    /// Manages capability checks and testing for the task runner.
    /// </summary>
    public class Capabilities
    {
        /// <summary>
        /// Maps capability IDs to their descriptive names.
        /// </summary>
        public static readonly Dictionary<int, string> CapabilityNames = new Dictionary<int, string>
        {
            { 0, "Internet" },
            { 10, "DiskSpace" },
            { 20, "AI" }
        };

        /// <summary>
        /// Checks the provided capabilities, internal capabilities, and returns their statuses.
        /// </summary>
        /// <param name="capabilitiesToCheck">A dictionary where keys are capability IDs and values are configuration dictionaries.</param>
        /// <returns>A list of capability names that passed the checks.</returns>
        public async static Task<List<int>> CheckCapabilitiesAsync(Dictionary<string, object> capabilitiesToCheck)
        {
            List<int> results = new List<int>();

            // get all available capability types that implement ICapability
            List<Type> availableCapabilityTypes = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(assembly => assembly.GetTypes())
                        .Where(type => typeof(ICapability).IsAssignableFrom(type) &&
                                      type.IsClass &&
                                      !type.IsAbstract &&
                                      type.Namespace == "hasheous_taskrunner.Classes.Capabilities")
                        .ToList();

            // check internal capabilities first
            Console.WriteLine("Checking internal capabilities...");
            foreach (var capabilityType in availableCapabilityTypes)
            {
                // create instance only when needed
                var capability = (ICapability?)Activator.CreateInstance(capabilityType);
                if (capability == null || !capability.IsInternalCapability)
                {
                    continue;
                }

                if (capabilitiesToCheck.ContainsKey(capability.CapabilityId.ToString()))
                {
                    Console.WriteLine($"Checking capability: {CapabilityNames[capability.CapabilityId]}");
                    // test capability
                    bool testResult = await capability.TestAsync();
                    if (testResult)
                    {
                        Console.WriteLine($"Capability {CapabilityNames[capability.CapabilityId]} passed.");
                        results.Add(capability.CapabilityId);
                    }
                }
            }

            // check requested capabilities - can be internal or external
            Console.WriteLine("Checking requested capabilities...");
            foreach (var capabilityEntry in capabilitiesToCheck)
            {
                int? capabilityId = null;
                try
                {
                    // search CapabilityNames to get the ID
                    foreach (var kvp in CapabilityNames)
                    {
                        if (kvp.Value.Equals(capabilityEntry.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            capabilityId = kvp.Key;
                            break;
                        }
                    }
                }
                catch
                {
                    continue; // skip invalid keys
                }

                if (capabilityId == null)
                {
                    continue; // skip if capability ID not found
                }

                // find the matching capability type and create instance only when found
                ICapability? capability = null;
                foreach (var capabilityType in availableCapabilityTypes)
                {
                    var tempCapability = (ICapability?)Activator.CreateInstance(capabilityType);
                    if (tempCapability != null && tempCapability.CapabilityId == capabilityId)
                    {
                        capability = tempCapability;
                        break;
                    }
                }

                if (capability != null)
                {
                    Console.WriteLine($"Checking capability: {CapabilityNames[capability.CapabilityId]}");

                    // set configuration if provided
                    {
                        Dictionary<string, object>? configDict = null;
                        try
                        {
                            if (capabilityEntry.Value is Dictionary<string, object> direct)
                            {
                                configDict = direct;
                            }
                            else if (capabilityEntry.Value is string s)
                            {
                                if (!string.IsNullOrWhiteSpace(s))
                                {
                                    configDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(s);
                                }
                            }
                            else if (capabilityEntry.Value is System.Text.Json.JsonElement je)
                            {
                                if (je.ValueKind != System.Text.Json.JsonValueKind.Null &&
                                    je.ValueKind != System.Text.Json.JsonValueKind.Undefined)
                                {
                                    configDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(je.GetRawText());
                                }
                            }
                            // null or empty config is fine; leave configDict as null
                        }
                        catch
                        {
                            // deserialization error: skip this capability
                            Console.WriteLine($"Failed to parse configuration for capability {CapabilityNames[capability.CapabilityId]}, skipping...");
                            continue;
                        }
                        capability.Configuration = configDict;
                    }

                    // test capability
                    bool testResult = await capability.TestAsync();
                    if (testResult)
                    {
                        Console.WriteLine($"Capability {CapabilityNames[capability.CapabilityId]} passed.");
                        results.Add(capability.CapabilityId);
                    }
                }
            }

            return results;
        }
    }
}