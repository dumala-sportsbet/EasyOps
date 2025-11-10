# EasyOps Jenkins MCP Server

## Overview

The EasyOps Jenkins MCP Server enables you to control Jenkins builds and deployments using natural language through any MCP-compatible client (like GitHub Copilot, Claude Desktop, etc.).

## Features

✅ **Build Projects** - Trigger Jenkins builds using simple commands like "build AFL pricer"  
✅ **Deploy Projects** - Deploy specific versions to dev/stg/prd environments  
✅ **Check Build Status** - Get real-time status of your builds  
✅ **List Projects** - View all available Jenkins projects  
✅ **Natural Language** - Use friendly names like "AFL pricer" instead of "sb-rtp-sports-afl-pricer"  

## Setup Instructions

### 1. Configure Jenkins Credentials

Edit `EasyOps.McpServer\appsettings.json` and add your Jenkins credentials:

```json
{
  "Jenkins": {
    "BaseUrl": "https://jenkins.int.ts.dev.sbet.cloud",
    "Username": "your.username",
    "ApiToken": "your-jenkins-api-token"
  }
}
```

**How to get your Jenkins API token:**
1. Go to Jenkins → Click your name (top right) → Configure
2. Under "API Token", click "Add new Token"
3. Give it a name and click "Generate"
4. Copy the token and paste it in appsettings.json

### 2. Build the MCP Server

```powershell
cd "c:\Users\dumala\OneDrive - Sportsbet\Documents\Visual Studio 2022\Projects\EasyOps"
dotnet build EasyOps.McpServer
```

### 3. Configure Your MCP Client

#### For VS Code with GitHub Copilot

Add to your VS Code settings (`settings.json`):

```json
{
  "github.copilot.advanced": {
    "mcpServers": {
      "easyops-jenkins": {
        "command": "dotnet",
        "args": [
          "run",
          "--project",
          "c:\\Users\\dumala\\OneDrive - Sportsbet\\Documents\\Visual Studio 2022\\Projects\\EasyOps\\EasyOps.McpServer\\EasyOps.McpServer.csproj"
        ]
      }
    }
  }
}
```

#### For Claude Desktop

Add to your Claude Desktop config (`%APPDATA%\Claude\claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "easyops-jenkins": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "c:\\Users\\dumala\\OneDrive - Sportsbet\\Documents\\Visual Studio 2022\\Projects\\EasyOps\\EasyOps.McpServer\\EasyOps.McpServer.csproj"
      ]
    }
  }
}
```

### 4. Restart Your MCP Client

After adding the configuration, restart VS Code or Claude Desktop to load the MCP server.

## Usage Examples

Once configured, you can use natural language commands in your MCP client:

### Build a Project
```
"Build AFL pricer"
"Build the RL orchestrator on the develop branch"
"Rebuild sb-rtp-sports-afl-pricer"
```

### Check Build Status
```
"What's the status of AFL pricer?"
"Check the build status of RL orchestrator"
```

### Deploy a Project
```
"Deploy AFL pricer version 1.2.3 to dev"
"Deploy RL orchestrator version 2.0.0 to dev and stg"
```

### List Available Projects
```
"What Jenkins projects are available?"
"Show me all the projects"
```

## Available MCP Tools

The server exposes the following tools:

1. **jenkins_build_project** - Build a Jenkins project
   - Parameters: `project` (required), `branch` (default: "develop"), `monorepo` (optional)

2. **jenkins_get_build_status** - Get build status
   - Parameters: `project` (required), `branch` (default: "develop"), `monorepo` (optional)

3. **jenkins_list_projects** - List all available projects
   - No parameters required

4. **jenkins_deploy_project** - Deploy a project
   - Parameters: `project`, `version`, `branch`, `deployToDev`, `deployToStg`, `deployToPrd`, `changeDescription`

## Project Name Mappings

The server includes friendly name mappings for easier commands:

| Friendly Name | Jenkins Project Name | Monorepo |
|--------------|---------------------|----------|
| AFL Pricer | sb-rtp-sports-afl-pricer | sb-rtp-sports-afl |
| AFL Orchestrator | sb-rtp-sports-afl-orchestrator | sb-rtp-sports-afl |
| RL Pricer | sb-rtp-sports-rl-pricer | sb-rtp-sports-rl |
| RL Orchestrator | sb-rtp-sports-rl-orchestrator | sb-rtp-sports-rl |

You can add more mappings in `EasyOps.Shared\Services\JenkinsService.cs` in the `InitializeProjectMappings()` method.

## Troubleshooting

### Server Not Starting
- Check that .NET 9.0 SDK is installed: `dotnet --version`
- Verify the appsettings.json file exists and has valid JSON
- Check the MCP server logs (stderr output)

### Authentication Errors
- Verify your Jenkins username and API token are correct
- Make sure the API token has the necessary permissions
- Test your credentials by logging into Jenkins web UI

### Build Commands Not Working
- Ensure the project name exists in Jenkins
- Check the monorepo configuration
- Verify branch name is correct
- Look at the MCP server logs for detailed error messages

## Architecture

```
EasyOps Solution
├── EasyOps (ASP.NET Web App)
│   └── Your existing Jenkins web UI
├── EasyOps.Shared (Class Library)
│   ├── Models/ - Shared data models
│   └── Services/ - Jenkins service implementation
└── EasyOps.McpServer (Console App)
    ├── Mcp/ - MCP protocol implementation
    │   ├── McpProtocol.cs - Protocol models
    │   ├── McpServerHandler.cs - Request handler
    │   └── JenkinsTools.cs - Jenkins MCP tools
    └── Program.cs - Entry point
```

This architecture ensures:
- ✅ **Zero impact** on your existing web application
- ✅ **Code reuse** between web app and MCP server
- ✅ **Easy maintenance** - Jenkins logic in one place
- ✅ **Extensible** - Easy to add new tools and features

## Adding More Projects

To add more project mappings, edit `EasyOps.Shared\Services\JenkinsService.cs`:

```csharp
private Dictionary<string, ProjectMapping> InitializeProjectMappings()
{
    return new Dictionary<string, ProjectMapping>(StringComparer.OrdinalIgnoreCase)
    {
        // Existing mappings...
        
        ["your friendly name"] = new ProjectMapping
        {
            FriendlyName = "Your Friendly Name",
            JenkinsProjectName = "actual-jenkins-project-name",
            DefaultMonorepo = "monorepo-name",
            Description = "Description of the project"
        },
    };
}
```

Then rebuild: `dotnet build EasyOps.Shared`

## Support

For issues or questions:
1. Check the MCP server logs (stderr output)
2. Verify your appsettings.json configuration
3. Test Jenkins connectivity using the web app first
4. Check MCP client configuration

## Next Steps

- Add more Jenkins operations (restart build, view logs, etc.)
- Integrate with other systems (AWS, databases, etc.)
- Add more sophisticated natural language understanding
- Implement caching for faster responses
