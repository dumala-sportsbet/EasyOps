using Amazon.ECS;
using Amazon.ECS.Model;
using EasyOps.Models;
using EasyOps.Services;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace EasyOps.Services
{
    public interface IAwsEcsService
    {
        Task<List<EcsServiceInfo>> GetClusterServicesAsync(string clusterName);
        Task<EcsServiceInfo?> GetServiceDetailsAsync(string clusterName, string serviceName);
        List<EcsClusterOption> GetAvailableClusters();
    }

    public class AwsEcsService : IAwsEcsService
    {
        private readonly AwsConfiguration _awsConfig;
        private readonly IAwsAuthenticationService _awsAuthService;
        private readonly ILogger<AwsEcsService> _logger;

        public AwsEcsService(
            IOptions<AwsConfiguration> awsConfig, 
            IAwsAuthenticationService awsAuthService,
            ILogger<AwsEcsService> logger)
        {
            _awsConfig = awsConfig.Value;
            _awsAuthService = awsAuthService;
            _logger = logger;
        }

        public List<EcsClusterOption> GetAvailableClusters()
        {
            return _awsConfig.AvailableClusters;
        }

        public async Task<List<EcsServiceInfo>> GetClusterServicesAsync(string clusterName)
        {
            var services = new List<EcsServiceInfo>();

            try
            {
                if (!_awsAuthService.AreCredentialsValid())
                {
                    throw new UnauthorizedAccessException("AWS credentials are not valid. Please refresh your SAML2AWS session.");
                }

                // Get the current profile and create credentials provider
                var currentProfile = _awsAuthService.GetCurrentProfile();
                var credentials = CreateCredentialsProvider(currentProfile);
                
                using var ecsClient = new AmazonECSClient(credentials, Amazon.RegionEndpoint.GetBySystemName(_awsConfig.Region));

                _logger.LogInformation("Fetching services for cluster: {ClusterName} using profile: {Profile}", clusterName, currentProfile);

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
                        RunningCount = service.RunningCount ?? 0,
                        DesiredCount = service.DesiredCount ?? 0,
                        PendingCount = service.PendingCount ?? 0,
                        LastUpdated = service.CreatedAt ?? DateTime.UtcNow
                    };

                    // Extract task definition details
                    if (taskDefinitions.TryGetValue(service.TaskDefinition, out var taskDef))
                    {
                        serviceInfo.TaskDefinitionFamily = taskDef.Family;
                        serviceInfo.TaskDefinitionRevision = taskDef.Revision ?? 0;
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
                                Essential = container.Essential ?? false
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
            
            // Fallback to anonymous credentials (will fail auth, but won't crash)
            return new Amazon.Runtime.AnonymousAWSCredentials();
        }
    }
}
