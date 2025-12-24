using System.Diagnostics;
using System.Net;
using hasheous_taskrunner.Classes;
using hasheous_taskrunner.Classes.Communication;

// Load configuration
Config.LoadConfiguration();

// Register the task runner with the service host
await hasheous_taskrunner.Classes.Communication.Registration.Initialize(Config.RegistrationParameters);

if (hasheous_taskrunner.Classes.Communication.Common.IsRegistered())
{
    Console.WriteLine("");
    Console.WriteLine("Task worker is now registered and ready to receive tasks.");

    // Start the task processing loop
    Console.WriteLine("");
    Console.WriteLine("Starting task processing loop... (press Ctrl+C to exit)");

    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (sender, e) =>
    {
        e.Cancel = true;  // Prevent immediate termination
        cts.Cancel();     // Signal the loop to break
    };

    // Main processing loop
    while (!cts.Token.IsCancellationRequested)
    {
        // Send heartbeat if due
        await hasheous_taskrunner.Classes.Communication.Heartbeat.SendHeartbeatIfDue();

        // Wait before next iteration
        await Task.Delay(5000);
    }

    // Cleanup: OS task kill commands
    Console.WriteLine("Cancellation requested. Cleaning up...");

    if (OperatingSystem.IsWindows())
    {
        // Windows: kill any spawned processes (example: taskkill /F /IM processname.exe)
        // Customize with actual process names or PIDs as needed
    }
    else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
    {
        // Linux/macOS: kill any spawned processes (example: kill -9 <PID>)
        // Customize with actual PIDs as needed
    }

    Console.WriteLine("Task worker is shutting down...");
    await hasheous_taskrunner.Classes.Communication.Registration.Unregister();
}
else
{
    Console.WriteLine("Task worker registration failed. Exiting.");
    return;
}

// Keep the console window open
Console.WriteLine("Task worker has stopped.");
Console.WriteLine("Press any key to exit...");
Console.ReadKey();