using Amazon.ECS;
using Amazon.ECS.Model;
using EasyOps.Models;
using EasyOps.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace EasyOps.Services
{
    public interface IAwsEcsService
    {
        Task<List<EcsServiceInfo>> GetClusterServicesAsync(string clusterName);
        Task<EcsServiceInfo?> GetServiceDetailsAsync(string clusterName, string serviceName);
        Task<List<EcsClusterOption>> GetAvailableClustersAsync();
    }

    public class AwsEcsService : IAwsEcsService
    {
        private readonly AwsConfiguration _awsConfig;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IAwsAuthenticationService _awsAuthService;
        private readonly IDatabaseService _databaseService;
        private readonly ILogger<AwsEcsService> _logger;

        public AwsEcsService(
            IOptions<AwsConfiguration> awsConfig,
            IWebHostEnvironment webHostEnvironment,
            IAwsAuthenticationService awsAuthService,
            IDatabaseService databaseService,
            ILogger<AwsEcsService> logger)
        {
            _awsConfig = awsConfig.Value;
            _webHostEnvironment = webHostEnvironment;
            _awsAuthService = awsAuthService;
            _databaseService = databaseService;
            _logger = logger;
        }

        public async Task<List<EcsClusterOption>> GetAvailableClustersAsync()
        {
            try
            {
                var clusters = await _databaseService.GetClustersWithEnvironmentAsync();
                var clusterOptions = clusters.Select(c => new EcsClusterOption
                {
                    Name = c.Name,
                    ClusterName = c.ClusterName,
                    Environment = c.EnvironmentType,
                    Description = c.Description,
                    AwsProfile = c.AwsProfile,
                    AccountId = c.AccountId
                }).ToList();

                _logger.LogInformation("Loaded {Count} clusters from database", clusterOptions.Count);
                foreach (var cluster in clusterOptions)
                {
                    _logger.LogInformation("Cluster: {Name}, Environment: {Environment}, ClusterName: {ClusterName}", 
                        cluster.Name, cluster.Environment, cluster.ClusterName);
                }

                return clusterOptions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available clusters from database");
                return new List<EcsClusterOption>();
            }
        }

        public async Task<List<EcsServiceInfo>> GetClusterServicesAsync(string clusterName)
        {
            var services = new List<EcsServiceInfo>();

            try
            {
                // Get the cluster information to determine the correct AWS profile
                var clusters = await _databaseService.GetClustersWithEnvironmentAsync();
                var cluster = clusters.FirstOrDefault(c => c.ClusterName == clusterName);
                
                if (cluster == null)
                {
                    throw new ArgumentException($"Cluster '{clusterName}' not found in database");
                }

                var awsProfile = cluster.AwsProfile;
                
                // In development mode, skip credential validation as we're using mock data for auth
                if (!_webHostEnvironment.IsDevelopment() && !_awsAuthService.AreCredentialsValid())
                {
                    throw new UnauthorizedAccessException("AWS credentials are not valid. Please refresh your SAML2AWS session.");
                }

                // Create credentials provider using the cluster's specific AWS profile
                var credentials = CreateCredentialsProvider(awsProfile);
                
                using var ecsClient = new AmazonECSClient(credentials, Amazon.RegionEndpoint.GetBySystemName(_awsConfig.Region));

                _logger.LogInformation("Fetching services for cluster: {ClusterName} using profile: {Profile}", clusterName, awsProfile);

                // List all services in the cluster
                var listServicesRequest = new ListServicesRequest
                {
                    Cluster = clusterName,
                    MaxResults = 100
                };

                var listServicesResponse = await ecsClient.ListServicesAsync(listServicesRequest);
                
                if (!listServicesResponse.ServiceArns.Any())
                {
                    _logger.LogInformation("No services found in cluster: {ClusterName}", clusterName);
                    return services;
                }

                // Describe services in batches (AWS API limit: 10 services per request)
                var allServices = new List<Amazon.ECS.Model.Service>();
                var serviceBatches = listServicesResponse.ServiceArns
                    .Select((arn, index) => new { arn, index })
                    .GroupBy(x => x.index / 10)
                    .Select(g => g.Select(x => x.arn).ToList())
                    .ToList();

                _logger.LogInformation("Found {TotalServices} services, processing in {BatchCount} batches", 
                    listServicesResponse.ServiceArns.Count, serviceBatches.Count);

                foreach (var serviceBatch in serviceBatches)
                {
                    _logger.LogInformation("Processing batch with {ServiceCount} services", serviceBatch.Count);
                    
                    var describeServicesRequest = new DescribeServicesRequest
                    {
                        Cluster = clusterName,
                        Services = serviceBatch
                    };

                    var describeServicesResponse = await ecsClient.DescribeServicesAsync(describeServicesRequest);
                    allServices.AddRange(describeServicesResponse.Services);
                }

                // Get task definitions for all services
                var taskDefinitionArns = allServices
                    .Select(s => s.TaskDefinition)
                    .Distinct()
                    .ToList();

                var taskDefinitions = new Dictionary<string, Amazon.ECS.Model.TaskDefinition>();
                
                foreach (var taskDefArn in taskDefinitionArns)
                {
                    try
                    {
                        var describeTaskDefRequest = new DescribeTaskDefinitionRequest
                        {
                            TaskDefinition = taskDefArn
                        };
                        var taskDefResponse = await ecsClient.DescribeTaskDefinitionAsync(describeTaskDefRequest);
                        taskDefinitions[taskDefArn] = taskDefResponse.TaskDefinition;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Failed to fetch task definition {TaskDef}: {Error}", taskDefArn, ex.Message);
                    }
                }

                // Build service information
                foreach (var service in allServices)
                {
                    var serviceInfo = new EcsServiceInfo
                    {
                        ServiceName = service.ServiceName,
                        TaskDefinitionArn = service.TaskDefinition,
                        ServiceStatus = service.Status,
                        RunningCount = service.RunningCount,
                        DesiredCount = service.DesiredCount,
                        PendingCount = service.PendingCount,
                        LastUpdated = service.CreatedAt
                    };

                    // Extract task definition details
                    if (taskDefinitions.TryGetValue(service.TaskDefinition, out var taskDef))
                    {
                        serviceInfo.TaskDefinitionFamily = taskDef.Family;
                        serviceInfo.TaskDefinitionRevision = taskDef.Revision;
                        serviceInfo.Cpu = taskDef.Cpu ?? "Not specified";
                        serviceInfo.Memory = taskDef.Memory ?? "Not specified";

                        // Extract container information
                        foreach (var container in taskDef.ContainerDefinitions)
                        {
                            var containerInfo = new EcsContainerInfo
                            {
                                Name = container.Name,
                                Image = container.Image,
                                Cpu = container.Cpu,
                                Memory = container.Memory,
                                MemoryReservation = container.MemoryReservation,
                                Essential = container.Essential
                            };

                            // Extract image tag
                            if (!string.IsNullOrEmpty(container.Image))
                            {
                                var imageParts = container.Image.Split(':');
                                if (imageParts.Length > 1)
                                {
                                    containerInfo.ImageTag = imageParts.Last();
                                }
                                else
                                {
                                    containerInfo.ImageTag = "latest";
                                }
                            }

                            // Extract environment variables (first few for display)
                            if (container.Environment != null)
                            {
                                containerInfo.Environment = container.Environment
                                    .Take(5) // Limit to first 5 for display
                                    .Select(env => $"{env.Name}={env.Value}")
                                    .ToList();
                            }

                            serviceInfo.Containers.Add(containerInfo);
                        }
                    }

                    services.Add(serviceInfo);
                }

                _logger.LogInformation("Successfully fetched {Count} services from cluster {ClusterName}", 
                    services.Count, clusterName);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching services for cluster {ClusterName}", clusterName);
                throw;
            }

            return services.OrderBy(s => s.ServiceName).ToList();
        }

        public async Task<EcsServiceInfo?> GetServiceDetailsAsync(string clusterName, string serviceName)
        {
            try
            {
                var services = await GetClusterServicesAsync(clusterName);
                return services.FirstOrDefault(s => s.ServiceName == serviceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching service details for {ServiceName} in cluster {ClusterName}", 
                    serviceName, clusterName);
                return null;
            }
        }

        private Amazon.Runtime.AWSCredentials CreateCredentialsProvider(string profileName)
        {
            // Use profile-based credentials
            var credentialProfileStoreChain = new Amazon.Runtime.CredentialManagement.CredentialProfileStoreChain();
            if (credentialProfileStoreChain.TryGetAWSCredentials(profileName, out var credentials))
            {
                return credentials;
            }
            
            // If profile not found, throw a helpful error
            throw new InvalidOperationException($"AWS profile '{profileName}' not found. Please configure your AWS credentials for this profile in ~/.aws/credentials or run 'aws configure sso --profile {profileName}' to set up SAML authentication.");
        }
    }
}
