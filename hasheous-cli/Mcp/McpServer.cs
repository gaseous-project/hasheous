using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Classes;
using Classes.Mcp;

namespace hasheous_cli.Mcp;

public static class McpServer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static async Task RunAsync(Database db)
    {
        Stream input = Console.OpenStandardInput();
        Stream output = Console.OpenStandardOutput();

        while (true)
        {
            string? message = ReadMessage(input);
            if (message == null)
            {
                return;
            }

            JsonNode? node;
            try
            {
                node = JsonNode.Parse(message);
            }
            catch
            {
                WriteResponse(output, McpRequestProcessor.BuildErrorResponse(null, -32700, "Parse error"));
                continue;
            }

            if (node is not JsonObject request)
            {
                WriteResponse(output, McpRequestProcessor.BuildErrorResponse(null, -32600, "Invalid Request"));
                continue;
            }

            JsonObject? response = await McpRequestProcessor.ProcessRequestAsync(db, request);
            if (response != null)
            {
                WriteResponse(output, response);
            }
        }
    }

    private static string? ReadMessage(Stream input)
    {
        int contentLength = 0;
        bool hasHeaders = false;

        while (true)
        {
            string? line = ReadAsciiLine(input);
            if (line == null)
            {
                return null;
            }

            if (line.Length == 0)
            {
                break;
            }

            hasHeaders = true;

            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                string lengthString = line.Substring("Content-Length:".Length).Trim();
                if (!int.TryParse(lengthString, out contentLength) || contentLength < 0)
                {
                    return null;
                }
            }
        }

        if (!hasHeaders || contentLength == 0)
        {
            return null;
        }

        byte[] payloadBytes = new byte[contentLength];
        int read = 0;
        while (read < contentLength)
        {
            int bytesRead = input.Read(payloadBytes, read, contentLength - read);
            if (bytesRead <= 0)
            {
                return null;
            }

            read += bytesRead;
        }

        return Encoding.UTF8.GetString(payloadBytes);
    }

    private static string? ReadAsciiLine(Stream input)
    {
        List<byte> buffer = new List<byte>();
        while (true)
        {
            int value = input.ReadByte();
            if (value == -1)
            {
                if (buffer.Count == 0)
                {
                    return null;
                }

                break;
            }

            if (value == '\n')
            {
                break;
            }

            if (value != '\r')
            {
                buffer.Add((byte)value);
            }
        }

        return Encoding.ASCII.GetString(buffer.ToArray());
    }

    private static void WriteResponse(Stream output, JsonObject response)
    {
        string json = response.ToJsonString(JsonOptions);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(json);
        byte[] headerBytes = Encoding.ASCII.GetBytes($"Content-Length: {bodyBytes.Length}\r\n\r\n");

        output.Write(headerBytes, 0, headerBytes.Length);
        output.Write(bodyBytes, 0, bodyBytes.Length);
        output.Flush();
    }
}
