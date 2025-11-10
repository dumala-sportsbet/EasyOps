using System.Text.Json;
using EasyOps.Shared.Models;
using EasyOps.Shared.Services;
using Microsoft.Extensions.Logging;

namespace EasyOps.McpServer.Mcp;

public class JenkinsTools
{
    private readonly IJenkinsService _jenkinsService;
    private readonly ILogger<JenkinsTools> _logger;

    public JenkinsTools(IJenkinsService jenkinsService, ILogger<JenkinsTools> logger)
    {
        _jenkinsService = jenkinsService;
        _logger = logger;
    }

    public List<Tool> GetTools()
    {
        return new List<Tool>
        {
            new Tool
            {
                Name = "jenkins_build_project",
                Description = "Build a Jenkins project. Provide the exact project name from Jenkins. Default branch is 'develop' if not specified.",
                InputSchema = CreateBuildProjectSchema()
            },
            new Tool
            {
                Name = "jenkins_get_build_status",
                Description = "Get the status of the latest build for a Jenkins project",
                InputSchema = CreateBuildStatusSchema()
            },
            new Tool
            {
                Name = "jenkins_list_projects",
                Description = "List all available Jenkins projects in a specific monorepo (AFL, RL, Racing, Soccer). Requires EasyOps web app to be running at http://localhost:5000",
                InputSchema = CreateListProjectsSchema()
            },
            new Tool
            {
                Name = "jenkins_deploy_project",
                Description = "Deploy a Jenkins project to specified environments",
                InputSchema = CreateDeployProjectSchema()
            }
        };
    }

    private JsonElement CreateBuildProjectSchema()
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                project = new { type = "string", description = "Project name (exact Jenkins project name)" },
                branch = new { type = "string", description = "Branch name (default: 'develop')" },
                monorepo = new { type = "string", description = "Monorepo name (e.g., 'sb-rtp-sports-afl', will be auto-detected if not provided)" }
            },
            required = new[] { "project" }
        };
        return JsonSerializer.SerializeToDocument(schema).RootElement;
    }

    private JsonElement CreateBuildStatusSchema()
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                project = new { type = "string", description = "Project name" },
                branch = new { type = "string", description = "Branch name (default: 'develop')" },
                monorepo = new { type = "string", description = "Monorepo name (optional)" }
            },
            required = new[] { "project" }
        };
        return JsonSerializer.SerializeToDocument(schema).RootElement;
    }

    private JsonElement CreateListProjectsSchema()
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                monorepo = new { type = "string", description = "Monorepo name (e.g., 'sb-rtp-sports-afl', 'sb-rtp-sports-rl', or just 'afl', 'rl')", @default = "sb-rtp-sports-afl" }
            }
        };
        return JsonSerializer.SerializeToDocument(schema).RootElement;
    }

    private JsonElement CreateDeployProjectSchema()
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                project = new { type = "string", description = "Project name" },
                version = new { type = "string", description = "Version to deploy" },
                branch = new { type = "string", description = "Branch name (default: 'develop')" },
                deployToDev = new { type = "boolean", description = "Deploy to DEV environment" },
                deployToStg = new { type = "boolean", description = "Deploy to STG environment" },
                deployToPrd = new { type = "boolean", description = "Deploy to PRD environment" },
                changeDescription = new { type = "string", description = "Description of changes" }
            },
            required = new[] { "project", "version" }
        };
        return JsonSerializer.SerializeToDocument(schema).RootElement;
    }

    public async Task<CallToolResult> ExecuteTool(string toolName, JsonElement? arguments)
    {
        try
        {
            return toolName switch
            {
                "jenkins_build_project" => await BuildProject(arguments),
                "jenkins_get_build_status" => await GetBuildStatus(arguments),
                "jenkins_list_projects" => await ListProjects(arguments),
                "jenkins_deploy_project" => await DeployProject(arguments),
                _ => new CallToolResult
                {
                    Content = new List<ToolContent>
                    {
                        new ToolContent { Text = $"Unknown tool: {toolName}" }
                    },
                    IsError = true
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error executing tool {toolName}");
            return new CallToolResult
            {
                Content = new List<ToolContent>
                {
                    new ToolContent { Text = $"Error: {ex.Message}" }
                },
                IsError = true
            };
        }
    }

    private async Task<CallToolResult> BuildProject(JsonElement? arguments)
    {
        if (arguments == null)
        {
            return ErrorResult("Missing arguments");
        }

        var args = arguments.Value;
        var project = args.TryGetProperty("project", out var p) ? p.GetString() : null;
        var branch = args.TryGetProperty("branch", out var b) ? b.GetString() : "develop";
        var monorepo = args.TryGetProperty("monorepo", out var m) ? m.GetString() : "";

        if (string.IsNullOrEmpty(project))
        {
            return ErrorResult("Project name is required");
        }

        var request = new ExecuteJobRequest
        {
            Project = project,
            Branch = branch ?? "develop",
            JobType = "build",
            Monorepo = monorepo ?? ""
        };

        var result = await _jenkinsService.ExecuteJobAsync(request);

        var message = result.Success
            ? $"✅ Successfully triggered build for '{project}' (branch: {branch})\\n\\nBuild #{result.BuildNumber}\\nJob URL: {result.JobUrl}\\n\\n{result.Message}"
            : $"❌ Failed to build '{project}'\\n\\n{result.Message}\\n\\n💡 Make sure the EasyOps web app is running at http://localhost:5000";

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new ToolContent { Text = message }
            },
            IsError = !result.Success
        };
    }

    private async Task<CallToolResult> GetBuildStatus(JsonElement? arguments)
    {
        if (arguments == null)
        {
            return ErrorResult("Missing arguments");
        }

        var args = arguments.Value;
        var project = args.TryGetProperty("project", out var p) ? p.GetString() : null;
        var branch = args.TryGetProperty("branch", out var b) ? b.GetString() : "develop";
        var monorepo = args.TryGetProperty("monorepo", out var m) ? m.GetString() : "";

        if (string.IsNullOrEmpty(project))
        {
            return ErrorResult("Project name is required");
        }

        var result = await _jenkinsService.GetBuildStatusAsync(project, branch ?? "develop", monorepo ?? "");

        var status = result.IsBuilding ? "🔨 BUILDING" : result.Status;
        var message = $"📊 Build Status for '{project}' (branch: {branch})\\n\\n" +
                     $"Status: {status}\\n" +
                     $"Build #{result.BuildNumber}\\n";
        
        if (!string.IsNullOrEmpty(result.Version))
        {
            message += $"Version: {result.Version}\\n";
        }
        
        message += $"URL: {result.BuildUrl}\\n";
        
        if (result.Timestamp.HasValue)
        {
            message += $"Timestamp: {result.Timestamp:yyyy-MM-dd HH:mm:ss}\\n";
        }

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new ToolContent { Text = message }
            }
        };
    }

    private async Task<CallToolResult> ListProjects(JsonElement? arguments)
    {
        // Get monorepo from arguments, default to AFL
        var monorepo = "sb-rtp-sports-afl";
        if (arguments.HasValue)
        {
            var args = arguments.Value;
            if (args.TryGetProperty("monorepo", out var m))
            {
                var monorepoInput = m.GetString() ?? "";
                // Normalize monorepo name
                monorepo = NormalizeMonorepoName(monorepoInput);
            }
        }

        var projects = await _jenkinsService.GetProjectsFromJenkinsAsync(monorepo);

        if (projects.Count == 0)
        {
            return new CallToolResult
            {
                Content = new List<ToolContent>
                {
                    new ToolContent { Text = $"❌ No projects found in monorepo '{monorepo}'.\\n\\n💡 Make sure the EasyOps web app is running at http://localhost:5000" }
                },
                IsError = true
            };
        }

        var message = $"📋 Jenkins Projects in '{monorepo}':\\n\\n";
        foreach (var project in projects.OrderBy(p => p.DisplayName))
        {
            message += $"• **{project.DisplayName}**\\n";
            message += $"  Name: `{project.Name}`\\n";
            if (!string.IsNullOrEmpty(project.Url))
            {
                message += $"  URL: {project.Url}\\n";
            }
            message += "\\n";
        }

        message += $"\\n💡 Tip: Use 'Build {projects.First().Name}' to trigger a build";

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new ToolContent { Text = message }
            }
        };
    }

    private async Task<CallToolResult> DeployProject(JsonElement? arguments)
    {
        if (arguments == null)
        {
            return ErrorResult("Missing arguments");
        }

        var args = arguments.Value;
        var project = args.TryGetProperty("project", out var p) ? p.GetString() : null;
        var version = args.TryGetProperty("version", out var v) ? v.GetString() : null;
        var branch = args.TryGetProperty("branch", out var b) ? b.GetString() : "develop";
        var deployToDev = args.TryGetProperty("deployToDev", out var dev) && dev.GetBoolean();
        var deployToStg = args.TryGetProperty("deployToStg", out var stg) && stg.GetBoolean();
        var deployToPrd = args.TryGetProperty("deployToPrd", out var prd) && prd.GetBoolean();
        var changeDesc = args.TryGetProperty("changeDescription", out var desc) ? desc.GetString() : "";

        if (string.IsNullOrEmpty(project) || string.IsNullOrEmpty(version))
        {
            return ErrorResult("Project name and version are required");
        }

        var request = new ExecuteJobRequest
        {
            Project = project,
            Branch = branch ?? "develop",
            JobType = "deploy",
            DeployParams = new DeployParameters
            {
                APP_VERSION = version,
                DEPLOY_TO_DEV = deployToDev,
                DEPLOY_TO_STG = deployToStg,
                DEPLOY_TO_PRD = deployToPrd,
                CHANGE_DESCRIPTION = changeDesc ?? ""
            }
        };

        var result = await _jenkinsService.ExecuteJobAsync(request);

        var environments = new List<string>();
        if (deployToDev) environments.Add("DEV");
        if (deployToStg) environments.Add("STG");
        if (deployToPrd) environments.Add("PRD");

        var message = result.Success
            ? $"✅ Successfully triggered deployment for '{project}' version {version}\\n\\nEnvironments: {string.Join(", ", environments)}\\nJob URL: {result.JobUrl}\\n\\n{result.Message}"
            : $"❌ Failed to deploy '{project}'\\n\\n{result.Message}\\n\\n💡 Make sure the EasyOps web app is running at http://localhost:5000";

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new ToolContent { Text = message }
            },
            IsError = !result.Success
        };
    }

    private string NormalizeMonorepoName(string input)
    {
        // Handle short names like "afl", "rl", etc.
        var normalized = input.ToLower().Trim();
        
        if (normalized == "afl")
            return "sb-rtp-sports-afl";
        if (normalized == "rl" || normalized == "rugby")
            return "sb-rtp-sports-rl";
        if (normalized == "racing")
            return "sb-rtp-sports-racing";
        if (normalized == "soccer")
            return "sb-rtp-sports-soccer";
            
        // If it's already a full name, return as-is
        return input;
    }

    private CallToolResult ErrorResult(string message)
    {
        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new ToolContent { Text = $"❌ Error: {message}" }
            },
            IsError = true
        };
    }
}
