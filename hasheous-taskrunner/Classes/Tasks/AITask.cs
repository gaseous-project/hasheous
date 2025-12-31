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

            if (!parameters.ContainsKey("model"))
            {
                verificationResults.Details.Add("model", "Missing required parameter: model");
                verificationResults.Status = TaskVerificationResult.VerificationStatus.Failure;
            }

            if (!parameters.ContainsKey("prompt"))
            {
                verificationResults.Details.Add("prompt", "Missing required parameter: prompt");
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

            var response = await ai.ExecuteAsync(parameters?.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value) ?? new Dictionary<string, object>());

            if (response == null)
            {
                response = new Dictionary<string, object>
                {
                    { "result", false },
                    { "error", "No response from AI capability." }
                };
            }

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
    }
}