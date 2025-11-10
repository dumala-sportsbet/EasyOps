using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace EasyOps.McpServer.Mcp;

public class McpServerHandler
{
    private readonly JenkinsTools _jenkinsTools;
    private readonly ILogger<McpServerHandler> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServerHandler(JenkinsTools jenkinsTools, ILogger<McpServerHandler> logger)
    {
        _jenkinsTools = jenkinsTools;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MCP Server started. Waiting for requests...");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await Console.In.ReadLineAsync(cancellationToken);
                if (line == null) break; // EOF

                if (string.IsNullOrWhiteSpace(line)) continue;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessRequestAsync(line);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing request");
                    }
                }, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MCP Server shutting down...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in MCP server");
        }
    }

    private async Task ProcessRequestAsync(string requestJson)
    {
        McpResponse response;

        try
        {
            var request = JsonSerializer.Deserialize<McpRequest>(requestJson, _jsonOptions);
            if (request == null)
            {
                response = CreateErrorResponse(null, -32700, "Parse error");
            }
            else
            {
                response = await HandleRequestAsync(request);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parse error");
            response = CreateErrorResponse(null, -32700, "Parse error: " + ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error");
            response = CreateErrorResponse(null, -32603, "Internal error: " + ex.Message);
        }

        var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
        await Console.Out.WriteLineAsync(responseJson);
        await Console.Out.FlushAsync();
    }

    private async Task<McpResponse> HandleRequestAsync(McpRequest request)
    {
        _logger.LogInformation($"Handling method: {request.Method}");

        try
        {
            switch (request.Method)
            {
                case "initialize":
                    return CreateSuccessResponse(request.Id, new InitializeResult
                    {
                        ProtocolVersion = "2024-11-05",
                        Capabilities = new ServerCapabilities
                        {
                            Tools = new ToolsCapability()
                        },
                        ServerInfo = new ServerInfo
                        {
                            Name = "EasyOps Jenkins MCP Server",
                            Version = "1.0.0"
                        }
                    });

                case "initialized":
                    // Notification - no response needed, but return empty success
                    return CreateSuccessResponse(request.Id, new { });

                case "tools/list":
                    var tools = _jenkinsTools.GetTools();
                    return CreateSuccessResponse(request.Id, new ToolsList { Tools = tools });

                case "tools/call":
                    if (request.Params == null)
                    {
                        return CreateErrorResponse(request.Id, -32602, "Missing params");
                    }

                    var callParams = request.Params.Value;
                    var toolName = callParams.TryGetProperty("name", out var nameElement)
                        ? nameElement.GetString()
                        : null;

                    if (string.IsNullOrEmpty(toolName))
                    {
                        return CreateErrorResponse(request.Id, -32602, "Missing tool name");
                    }

                    JsonElement? arguments = null;
                    if (callParams.TryGetProperty("arguments", out var argsElement))
                    {
                        arguments = argsElement;
                    }

                    var result = await _jenkinsTools.ExecuteTool(toolName, arguments);
                    return CreateSuccessResponse(request.Id, result);

                case "ping":
                    return CreateSuccessResponse(request.Id, new { });

                default:
                    _logger.LogWarning($"Unknown method: {request.Method}");
                    return CreateErrorResponse(request.Id, -32601, $"Method not found: {request.Method}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error handling request method: {request.Method}");
            return CreateErrorResponse(request.Id, -32603, $"Internal error: {ex.Message}");
        }
    }

    private McpResponse CreateSuccessResponse(object? id, object result)
    {
        return new McpResponse
        {
            Id = id,
            Result = result
        };
    }

    private McpResponse CreateErrorResponse(object? id, int code, string message, object? data = null)
    {
        return new McpResponse
        {
            Id = id,
            Error = new McpError
            {
                Code = code,
                Message = message,
                Data = data
            }
        };
    }
}
