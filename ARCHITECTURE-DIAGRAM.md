# EasyOps MCP Server Architecture

## System Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Your Workflow                                │
│                                                                       │
│  You type in VS Code:  "Build AFL pricer"                           │
│                              ↓                                        │
│                    GitHub Copilot Chat                               │
└────────────────────────────┬──────────────────────────────────────┘
                             │
                             │ Natural Language
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                   Model Context Protocol (MCP)                       │
│                                                                       │
│  AI interprets your request and calls the appropriate MCP tool:     │
│  tools/call → jenkins_build_project                                  │
│  {                                                                    │
│    "name": "jenkins_build_project",                                  │
│    "arguments": {                                                    │
│      "project": "AFL pricer",                                        │
│      "branch": "develop"                                             │
│    }                                                                  │
│  }                                                                    │
└────────────────────────────┬──────────────────────────────────────┘
                             │
                             │ JSON-RPC 2.0 over stdio
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│              EasyOps.McpServer (Console Application)                 │
│                                                                       │
│  ┌─────────────────────────────────────────────────────────┐        │
│  │  McpServerHandler                                        │        │
│  │  • Receives JSON-RPC requests on stdin                   │        │
│  │  • Routes to appropriate tool                            │        │
│  │  • Returns JSON-RPC responses on stdout                  │        │
│  └────────────────────┬────────────────────────────────────┘        │
│                       │                                               │
│                       ▼                                               │
│  ┌─────────────────────────────────────────────────────────┐        │
│  │  JenkinsTools                                            │        │
│  │  • jenkins_build_project    → Trigger builds             │        │
│  │  • jenkins_get_build_status → Check status               │        │
│  │  • jenkins_list_projects    → List all projects          │        │
│  │  • jenkins_deploy_project   → Deploy to environments     │        │
│  │                                                           │        │
│  │  • Resolves friendly names: "AFL pricer" →               │        │
│  │    "sb-rtp-sports-afl-pricer"                            │        │
│  └────────────────────┬────────────────────────────────────┘        │
└────────────────────────┼──────────────────────────────────────────┘
                         │
                         │ Uses shared service
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────────┐
│              EasyOps.Shared (Class Library)                          │
│                                                                       │
│  ┌─────────────────────────────────────────────────────────┐        │
│  │  JenkinsService (IJenkinsService)                       │        │
│  │  • ExecuteJobAsync()      - Trigger build/deploy        │        │
│  │  • GetBuildStatusAsync()  - Get build info              │        │
│  │  • GetProjectMappingsAsync() - Get friendly names       │        │
│  │                                                           │        │
│  │  Features:                                               │        │
│  │  • HTTP client with Basic Auth                           │        │
│  │  • Branch name encoding for Jenkins URLs                 │        │
│  │  • Project name resolution                               │        │
│  │  • Default monorepo detection                            │        │
│  └────────────────────┬────────────────────────────────────┘        │
└────────────────────────┼──────────────────────────────────────────┘
                         │
                         │ HTTP API calls
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────────┐
│                       Jenkins Server                                 │
│              https://jenkins.int.ts.dev.sbet.cloud                   │
│                                                                       │
│  /job/Sports/job/{monorepo}/job/{project}/                          │
│    job/build-pipeline/job/{branch}/build                             │
│                                                                       │
│  Executes the actual build/deployment                                │
└─────────────────────────────────────────────────────────────────────┘
```

## Project Structure

```
EasyOps/
│
├── EasyOps (Web Application) ─────────────────────┐
│   ├── Controllers/JenkinsController.cs           │ Existing
│   ├── Pages/Jenkins.cshtml                       │ web app
│   └── wwwroot/js/jenkins.js                      │ (untouched)
│                                                   │
├── EasyOps.Shared (Class Library) ────────────────┤
│   ├── Models/                                    │ New shared
│   │   └── JenkinsModels.cs                       │ code that
│   └── Services/                                  │ both projects
│       ├── IJenkinsService.cs                     │ can use
│       └── JenkinsService.cs                      │
│                                                   │
└── EasyOps.McpServer (Console App) ───────────────┤
    ├── Mcp/                                       │ New MCP
    │   ├── McpProtocol.cs    - Protocol models    │ server for
    │   ├── McpServerHandler.cs - Request handler  │ natural
    │   └── JenkinsTools.cs    - Jenkins tools     │ language
    ├── Program.cs             - Entry point       │ control
    ├── appsettings.json       - Configuration     │
    └── README.md              - Documentation     │
```

## Data Flow Example: "Build AFL pricer"

```
1. User Input
   ┌─────────────────────────────────────┐
   │  You type: "Build AFL pricer"       │
   └────────────┬────────────────────────┘
                │
2. AI Processing
   ┌────────────▼────────────────────────┐
   │  Copilot understands intent         │
   │  Selects tool: jenkins_build_project│
   │  Extracts args: {                   │
   │    project: "AFL pricer",           │
   │    branch: "develop"                │
   │  }                                   │
   └────────────┬────────────────────────┘
                │
3. MCP Protocol
   ┌────────────▼────────────────────────┐
   │  JSON-RPC Request                   │
   │  {                                   │
   │    "method": "tools/call",          │
   │    "params": {                       │
   │      "name": "jenkins_build_project",│
   │      "arguments": {...}             │
   │    }                                 │
   │  }                                   │
   └────────────┬────────────────────────┘
                │
4. MCP Server Processing
   ┌────────────▼────────────────────────┐
   │  JenkinsTools.BuildProject()        │
   │  • Resolves "AFL pricer" to         │
   │    "sb-rtp-sports-afl-pricer"       │
   │  • Detects monorepo:                │
   │    "sb-rtp-sports-afl"              │
   │  • Calls JenkinsService             │
   └────────────┬────────────────────────┘
                │
5. Jenkins Service
   ┌────────────▼────────────────────────┐
   │  JenkinsService.ExecuteJobAsync()   │
   │  • Builds Jenkins URL               │
   │  • Adds authentication              │
   │  • Makes HTTP POST request          │
   └────────────┬────────────────────────┘
                │
6. Jenkins Server
   ┌────────────▼────────────────────────┐
   │  Jenkins receives request           │
   │  • Queues build #42                 │
   │  • Returns 201 Created              │
   └────────────┬────────────────────────┘
                │
7. Response Back
   ┌────────────▼────────────────────────┐
   │  MCP Server formats response        │
   │  {                                   │
   │    "content": [{                     │
   │      "text": "✅ Successfully..."   │
   │      "Build #42"                     │
   │      "Job URL: https://..."         │
   │    }]                                │
   │  }                                   │
   └────────────┬────────────────────────┘
                │
8. Display to User
   ┌────────────▼────────────────────────┐
   │  Copilot shows you:                 │
   │  ✅ Successfully triggered build    │
   │  for 'AFL pricer' (branch: develop) │
   │                                      │
   │  Build #42                          │
   │  Job URL: https://jenkins.int...    │
   └─────────────────────────────────────┘
```

## Key Features

### 🎯 Natural Language → Structured Commands
- "Build AFL pricer" → `jenkins_build_project("sb-rtp-sports-afl-pricer", "develop")`
- "Check status" → `jenkins_get_build_status(...)`
- "Deploy to dev" → `jenkins_deploy_project(..., dev=true)`

### 🔄 Intelligent Name Resolution
```
Input: "AFL pricer"
  ↓
Lookup in mappings
  ↓
Output: project="sb-rtp-sports-afl-pricer"
        monorepo="sb-rtp-sports-afl"
```

### 🔐 Secure Authentication
```
appsettings.json credentials
  ↓
Base64 encoded
  ↓
Authorization: Basic {encoded}
  ↓
Jenkins API
```

### 🎨 Beautiful Responses
```
✅ Success indicators
📊 Status emojis
🔨 Building indicators
📋 Lists with formatting
```

## Extension Points

### Add New Tools
Edit `JenkinsTools.cs` → Add to `GetTools()` → Implement handler

### Add New Projects
Edit `JenkinsService.cs` → `InitializeProjectMappings()` → Add entry

### Add New Services
Create new tools class → Register in `Program.cs` → Expose as MCP tools

## Security Notes

- ✅ Credentials stored locally in appsettings.json
- ✅ Communication via stdin/stdout (local process only)
- ✅ No network exposure (MCP server doesn't listen on ports)
- ✅ Authentication passed through to Jenkins API
- ⚠️ Keep appsettings.json out of source control (add to .gitignore)
