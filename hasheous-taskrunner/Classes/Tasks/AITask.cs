using hasheous_taskrunner.Classes.Capabilities;

namespace hasheous_taskrunner.Classes.Tasks
{
    /// <summary>
    /// Represents an AI task that can be executed by the task runner.
    /// </summary>
    public class AITask : ITask
    {

        /// <inheritdoc/>
        public TaskType TaskType => TaskType.AIDescriptionAndTagging;

        /// <inheritdoc/>
        public async Task<TaskVerificationResult> VerifyAsync(Dictionary<string, string>? parameters, CancellationToken cancellationToken)
        {
            // check for required parameters - model and prompt
            TaskVerificationResult verificationResults = new TaskVerificationResult();

            if (parameters == null)
            {
                verificationResults.Status = TaskVerificationResult.VerificationStatus.Failure;
                verificationResults.Details.Add("parameters", "Parameters cannot be null.");
                return await Task.FromResult(verificationResults);
            }

            if (!parameters.ContainsKey("model_description") && !parameters.ContainsKey("model_tags"))
            {
                verificationResults.Details.Add("model", "Missing required parameter: model_description or model_tags");
                verificationResults.Status = TaskVerificationResult.VerificationStatus.Failure;
            }

            if (!parameters.ContainsKey("prompt_description") && !parameters.ContainsKey("prompt_tags"))
            {
                verificationResults.Details.Add("prompt", "Missing required parameter: prompt_description or prompt_tags");
                verificationResults.Status = TaskVerificationResult.VerificationStatus.Failure;
            }

            return await Task.FromResult(verificationResults);
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, object>> ExecuteAsync(Dictionary<string, string>? parameters, CancellationToken cancellationToken)
        {
            // use the model and prompt parameters to call Ollama API
            var ai = Classes.Capabilities.Capabilities.GetCapabilityById<ICapability>(20); // AI capability
            if (ai == null)
            {
                throw new InvalidOperationException("AI capability is not available.");
            }

            // get sources from parameters
            List<string> sources = new List<string>();
            foreach (string sourceKey in parameters["sources"].Split(';'))
            {
                if (parameters.ContainsKey("Source_" + sourceKey.Trim()))
                {
                    sources.Add("# " + sourceKey.Trim() + "\n\n" + parameters["Source_" + sourceKey.Trim()] + "\n\n");
                }
            }

            // override model to use ollama
            string modelDescriptionOverride = "gemma3:12b";
            string modelTagOverride = "gemma3:12b";
            bool applyDescriptionOverride = true;
            bool applyTagOverride = true;
            if (applyDescriptionOverride == false)
            {
                modelDescriptionOverride = parameters != null && parameters.ContainsKey("prompt_description") ? parameters["prompt_description"] : "";
            }
            if (applyTagOverride == false)
            {
                modelTagOverride = parameters != null && parameters.ContainsKey("prompt_tags") ? parameters["prompt_tags"] : "";
            }

            // generate the description
            var descriptionResult = await ai.ExecuteAsync(new Dictionary<string, object>
            {
                { "model", modelDescriptionOverride },
                { "prompt", parameters != null && parameters.ContainsKey("prompt_description") ? parameters["prompt_description"] : "" },
                { "embeddings", sources }
            });

            var tagsResult = await ai.ExecuteAsync(new Dictionary<string, object>
            {
                { "model", modelTagOverride },
                { "prompt", parameters != null && parameters.ContainsKey("prompt_tags") ? parameters["prompt_tags"] : "" },
                { "embeddings", sources }
            });

            Dictionary<string, object> response = new Dictionary<string, object>();
            if (descriptionResult == null || tagsResult == null)
            {
                response = new Dictionary<string, object>
                {
                    { "result", false },
                    { "error", "No response from AI capability." }
                };
            }

            // merge results
            Dictionary<string, object> responseVars = new Dictionary<string, object>();
            if (descriptionResult != null && descriptionResult.ContainsKey("result") && (bool)descriptionResult["result"])
            {
                responseVars["description"] = descriptionResult.ContainsKey("response") ? descriptionResult["response"] : "";
            }
            else
            {
                responseVars["description"] = "";
            }
            if (tagsResult != null && tagsResult.ContainsKey("result") && (bool)tagsResult["result"])
            {
                responseVars["tags"] = tagsResult.ContainsKey("response") ? tagsResult["response"].ToString() : "";
                responseVars["tags"] = ollamaPrune(responseVars["tags"].ToString());
                // deserialise tags into a dictionary<string, string[]> if possible
                try
                {
                    var deserializedTags = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string[]>>(responseVars["tags"].ToString() ?? "");
                    if (deserializedTags != null)
                    {
                        responseVars["tags"] = deserializedTags;
                    }
                }
                catch
                {
                    // ignore deserialization errors
                }
            }
            else
            {
                responseVars["tags"] = "";
            }
            response = new Dictionary<string, object>
            {
                { "result", true },
                { "response", responseVars }
            };

            if (response.ContainsKey("result") && response["result"] is bool resultBool && resultBool)
            {
                // success
                // ollama sometimes returns the response wrapped in markdown code blocks, so strip those if present
                if (response.ContainsKey("response") && response["response"] is string respStr)
                {
                    respStr = respStr.Trim();
                    if ((respStr.StartsWith("```") || respStr.StartsWith("```json")) && respStr.EndsWith("```"))
                    {
                        int firstLineEnd = respStr.IndexOf('\n');
                        int lastLineStart = respStr.LastIndexOf("```");
                        if (firstLineEnd >= 0 && lastLineStart > firstLineEnd)
                        {
                            respStr = respStr.Substring(firstLineEnd + 1, lastLineStart - firstLineEnd - 1).Trim();
                        }
                    }
                    response["response"] = respStr;
                }
            }
            else
            {
                // failure
                response.Add("result", "");
                response.Add("error", response.ContainsKey("error") ? response["error"] : "Unknown error from AI capability.");
            }

            return await Task.FromResult(response ?? new Dictionary<string, object>());
        }

        private string ollamaPrune(string input)
        {
            input = input.Trim();
            if ((input.StartsWith("```") || input.StartsWith("```json")) && input.EndsWith("```"))
            {
                int firstLineEnd = input.IndexOf('\n');
                int lastLineStart = input.LastIndexOf("```");
                if (firstLineEnd >= 0 && lastLineStart > firstLineEnd)
                {
                    input = input.Substring(firstLineEnd + 1, lastLineStart - firstLineEnd - 1).Trim();
                }
            }
            return input;
        }
    }
}