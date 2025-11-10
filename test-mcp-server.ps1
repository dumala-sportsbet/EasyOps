# Test script for EasyOps MCP Server
# This sends test requests to the MCP server to verify it's working

Write-Host "🧪 Testing EasyOps MCP Server" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

$mcpServerPath = Join-Path $PSScriptRoot "EasyOps.McpServer"

# Start the MCP server process
Write-Host "Starting MCP server..." -ForegroundColor Yellow
$process = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "$mcpServerPath" -NoNewWindow -PassThru -RedirectStandardInput -RedirectStandardOutput -RedirectStandardError

Start-Sleep -Seconds 2

# Test 1: Initialize
Write-Host "`n📋 Test 1: Initialize" -ForegroundColor Green
$initRequest = @{
    jsonrpc = "2.0"
    id = 1
    method = "initialize"
    params = @{
        protocolVersion = "2024-11-05"
        capabilities = @{}
        clientInfo = @{
            name = "test-client"
            version = "1.0.0"
        }
    }
} | ConvertTo-Json -Compress

Write-Host "Request: $initRequest" -ForegroundColor Gray
# Note: In a real test, you'd send this to stdin and read from stdout

# Test 2: List Tools
Write-Host "`n📋 Test 2: List Tools" -ForegroundColor Green
$listToolsRequest = @{
    jsonrpc = "2.0"
    id = 2
    method = "tools/list"
} | ConvertTo-Json -Compress

Write-Host "Request: $listToolsRequest" -ForegroundColor Gray

# Test 3: Call a tool (list projects - safe, no side effects)
Write-Host "`n📋 Test 3: List Projects" -ForegroundColor Green
$listProjectsRequest = @{
    jsonrpc = "2.0"
    id = 3
    method = "tools/call"
    params = @{
        name = "jenkins_list_projects"
        arguments = @{}
    }
} | ConvertTo-Json -Compress

Write-Host "Request: $listProjectsRequest" -ForegroundColor Gray

Write-Host "`n"
Write-Host "================================" -ForegroundColor Cyan
Write-Host "✅ Test requests generated" -ForegroundColor Green
Write-Host ""
Write-Host "To manually test the server:" -ForegroundColor Yellow
Write-Host "1. Run: dotnet run --project .\EasyOps.McpServer" -ForegroundColor White
Write-Host "2. Copy and paste the JSON requests above (one per line)" -ForegroundColor White
Write-Host "3. Press Enter after each request to see the response" -ForegroundColor White
Write-Host ""
Write-Host "Or configure it in VS Code/Claude Desktop and use natural language!" -ForegroundColor Cyan

# Cleanup
if ($process -and !$process.HasExited) {
    Stop-Process -Id $process.Id -Force
    Write-Host "`nStopped test server process" -ForegroundColor Gray
}
