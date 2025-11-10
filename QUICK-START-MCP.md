# 🚀 Quick Start - EasyOps MCP Server

## One-Time Setup (5 minutes)

### 1️⃣ Add Your Jenkins Credentials
Edit: `EasyOps.McpServer\appsettings.json`
```json
{
  "Jenkins": {
    "Username": "your.username",
    "ApiToken": "get-from-jenkins-settings"
  }
}
```

### 2️⃣ Build the Server
```powershell
cd "c:\Users\dumala\OneDrive - Sportsbet\Documents\Visual Studio 2022\Projects\EasyOps"
dotnet build EasyOps.McpServer
```

### 3️⃣ Configure VS Code
Add to VS Code settings (Ctrl+Shift+P → "Preferences: Open User Settings (JSON)"):
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

### 4️⃣ Restart VS Code
Close and reopen VS Code to load the MCP server.

## Usage - Just Talk to Copilot! 💬

### Build Commands
```
"Build AFL pricer"
"Rebuild RL orchestrator on the develop branch"
"Build sb-rtp-sports-afl-pricer"
```

### Status Commands
```
"What's the status of AFL pricer?"
"Check the build status of RL orchestrator"
"Is the AFL pricer build done?"
```

### Deploy Commands
```
"Deploy AFL pricer version 1.2.3 to dev"
"Deploy RL orchestrator 2.0.0 to dev and stg"
```

### List Commands
```
"What Jenkins projects are available?"
"Show me all the projects"
"List Jenkins projects"
```

## Friendly Names You Can Use

| Say This | Builds This |
|----------|-------------|
| AFL pricer | sb-rtp-sports-afl-pricer |
| AFL orchestrator | sb-rtp-sports-afl-orchestrator |
| RL pricer | sb-rtp-sports-rl-pricer |
| RL orchestrator | sb-rtp-sports-rl-orchestrator |

Or use the exact Jenkins project name.

## Troubleshooting

### "MCP server not found"
- Restart VS Code
- Check the path in settings.json is correct
- Verify .NET 9.0 is installed: `dotnet --version`

### "Authentication failed"
- Check your Jenkins username/API token in appsettings.json
- Get a new API token from Jenkins → Your Name → Configure → API Token

### "Project not found"
- Use exact Jenkins name or add mapping in `EasyOps.Shared/Services/JenkinsService.cs`
- Try: "List all projects" to see available options

## Next Steps

📖 Full documentation: [MCP-SERVER-GUIDE.md](MCP-SERVER-GUIDE.md)  
🔧 Implementation details: [MCP-IMPLEMENTATION-SUMMARY.md](MCP-IMPLEMENTATION-SUMMARY.md)  
💻 Developer reference: [EasyOps.McpServer/README.md](EasyOps.McpServer/README.md)

## That's It! 🎉

Just talk to Copilot naturally and it will use the MCP server to control Jenkins for you!
