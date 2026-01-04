namespace hasheous_taskrunner.Classes.Capabilities
{
    /// <summary>
    /// Interface for defining capabilities of a task worker.
    /// </summary>
    public interface ICapability
    {
        /// <summary>
        /// Gets the unique identifier for the capability.
        /// </summary>
        public int CapabilityId { get; }

        /// <summary>
        /// Indicates whether this is an internal capability. If false, it will only be called when requested by the server.
        /// </summary>
        public bool IsInternalCapability => false;

        /// <summary>
        /// Gets or sets the configuration dictionary for the capability.
        /// </summary>
        public Dictionary<string, object>? Configuration { get; set; }

        /// <summary>
        /// Tests the capability to ensure it is available.
        /// </summary>
        public Task<bool> TestAsync();

        /// <summary>
        /// Executes the capability with the provided parameters.
        /// </summary>
        /// <param name="parameters">A dictionary of parameters required for execution.</param>
        /// <returns>A task representing the asynchronous operation, with a result indicating success or failure.</returns>
        public Task<Dictionary<string, object>?> ExecuteAsync(Dictionary<string, object> parameters);
    }
}