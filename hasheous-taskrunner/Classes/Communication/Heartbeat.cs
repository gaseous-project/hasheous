namespace hasheous_taskrunner.Classes.Communication
{
    public static class Heartbeat
    {
        private static DateTime lastHeartbeatTime = DateTime.MinValue;
        private static readonly TimeSpan heartbeatInterval = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Sends a heartbeat signal to the host if the heartbeat interval has elapsed.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task SendHeartbeatIfDue()
        {
            if (DateTime.UtcNow - lastHeartbeatTime >= heartbeatInterval)
            {
                string heartbeatUrl = $"{Config.BaseUriPath}/clients/{Config.GetAuthValue("client_id")}/heartbeat";
                try
                {
                    await Common.Put<object>(heartbeatUrl, new { });
                    lastHeartbeatTime = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to send heartbeat: {ex.Message}");
                }
            }
        }
    }
}