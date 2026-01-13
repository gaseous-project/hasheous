using hasheous_taskrunner.Classes.Tasks;

namespace hasheous_taskrunner.Classes.Communication
{
    /// <summary>
    /// Container for task-related communication helpers used by the task runner.
    /// </summary>
    public static class Tasks
    {
        private static DateTime lastTaskFetch = DateTime.MinValue;
        private static readonly TimeSpan taskFetchInterval = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Indicates whether a task is currently being executed.
        /// </summary>
        public static bool IsRunningTask { get; set; } = false;

        /// <summary>
        /// Fetches and executes tasks from the configured host if the fetch interval has elapsed.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task FetchAndExecuteTasksIfDue(CancellationToken cancellationToken = default)
        {
            if (DateTime.UtcNow - lastTaskFetch >= taskFetchInterval)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IsRunningTask = true;
                string fetchTasksUrl = $"{Config.BaseUriPath}/clients/{Config.GetAuthValue("client_id")}/job";
                try
                {
                    var job = await Common.Get<TaskItem>(fetchTasksUrl);
                    if (job != null)
                    {
                        Console.WriteLine($"Fetched task ID {job.Id} of type {job.TaskName}.");

                        // find the appropriate task handler based on job.TaskName = ITask.TaskType
                        var taskType = typeof(ITask);
                        var taskHandlers = AppDomain.CurrentDomain.GetAssemblies()
                            .SelectMany(s => s.GetTypes())
                            .Where(p => taskType.IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract);

                        ITask? handler = null;
                        foreach (var handlerType in taskHandlers)
                        {
                            var instance = Activator.CreateInstance(handlerType) as ITask;
                            if (instance?.TaskType == job.TaskName)
                            {
                                handler = instance;
                                break;
                            }
                        }

                        if (handler == null)
                        {
                            throw new InvalidOperationException($"No task handler found for task type: {job.TaskName}");
                        }

                        // verify the task
                        Console.Write($"Verifying task ID {job.Id}...");
                        TaskVerificationResult verificationResult = await handler.VerifyAsync(job.Parameters, cancellationToken);
                        Console.WriteLine($" {verificationResult.Status}");

                        // acknowledge receipt of the task
                        string acknowledgeUrl = $"{Config.BaseUriPath}/clients/{Config.GetAuthValue("client_id")}/job";
                        Dictionary<string, object> ackPayload = new Dictionary<string, object>
                        {
                            { "task_id", job.Id }
                        };
                        if (verificationResult.Status == TaskVerificationResult.VerificationStatus.Success)
                        {
                            ackPayload["status"] = QueueItemStatus.InProgress.ToString();
                            ackPayload["result"] = "";
                            ackPayload["error_message"] = "";
                        }
                        else
                        {
                            ackPayload["status"] = QueueItemStatus.Failed.ToString();
                            ackPayload["result"] = "";
                            ackPayload["error_message"] = verificationResult.Details;
                        }
                        Console.Write($"Acknowledging task ID {job.Id} with status {ackPayload["status"]}...");
                        await Common.Post<object>(acknowledgeUrl, ackPayload);
                        Console.WriteLine(" done.");

                        // if verification failed, do not execute
                        if (verificationResult.Status != TaskVerificationResult.VerificationStatus.Success)
                        {
                            lastTaskFetch = DateTime.UtcNow;
                            IsRunningTask = false;
                            Console.WriteLine($"Skipping execution of task ID {job.Id} due to verification failure.");
                            return;
                        }

                        // execute the task
                        try
                        {
                            Console.WriteLine($"Executing task ID {job.Id}...");
                            Dictionary<string, object> executionResult = await handler.ExecuteAsync(job.Parameters, cancellationToken);
                            // report task completion
                            ackPayload["status"] = QueueItemStatus.Submitted.ToString();
                            ackPayload["result"] = executionResult.ContainsKey("response") ? executionResult["response"] : "";
                            ackPayload["error_message"] = executionResult.ContainsKey("error") ? executionResult["error"] : "";
                            Console.WriteLine($"Task ID {job.Id} complete.");
                        }
                        catch (Exception execEx)
                        {
                            // report task failure
                            ackPayload["status"] = QueueItemStatus.Failed.ToString();
                            ackPayload["result"] = "";
                            ackPayload["error_message"] = execEx.Message;
                            Console.WriteLine($" failed: {execEx.Message}");
                        }
                        Console.WriteLine($"Reporting completion of task ID {job.Id} with status {ackPayload["status"]}...");
                        await Common.Post<object>(acknowledgeUrl, ackPayload);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to fetch tasks: {ex.Message}");
                }
                lastTaskFetch = DateTime.UtcNow;
                IsRunningTask = false;
                Console.WriteLine("Task processing cycle complete. Waiting for next interval.");
            }
        }
    }
}