// AWS functionality for EasyOps - AWS ECS Operations

// Global variables
let awsCredentialStatus = null;
let availableAwsClusters = [];
let availableAwsEnvironments = [];
let currentAwsCluster = '';
let currentAwsEnvironment = null;
let currentServices = [];
let servicesData = [];

// Authentication and initialization
document.addEventListener('DOMContentLoaded', async function() {
    await loadAwsEnvironments();
    await checkAwsAuthStatus();
    await loadAwsClusters();
});

// AWS Authentication Functions
async function checkAwsAuthStatus() {
    try {
        const response = await fetch('/api/aws/auth/status');
        awsCredentialStatus = await response.json();
        
        updateAwsAuthUI();
        return awsCredentialStatus.isValid;
    } catch (error) {
        console.error('Error checking AWS auth status:', error);
        awsCredentialStatus = { 
            isValid: false, 
            errorMessage: 'Failed to check AWS credentials' 
        };
        updateAwsAuthUI();
        return false;
    }
}

function updateAwsAuthUI() {
    const authStatusBar = document.getElementById('awsAuthStatusBar');
    const authStatusMessage = document.getElementById('awsAuthStatusMessage');
    const awsCurrentUserInfo = document.getElementById('awsCurrentUserInfo');
    const awsAccountId = document.getElementById('awsAccountId');
    const awsRegion = document.getElementById('awsRegion');
    const awsEnvironmentName = document.getElementById('awsEnvironmentName');
    const awsExpiresAt = document.getElementById('awsExpiresAt');
    const awsRefreshBtn = document.getElementById('awsRefreshBtn');
    const awsCurrentEnvironmentText = document.getElementById('awsCurrentEnvironmentText');

    if (awsCredentialStatus?.isValid) {
        authStatusBar.className = 'alert alert-success mb-3';
        authStatusMessage.textContent = '✅ AWS credentials are valid. ECS operations available.';
        
        awsAccountId.textContent = awsCredentialStatus.accountId || 'Unknown';
        awsRegion.textContent = awsCredentialStatus.region || 'ap-southeast-2';
        awsEnvironmentName.textContent = awsCredentialStatus.environmentName || awsCredentialStatus.environment || 'Unknown';
        
        if (awsCredentialStatus.expiresAt) {
            const expiryDate = new Date(awsCredentialStatus.expiresAt);
            awsExpiresAt.textContent = expiryDate.toLocaleTimeString();
        } else {
            awsExpiresAt.textContent = 'Unknown';
        }
        
        awsCurrentUserInfo.style.display = 'block';
        awsRefreshBtn.textContent = '🔄 Refresh';
        awsRefreshBtn.className = 'btn btn-success btn-sm me-2';
        
        // Update environment dropdown button text
        awsCurrentEnvironmentText.textContent = awsCredentialStatus.environmentName || awsCredentialStatus.environment || 'Environment';
    } else {
        authStatusBar.className = 'alert alert-danger mb-3';
        authStatusMessage.textContent = `❌ ${awsCredentialStatus?.errorMessage || 'AWS credentials invalid'}`;
        awsCurrentUserInfo.style.display = 'none';
        awsRefreshBtn.textContent = '🔄 Get SAML2AWS';
        awsRefreshBtn.className = 'btn btn-warning btn-sm me-2';
        awsCurrentEnvironmentText.textContent = 'Environment';
    }
}

async function refreshAwsCredentials() {
    const awsRefreshBtn = document.getElementById('awsRefreshBtn');
    const originalText = awsRefreshBtn.textContent;
    
    awsRefreshBtn.disabled = true;
    awsRefreshBtn.textContent = '⏳ Connecting to Okta...';
    
    try {
        const response = await fetch('/api/aws/auth/refresh', { method: 'POST' });
        const result = await response.json();
        
        if (result.success) {
            awsCredentialStatus = result.status;
            updateAwsAuthUI();
            
            // Reload clusters if credentials were refreshed successfully
            await loadAwsClusters();
            
            alert('✅ AWS credentials refreshed successfully!');
        } else {
            alert(`❌ Failed to refresh AWS credentials: ${result.message}\n\n🔧 Please ensure:\n• SAML2AWS is configured for Okta\n• You approve the Okta push notification\n• Your role assignments are correct`);
        }
    } catch (error) {
        console.error('Error refreshing AWS credentials:', error);
        alert('❌ Error refreshing AWS credentials.\n\n🛠️ Troubleshooting:\n• Check if SAML2AWS is installed\n• Verify your Okta configuration\n• Run the setup script: setup-okta-saml2aws.ps1');
    } finally {
        awsRefreshBtn.disabled = false;
        awsRefreshBtn.textContent = originalText;
    }
}

// Environment Management Functions
async function loadAwsEnvironments() {
    try {
        const response = await fetch('/api/aws/environments');
        if (!response.ok) {
            throw new Error('Failed to fetch AWS environments');
        }
        
        availableAwsEnvironments = await response.json();
        populateEnvironmentDropdown();
        
        // Load current environment
        const currentResponse = await fetch('/api/aws/environments/current');
        if (currentResponse.ok) {
            currentAwsEnvironment = await currentResponse.json();
        }
        
    } catch (error) {
        console.error('Error loading AWS environments:', error);
    }
}

function populateEnvironmentDropdown() {
    const dropdown = document.getElementById('awsEnvironmentDropdown');
    dropdown.innerHTML = '';
    
    availableAwsEnvironments.forEach(env => {
        const li = document.createElement('li');
        const button = document.createElement('button');
        button.className = 'dropdown-item';
        button.textContent = `${env.name} (${env.description})`;
        button.onclick = () => switchAwsEnvironment(env.name);
        
        // Add badge for current environment
        if (currentAwsEnvironment && currentAwsEnvironment.name === env.name) {
            const badge = document.createElement('span');
            badge.className = 'badge bg-success ms-2';
            badge.textContent = 'Current';
            button.appendChild(badge);
        }
        
        li.appendChild(button);
        dropdown.appendChild(li);
    });
    
    // Add separator and login options
    if (availableAwsEnvironments.length > 0) {
        const separator = document.createElement('li');
        separator.innerHTML = '<hr class="dropdown-divider">';
        dropdown.appendChild(separator);
        
        availableAwsEnvironments.forEach(env => {
            const li = document.createElement('li');
            const button = document.createElement('button');
            button.className = 'dropdown-item';
            button.innerHTML = `🔑 Login to ${env.name}`;
            button.onclick = () => loginToAwsEnvironment(env.name);
            li.appendChild(button);
            dropdown.appendChild(li);
        });
    }
}

async function switchAwsEnvironment(environmentName) {
    try {
        const response = await fetch('/api/aws/environments/switch', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ environmentName })
        });
        
        const result = await response.json();
        
        if (result.success) {
            awsCredentialStatus = result.status;
            currentAwsEnvironment = availableAwsEnvironments.find(e => e.name === environmentName);
            
            updateAwsAuthUI();
            populateEnvironmentDropdown();
            
            // Reload clusters for new environment
            await loadAwsClusters();
            
            alert(`✅ Switched to ${environmentName} environment successfully!`);
        } else {
            alert(`❌ Failed to switch to ${environmentName}: ${result.message}`);
        }
    } catch (error) {
        console.error('Error switching environment:', error);
        alert(`❌ Error switching to ${environmentName}. Please try again.`);
    }
}

async function loginToAwsEnvironment(environmentName) {
    try {
        // Show progress message
        const button = event.target;
        const originalText = button.textContent;
        button.disabled = true;
        button.textContent = '⏳ Connecting to Okta...';
        
        const response = await fetch('/api/aws/environments/login', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ environmentName })
        });
        
        const result = await response.json();
        
        if (result.success) {
            awsCredentialStatus = result.status;
            currentAwsEnvironment = availableAwsEnvironments.find(e => e.name === environmentName);
            
            updateAwsAuthUI();
            populateEnvironmentDropdown();
            
            // Reload clusters for new environment
            await loadAwsClusters();
            
            alert(`✅ Successfully logged into ${environmentName} environment!\n\n🎉 You can now access ${environmentName} resources.`);
        } else {
            alert(`❌ Failed to login to ${environmentName}: ${result.message}\n\n🔧 Please ensure:\n• You approve the Okta push notification\n• Your role is assigned for this environment\n• SAML2AWS is properly configured`);
        }
        
        // Restore button state
        button.disabled = false;
        button.textContent = originalText;
    } catch (error) {
        console.error('Error logging into environment:', error);
        alert(`❌ Error logging into ${environmentName}.\n\n🛠️ Setup Help:\n• Run the setup script: setup-okta-saml2aws.ps1\n• Check your Okta configuration\n• Ensure SAML2AWS is installed`);
    }
}

// Cluster Management Functions
async function loadAwsClusters() {
    try {
        const response = await fetch('/api/aws/clusters');
        if (!response.ok) {
            throw new Error('Failed to fetch AWS clusters');
        }
        
        availableAwsClusters = await response.json();
        populateClusterDropdown();
        
    } catch (error) {
        console.error('Error loading AWS clusters:', error);
        showAwsError('Failed to load AWS clusters');
    }
}

function populateClusterDropdown() {
    const select = document.getElementById('awsClusterSelect');
    select.innerHTML = '<option value="">Select ECS cluster...</option>';
    
    // Filter clusters by current environment if available
    let clustersToShow = availableAwsClusters;
    if (currentAwsEnvironment) {
        clustersToShow = availableAwsClusters.filter(cluster => 
            cluster.environment === currentAwsEnvironment.environment
        );
    }
    
    if (clustersToShow.length === 0) {
        select.innerHTML = '<option value="">No clusters available for current environment</option>';
        return;
    }
    
    clustersToShow.forEach(cluster => {
        const option = document.createElement('option');
        option.value = cluster.clusterName;
        option.textContent = `${cluster.name} - ${cluster.description}`;
        option.dataset.environment = cluster.environment;
        option.dataset.awsProfile = cluster.awsProfile || '';
        select.appendChild(option);
    });
}

function onAwsClusterChange() {
    const select = document.getElementById('awsClusterSelect');
    currentAwsCluster = select.value;
    
    const loadServicesBtn = document.getElementById('loadServicesBtn');
    loadServicesBtn.disabled = !currentAwsCluster;
    
    // Update quick info
    updateAwsQuickInfo();
    
    // Hide previous results
    hideAwsResults();
}

function updateAwsQuickInfo() {
    const quickInfo = document.getElementById('awsQuickInfo');
    
    if (currentAwsCluster) {
        const cluster = availableAwsClusters.find(c => c.clusterName === currentAwsCluster);
        if (cluster) {
            quickInfo.innerHTML = `
                <div><strong>Cluster:</strong> ${cluster.name}</div>
                <div><strong>Environment:</strong> ${cluster.environment}</div>
                <div><strong>Region:</strong> ap-southeast-2</div>
                <div><strong>AWS Profile:</strong> ${cluster.awsProfile || 'default'}</div>
                <div class="text-muted mt-2">Click "Load Services" to view ECS services</div>
            `;
        }
    } else {
        const envText = currentAwsEnvironment ? ` for ${currentAwsEnvironment.name}` : '';
        quickInfo.innerHTML = `<div class="text-muted">Select a cluster${envText} to view ECS services</div>`;
    }
}

// Service Loading Functions
async function loadClusterServices() {
    if (!currentAwsCluster) {
        alert('Please select an ECS cluster first');
        return;
    }
    
    if (!awsCredentialStatus?.isValid) {
        alert('AWS credentials are not valid. Please refresh your SAML2AWS session.');
        return;
    }
    
    showAwsLoading('Loading ECS services...');
    
    try {
        const response = await fetch(`/api/aws/clusters/${encodeURIComponent(currentAwsCluster)}/services`);
        
        if (response.status === 401) {
            hideAwsLoading();
            alert('AWS credentials expired. Please refresh your SAML2AWS session.');
            await checkAwsAuthStatus();
            return;
        }
        
        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error || 'Failed to fetch services');
        }
        
        servicesData = await response.json();
        displayServices(servicesData);
        
    } catch (error) {
        console.error('Error loading cluster services:', error);
        hideAwsLoading();
        showAwsError(`Failed to load services: ${error.message}`);
    }
}

async function refreshClusterServices() {
    await loadClusterServices();
}

function displayServices(services) {
    hideAwsLoading();
    
    const servicesCard = document.getElementById('awsServicesCard');
    const servicesCount = document.getElementById('servicesCount');
    const tableBody = document.getElementById('servicesTableBody');
    const refreshBtn = document.getElementById('refreshServicesBtn');
    const awsResetBtn = document.getElementById('awsResetBtn');
    
    // Update count
    servicesCount.textContent = `${services.length} service${services.length !== 1 ? 's' : ''}`;
    
    // Clear table
    tableBody.innerHTML = '';
    
    if (services.length === 0) {
        tableBody.innerHTML = `
            <tr>
                <td colspan="8" class="text-center text-muted">No services found in cluster "${currentAwsCluster}"</td>
            </tr>
        `;
    } else {
        services.forEach(service => {
            const row = createServiceRow(service);
            tableBody.appendChild(row);
        });
    }
    
    // Show results and controls
    servicesCard.style.display = 'block';
    refreshBtn.style.display = 'inline-block';
    awsResetBtn.style.display = 'inline-block';
    refreshBtn.disabled = false;
    
    // Update quick info
    const quickInfo = document.getElementById('awsQuickInfo');
    const cluster = availableAwsClusters.find(c => c.clusterName === currentAwsCluster);
    if (cluster) {
        quickInfo.innerHTML = `
            <div><strong>Cluster:</strong> ${cluster.name}</div>
            <div><strong>Environment:</strong> ${cluster.environment}</div>
            <div><strong>Services:</strong> ${services.length}</div>
            <div><strong>Total Running:</strong> ${services.reduce((sum, s) => sum + s.runningCount, 0)}</div>
        `;
    }
}

function createServiceRow(service) {
    const row = document.createElement('tr');
    
    // Service status badge
    const statusClass = getServiceStatusClass(service.serviceStatus);
    const statusBadge = `<span class="badge ${statusClass}">${service.serviceStatus}</span>`;
    
    // Simplify service name by removing common prefixes
    const simplifiedServiceName = simplifyServiceName(service.serviceName);
    
    // Docker image version (get correct container's image tag based on service name)
    const correctContainer = findCorrectContainer(service);
    const imageVersion = correctContainer ? correctContainer.imageTag : 'N/A';
    
    // Running/Desired counts
    const runningDesired = `${service.runningCount}/${service.desiredCount}`;
    const countClass = service.runningCount === service.desiredCount ? 'text-success' : 'text-warning';
    
    // Last updated
    const lastUpdated = new Date(service.lastUpdated).toLocaleString();
    
    row.innerHTML = `
        <td><strong>${simplifiedServiceName}</strong></td>
        <td>${statusBadge}</td>
        <td><code>${imageVersion}</code></td>
        <td><code>${service.cpu}</code></td>
        <td><code>${service.memory}</code></td>
        <td><span class="${countClass}"><strong>${runningDesired}</strong></span></td>
        <td><small>${lastUpdated}</small></td>
        <td>
            <button class="btn btn-sm btn-outline-info" onclick="showServiceDetails('${service.serviceName}')">
                📋 Details
            </button>
        </td>
    `;
    
    return row;
}

function getServiceStatusClass(status) {
    switch (status.toUpperCase()) {
        case 'ACTIVE': return 'bg-success';
        case 'INACTIVE': return 'bg-secondary';
        case 'PENDING': return 'bg-warning';
        case 'DRAINING': return 'bg-info';
        default: return 'bg-primary';
    }
}

function getImageName(imageUrl) {
    if (!imageUrl) return 'N/A';
    
    // Extract image name from full URL (e.g., "123456789.dkr.ecr.region.amazonaws.com/my-app:tag" -> "my-app")
    const parts = imageUrl.split('/');
    const lastPart = parts[parts.length - 1];
    const imageName = lastPart.split(':')[0];
    
    return imageName;
}

function simplifyServiceName(serviceName) {
    if (!serviceName) return 'N/A';
    
    // Common prefixes to remove
    const prefixesToRemove = [
        'sb-rtp-sports-afl-',
        'sb-rtp-sports-',
        'sb-rtp-',
        'sports-afl-',
        'sports-',
        'afl-'
    ];
    
    let simplified = serviceName;
    
    // Remove the longest matching prefix
    for (const prefix of prefixesToRemove) {
        if (simplified.toLowerCase().startsWith(prefix.toLowerCase())) {
            simplified = simplified.substring(prefix.length);
            break;
        }
    }
    
    // Remove CloudFormation-style suffixes (e.g., -ecs-stg-EcsService-axLnMz83hiM1)
    // Pattern: -ecs-{env}-EcsService-{randomString}
    const cloudFormationPattern = /-ecs-(dev|stg|staging|prd|prod|production)-EcsService-[a-zA-Z0-9]+$/i;
    simplified = simplified.replace(cloudFormationPattern, '');
    
    // Remove common suffixes like environment indicators
    const suffixesToRemove = ['-dev', '-stg', '-staging', '-prd', '-prod', '-production'];
    for (const suffix of suffixesToRemove) {
        if (simplified.toLowerCase().endsWith(suffix.toLowerCase())) {
            simplified = simplified.substring(0, simplified.length - suffix.length);
            break;
        }
    }
    
    return simplified;
}

// Helper function to find the correct container based on service name
function findCorrectContainer(service) {
    if (!service.containers || service.containers.length === 0) {
        return null;
    }
    
    // If only one container, return it
    if (service.containers.length === 1) {
        return service.containers[0];
    }
    
    // Get the simplified service name for matching
    const simplifiedServiceName = simplifyServiceName(service.serviceName);
    
    // Try to find a container that matches the service name
    // Look for containers that contain the service name (without common suffixes like -xray-daemon)
    const matchingContainer = service.containers.find(container => {
        // Skip containers with known auxiliary suffixes
        const auxiliarySuffixes = ['-xray-daemon', '-sidecar', '-proxy', '-agent', '-monitor'];
        const hasAuxiliarySuffix = auxiliarySuffixes.some(suffix => 
            container.name.toLowerCase().includes(suffix)
        );
        
        if (hasAuxiliarySuffix) {
            return false;
        }
        
        // Check if container name is similar to service name
        const containerNameLower = container.name.toLowerCase();
        const serviceNameLower = simplifiedServiceName.toLowerCase();
        
        // Direct match or service name contains container name
        return containerNameLower === serviceNameLower || 
               serviceNameLower.includes(containerNameLower) ||
               containerNameLower.includes(serviceNameLower);
    });
    
    // If we found a matching container, return it; otherwise return the first non-auxiliary container
    if (matchingContainer) {
        return matchingContainer;
    }
    
    // Return the first container that doesn't have auxiliary suffixes
    const nonAuxiliaryContainer = service.containers.find(container => {
        const auxiliarySuffixes = ['-xray-daemon', '-sidecar', '-proxy', '-agent', '-monitor'];
        return !auxiliarySuffixes.some(suffix => 
            container.name.toLowerCase().includes(suffix)
        );
    });
    
    return nonAuxiliaryContainer || service.containers[0];
}

// Service Details Functions
async function showServiceDetails(serviceName) {
    const service = servicesData.find(s => s.serviceName === serviceName);
    if (!service) {
        alert('Service not found');
        return;
    }
    
    const modalTitle = document.querySelector('#serviceDetailsModal .modal-title');
    const modalContent = document.getElementById('serviceDetailsContent');
    
    modalTitle.textContent = `📦 ${serviceName} Details`;
    
    // Generate detailed service information
    const detailsHtml = `
        <div class="row">
            <div class="col-md-6">
                <h6>📋 Service Information</h6>
                <table class="table table-sm">
                    <tr><td><strong>Service Name:</strong></td><td>${service.serviceName}</td></tr>
                    <tr><td><strong>Status:</strong></td><td><span class="badge ${getServiceStatusClass(service.serviceStatus)}">${service.serviceStatus}</span></td></tr>
                    <tr><td><strong>Task Definition:</strong></td><td><code>${service.taskDefinitionFamily}:${service.taskDefinitionRevision}</code></td></tr>
                    <tr><td><strong>Running/Desired:</strong></td><td>${service.runningCount}/${service.desiredCount}</td></tr>
                    <tr><td><strong>Pending:</strong></td><td>${service.pendingCount}</td></tr>
                    <tr><td><strong>CPU:</strong></td><td><code>${service.cpu}</code></td></tr>
                    <tr><td><strong>Memory:</strong></td><td><code>${service.memory}</code></td></tr>
                    <tr><td><strong>Last Updated:</strong></td><td>${new Date(service.lastUpdated).toLocaleString()}</td></tr>
                </table>
            </div>
            <div class="col-md-6">
                <h6>🐳 Container Information</h6>
                ${service.containers.map(container => `
                    <div class="card mb-2">
                        <div class="card-body p-2">
                            <h7><strong>${container.name}</strong> ${container.essential ? '<span class="badge bg-primary">Essential</span>' : ''}</h7>
                            <div><strong>Image:</strong> <code>${container.image}</code></div>
                            <div><strong>Tag:</strong> <code>${container.imageTag}</code></div>
                            ${container.cpu ? `<div><strong>CPU:</strong> <code>${container.cpu}</code></div>` : ''}
                            ${container.memory ? `<div><strong>Memory:</strong> <code>${container.memory}</code></div>` : ''}
                            ${container.memoryReservation ? `<div><strong>Memory Reservation:</strong> <code>${container.memoryReservation}</code></div>` : ''}
                        </div>
                    </div>
                `).join('')}
            </div>
        </div>
    `;
    
    modalContent.innerHTML = detailsHtml;
    
    // Show modal
    const modal = new bootstrap.Modal(document.getElementById('serviceDetailsModal'));
    modal.show();
}

// Export Functions
function exportServicesData() {
    if (!servicesData || servicesData.length === 0) {
        alert('No services data to export');
        return;
    }
    
    // Prepare CSV data
    const csvData = [];
    const headers = [
        'Service Name', 'Simplified Name', 'Status', 'Version',
        'CPU', 'Memory', 'Running Count', 'Desired Count', 'Pending Count',
        'Container Name', 'Docker Image', 'Container CPU', 'Container Memory'
    ];
    csvData.push(headers);
    
    servicesData.forEach(service => {
        const simplifiedName = simplifyServiceName(service.serviceName);
        
        if (service.containers.length > 0) {
            service.containers.forEach(container => {
                csvData.push([
                    service.serviceName,
                    simplifiedName,
                    service.serviceStatus,
                    container.imageTag,
                    service.cpu,
                    service.memory,
                    service.runningCount,
                    service.desiredCount,
                    service.pendingCount,
                    container.name,
                    container.image,
                    container.cpu || '',
                    container.memory || ''
                ]);
            });
        } else {
            csvData.push([
                service.serviceName,
                simplifiedName,
                service.serviceStatus,
                'N/A',
                service.cpu,
                service.memory,
                service.runningCount,
                service.desiredCount,
                service.pendingCount,
                '', '', '', ''
            ]);
        }
    });
    
    // Convert to CSV string
    const csvString = csvData.map(row => 
        row.map(field => `"${String(field).replace(/"/g, '""')}"`).join(',')
    ).join('\n');
    
    // Download CSV file
    const blob = new Blob([csvString], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `aws-ecs-services-${currentAwsCluster}-${new Date().toISOString().split('T')[0]}.csv`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    window.URL.revokeObjectURL(url);
}

// Utility Functions
function showAwsLoading(message) {
    const loadingCard = document.getElementById('awsLoadingCard');
    const loadingMessage = document.getElementById('awsLoadingMessage');
    
    loadingMessage.textContent = message;
    loadingCard.style.display = 'block';
}

function hideAwsLoading() {
    const loadingCard = document.getElementById('awsLoadingCard');
    loadingCard.style.display = 'none';
}

function showAwsError(message) {
    alert(`AWS Error: ${message}`);
}

function hideAwsResults() {
    const servicesCard = document.getElementById('awsServicesCard');
    const refreshBtn = document.getElementById('refreshServicesBtn');
    const awsResetBtn = document.getElementById('awsResetBtn');
    
    servicesCard.style.display = 'none';
    refreshBtn.style.display = 'none';
    awsResetBtn.style.display = 'none';
    
    servicesData = [];
}

function resetAwsView() {
    // Clear selections
    currentAwsCluster = '';
    document.getElementById('awsClusterSelect').value = '';
    
    // Reset UI
    hideAwsResults();
    updateAwsQuickInfo();
    
    // Disable buttons
    document.getElementById('loadServicesBtn').disabled = true;
    document.getElementById('refreshServicesBtn').disabled = true;
}
