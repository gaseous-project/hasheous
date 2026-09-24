using System.IO;

namespace hasheous_lib.Tests;

public class FrontendDataObjectsListTests
{
    [Fact]
    public void GameListRequestsLightweightPayload()
    {
        string scriptPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "hasheous", "wwwroot", "scripts", "dataobjects.js");
        string fileContents = File.ReadAllText(Path.GetFullPath(scriptPath));
    }
}
