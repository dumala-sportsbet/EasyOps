# EasyOps.McpServer

A Model Context Protocol (MCP) server that provides natural language control of Jenkins builds and deployments.

## Quick Start

1. **Configure your credentials** in `appsettings.json`:
   ```json
   {
     "Jenkins": {
       "Username": "your.username",
       "ApiToken": "your-jenkins-api-token"
     }
   }
   ```

2. **Build the server**:
   ```powershell
   dotnet build
   ```

3. **Test it manually**:
   ```powershell
   dotnet run
   ```
   Then type this JSON request:
   ```json
   {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0.0"}}}
   ```

4. **Configure in VS Code** - See [../MCP-SERVER-GUIDE.md](../MCP-SERVER-GUIDE.md)

## Available Commands

Once integrated with an MCP client (GitHub Copilot, Claude, etc.), you can use natural language:

- "Build AFL pricer"
- "Check the status of RL orchestrator"
- "Deploy AFL pricer version 1.2.3 to dev"
- "List all Jenkins projects"

## Project Structure

```
Mcp/
  ├── McpProtocol.cs       - MCP protocol data models (JSON-RPC 2.0)
  ├── McpServerHandler.cs  - Request routing and protocol handling
  └── JenkinsTools.cs      - Jenkins operation implementations
Program.cs                 - Application entry point and DI setup
appsettings.json          - Configuration (credentials, logging)
```

## How It Works

1. The server listens on **stdin** and responds on **stdout** (MCP stdio transport)
2. Requests use **JSON-RPC 2.0** protocol
3. The server exposes Jenkins operations as **MCP tools**
4. Natural language from the AI client is converted to structured tool calls
5. Tools execute Jenkins API calls and return results

## Development

### Adding New Tools

Edit `Mcp/JenkinsTools.cs`:

```csharp
public List<Tool> GetTools()
{
    return new List<Tool>
    {
        // Add your new tool here
        new Tool
        {
            Name = "jenkins_your_new_tool",
            Description = "What it does",
            InputSchema = JsonDocument.Parse(@"{ ... }").RootElement
        }
    };
}

// Then implement the handler
private async Task<CallToolResult> YourNewTool(JsonElement? arguments)
{
    // Your implementation
}
```

### Testing

Run the server and interact via stdin/stdout:

```powershell
dotnet run
```

Example request to list tools:
```json
{"jsonrpc":"2.0","id":2,"method":"tools/list"}
```

Example request to build a project:
```json
{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"jenkins_build_project","arguments":{"project":"AFL pricer","branch":"develop"}}}
```

## Logging

Logs are sent to **stderr** to keep stdout clean for MCP protocol. Configure in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "EasyOps.McpServer": "Debug"
    }
  }
}
```

## Dependencies

- .NET 9.0
- EasyOps.Shared (shared models and services)
- Microsoft.Extensions.* (DI, logging, configuration)

## See Also

- [MCP Server Guide](../MCP-SERVER-GUIDE.md) - Full setup and usage documentation
- [Model Context Protocol Spec](https://modelcontextprotocol.io/) - MCP protocol specification
