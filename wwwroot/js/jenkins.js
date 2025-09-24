// Jenkins functionality for EasyOps - Streamlined UI

// Configuration
const JENKINS_BASE_URL = 'https://jenkins.int.ts.dev.sbet.cloud';
const MONOREPO_JOB = 'Sports';

// Global variables
let availableProjects = [];
let selectedProjects = [];
let currentMode = 'build'; // 'build' or 'deploy'
let lastBuildResults = []; // Store last build version results
let currentBranch = 'develop'; // Store current branch
let currentMonorepo = ''; // Store current selected monorepo
let triggeredBuilds = new Map(); // Track triggered build numbers by project: Map<projectName, {buildNumber, mode, branch}>
let originalSuccessfulBuilds = new Map(); // Store original last successful build info that never changes: Map<projectName, {buildNumber, version}>
let autoRefreshInterval; // Store auto-refresh interval for build monitoring
let currentDeployConfig = null; // Store deploy configuration {dev, stg, prd, changeDescription}
let savedProjectSelections = new Map(); // Store checkbox states for projects

// Authentication state
let isAuthenticated = false;
let currentUser = '';

// Authentication Functions
async function checkAuthStatus() {
    try {
        const response = await fetch('/api/auth/status');
        const status = await response.json();
        
        isAuthenticated = status.isAuthenticated;
        currentUser = status.username;
        
        updateAuthUI();
        return status.isAuthenticated;
    } catch (error) {
        console.error('Error checking auth status:', error);
        isAuthenticated = false;
        updateAuthUI();
        return false;
    }
}

function updateAuthUI() {
    const authStatusBar = document.getElementById('authStatusBar');
    const loginBtn = document.getElementById('loginBtn');
    const logoutBtn = document.getElementById('logoutBtn');
    const currentUserInfo = document.getElementById('currentUserInfo');
    const currentUsername = document.getElementById('currentUsername');
    const authStatusMessage = document.getElementById('authStatusMessage');

    if (isAuthenticated) {
        authStatusBar.className = 'alert alert-success mb-3';
        authStatusMessage.textContent = '✅ Authentication successful. Jenkins operations are available.';
        loginBtn.style.display = 'none';
        logoutBtn.style.display = 'inline-block';
        currentUserInfo.style.display = 'inline';
        currentUsername.textContent = currentUser;
        authStatusBar.style.display = 'block';
    } else {
        authStatusBar.className = 'alert alert-warning mb-3';
        authStatusMessage.textContent = '⚠️ Not authenticated. Please log in to use Jenkins operations.';
        loginBtn.style.display = 'inline-block';
        logoutBtn.style.display = 'none';
        currentUserInfo.style.display = 'none';
        authStatusBar.style.display = 'block';
    }
}

function showLoginModal() {
    const modal = new bootstrap.Modal(document.getElementById('authModal'));
    modal.show();
    
    // Clear previous values
    document.getElementById('jenkinsUsername').value = '';
    document.getElementById('jenkinsApiToken').value = '';
    document.getElementById('authError').style.display = 'none';
}

async function authenticateUser() {
    const username = document.getElementById('jenkinsUsername').value.trim();
    const apiToken = document.getElementById('jenkinsApiToken').value.trim();
    const authError = document.getElementById('authError');
    const authLoading = document.getElementById('authLoading');
    const loginSubmitBtn = document.getElementById('loginSubmitBtn');

    if (!username || !apiToken) {
        authError.textContent = 'Please enter both username and API token.';
        authError.style.display = 'block';
        return;
    }

    // Show loading state
    authError.style.display = 'none';
    authLoading.style.display = 'block';
    loginSubmitBtn.disabled = true;

    try {
        const response = await fetch('/api/auth/login', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                username: username,
                apiToken: apiToken
            })
        });

        const result = await response.json();

        if (result.success) {
            // Close modal and update UI
            const modal = bootstrap.Modal.getInstance(document.getElementById('authModal'));
            modal.hide();
            
            isAuthenticated = true;
            currentUser = username;
            updateAuthUI();
            
            // Load initial data
            await loadAvailableMonorepos();
            if (currentMonorepo) {
                loadProjects();
            }
        } else {
            authError.textContent = result.message || 'Authentication failed';
            authError.style.display = 'block';
        }
    } catch (error) {
        console.error('Authentication error:', error);
        authError.textContent = 'Connection error. Please try again.';
        authError.style.display = 'block';
    } finally {
        authLoading.style.display = 'none';
        loginSubmitBtn.disabled = false;
    }
}

async function logout() {
    try {
        await fetch('/api/auth/logout', { method: 'POST' });
        
        isAuthenticated = false;
        currentUser = '';
        updateAuthUI();
        
        // Clear all data
        availableProjects = [];
        selectedProjects = [];
        lastBuildResults = [];
        savedProjectSelections.clear();
        triggeredBuilds.clear();
        originalSuccessfulBuilds.clear();
        currentDeployConfig = null;
        
        // Reset UI
        resetToFreshState();
        
        // Clear dropdowns
        const monorepoSelect = document.getElementById('monorepoSelect');
        if (monorepoSelect) {
            monorepoSelect.innerHTML = '<option value="">Select monorepo...</option>';
        }
        
    } catch (error) {
        console.error('Logout error:', error);
    }
}

// Helper function to handle authentication errors
function handleAuthError(response) {
    if (response.status === 401) {
        isAuthenticated = false;
        updateAuthUI();
        showLoginModal();
        return true; // Indicates auth error was handled
    }
    return false; // No auth error
}

// Save current project selections
function saveProjectSelections() {
    savedProjectSelections.clear();
    const checkboxes = document.querySelectorAll('.project-checkbox');
    checkboxes.forEach(checkbox => {
        savedProjectSelections.set(checkbox.value, checkbox.checked);
    });
}

// Restore project selections
function restoreProjectSelections() {
    const checkboxes = document.querySelectorAll('.project-checkbox');
    checkboxes.forEach(checkbox => {
        const savedState = savedProjectSelections.get(checkbox.value);
        if (savedState !== undefined) {
            checkbox.checked = savedState;
        }
    });
    // Update the bulk execute button after restoring selections
    updateBulkExecuteButton();
}

// Monorepo Management Functions
async function loadAvailableMonorepos() {
    try {
        const response = await fetch('/api/jenkins/monorepos');
        if (!response.ok) {
            throw new Error('Failed to fetch monorepos');
        }
        
        const monorepos = await response.json();
        const select = document.getElementById('monorepoSelect');
        
        // Clear existing options
        select.innerHTML = '';
        
        // Add options for each monorepo
        monorepos.forEach(monorepo => {
            const option = document.createElement('option');
            option.value = monorepo.jobPath;
            option.textContent = `${monorepo.name} - ${monorepo.description}`;
            select.appendChild(option);
        });
        
        // Select the first one by default if available
        if (monorepos.length > 0) {
            const oldMonorepo = currentMonorepo;
            currentMonorepo = monorepos[0].jobPath;
            select.value = currentMonorepo;
            console.log(`loadAvailableMonorepos: set default monorepo from '${oldMonorepo}' to '${currentMonorepo}'`);
        }
        
    } catch (error) {
        console.error('Error loading monorepos:', error);
        showError('monorepoSelect', 'Failed to load monorepos');
    }
}

function onMonorepoChange() {
    const select = document.getElementById('monorepoSelect');
    const oldMonorepo = currentMonorepo;
    currentMonorepo = select.value;
    console.log(`onMonorepoChange: changed from '${oldMonorepo}' to '${currentMonorepo}'`);
    
    // Clear any existing project data when monorepo changes
    lastBuildResults = [];
    savedProjectSelections.clear();
    
    // Hide any existing results
    const resultsCard = document.getElementById('resultsCard');
    if (resultsCard) {
        resultsCard.style.display = 'none';
    }
    
    // Clear results content
    const resultsContent = document.getElementById('resultsContent');
    if (resultsContent) {
        resultsContent.innerHTML = '';
    }
    
    // Reset UI to initial state
    resetToFreshState();
    
    // Reload projects for the new monorepo
    if (currentMonorepo) {
        loadProjects();
    }
}

// UI Helper Functions
function setBranch(branchName) {
    currentBranch = branchName;
    
    // Update branch input fields
    const branchInput = document.getElementById('branchInput');
    const branchDeployInput = document.getElementById('branchDeployInput');
    const advancedBranchInput = document.getElementById('advancedBranchInput');
    
    if (branchInput) branchInput.value = branchName;
    if (branchDeployInput) branchDeployInput.value = branchName;
    if (advancedBranchInput) advancedBranchInput.value = branchName;
    
    // Update button states
    updateBranchButtons(branchName);
}

function updateBranchButtons(selectedBranch) {
    // Build tab buttons
    const mainBtn = document.getElementById('mainBtn');
    const developBtn = document.getElementById('developBtn');
    
    if (mainBtn && developBtn) {
        mainBtn.classList.toggle('active', selectedBranch === 'main');
        developBtn.classList.toggle('active', selectedBranch === 'develop');
    }
    
    // Deploy tab buttons
    const mainDeployBtn = document.getElementById('mainDeployBtn');
    const developDeployBtn = document.getElementById('developDeployBtn');
    
    if (mainDeployBtn && developDeployBtn) {
        mainDeployBtn.classList.toggle('active', selectedBranch === 'main');
        developDeployBtn.classList.toggle('active', selectedBranch === 'develop');
    }
}

function updateBranchFromInput() {
    const activeTab = document.querySelector('.tab-pane.active');
    let branchValue = '';
    
    if (activeTab && activeTab.id === 'build') {
        branchValue = document.getElementById('branchInput').value.trim();
    } else if (activeTab && activeTab.id === 'deploy') {
        branchValue = document.getElementById('branchDeployInput').value.trim();
    }
    
    if (branchValue) {
        setBranch(branchValue);
    }
}

function updateMainBranchFromAdvanced() {
    const advancedBranchValue = document.getElementById('advancedBranchInput').value.trim();
    if (advancedBranchValue) {
        setBranch(advancedBranchValue);
    }
}

function resetToFreshState() {
    // Hide results
    const resultsCard = document.getElementById('resultsCard');
    if (resultsCard) {
        resultsCard.style.display = 'none';
    }
    
    // Hide project selector
    hideProjectSelector();
    
    // Hide action and execute sections
    const actionSection = document.getElementById('actionSection');
    const deployOptionsCard = document.getElementById('deployOptionsCard');
    if (actionSection) actionSection.style.display = 'none';
    if (deployOptionsCard) deployOptionsCard.style.display = 'none';
    
    // Hide reset button
    const resetBtn = document.getElementById('resetBtn');
    if (resetBtn) resetBtn.style.display = 'none';
    
    // Clear data
    lastBuildResults = [];
    selectedProjects = [];
    triggeredBuilds.clear();
    currentDeployConfig = null;
    
    // Reset branch to develop
    setBranch('develop');
    
    // Clear selected projects display
    updateSelectedProjectsList('selectedProjects', []);
    
    // Reset deploy form
    resetDeployForm();
}

function resetDeployForm() {
    const deployToDev = document.getElementById('deployToDev');
    const deployToStg = document.getElementById('deployToStg');
    const deployToPrd = document.getElementById('deployToPrd');
    const changeDescription = document.getElementById('changeDescription');
    
    if (deployToDev) deployToDev.checked = false;
    if (deployToStg) deployToStg.checked = false;
    if (deployToPrd) deployToPrd.checked = false;
    if (changeDescription) {
        changeDescription.value = '';
        changeDescription.style.display = 'none';
    }
    
    const hint = document.getElementById('changeDescHint');
    if (hint) hint.style.display = 'none';
}

// Initialize page
document.addEventListener('DOMContentLoaded', async function() {
    // Check authentication status first
    const authenticated = await checkAuthStatus();
    
    if (authenticated) {
        // Load monorepos first, then projects for the default monorepo
        await loadAvailableMonorepos();
        if (currentMonorepo) {
            loadProjects();
        }
    } else {
        // Show login modal if not authenticated
        setTimeout(() => showLoginModal(), 500);
    }
});

// Load projects from Jenkins API
async function loadProjects() {
    console.log('loadProjects() called, currentMonorepo:', currentMonorepo);
    try {
        showLoading('projectDropdown', 'Loading projects...');
        
        const monorepoParam = currentMonorepo ? `?monorepo=${encodeURIComponent(currentMonorepo)}` : '';
        const url = `/api/jenkins/projects${monorepoParam}`;
        console.log('Fetching projects from:', url);
        
        const response = await fetch(url);
        console.log('Response status:', response.status, response.statusText);
        
        if (handleAuthError(response)) {
            console.log('Authentication error detected, returning early');
            return;
        }
        if (!response.ok) {
            throw new Error(`Failed to fetch projects: ${response.status} ${response.statusText}`);
        }
        
        const projects = await response.json();
        console.log('Projects received:', projects);
        availableProjects = projects;
        
        populateProjectDropdown('projectDropdown', projects);
        console.log('Projects populated in dropdown');
        
    } catch (error) {
        console.error('Error loading projects:', error);
        showError('projectDropdown', 'Failed to load projects');
    }
}

// Populate project dropdown
function populateProjectDropdown(dropdownId, projects) {
    const dropdown = document.getElementById(dropdownId);
    dropdown.innerHTML = '';
    
    if (projects.length === 0) {
        const option = document.createElement('option');
        option.value = '';
        option.textContent = 'No projects available';
        dropdown.appendChild(option);
        dropdown.disabled = true;
        console.log(`populateProjectDropdown: No projects, disabled ${dropdownId}`);
    } else {
        projects.forEach(project => {
            const option = document.createElement('option');
            option.value = project.name;
            option.textContent = project.displayName || project.name;
            dropdown.appendChild(option);
        });
        dropdown.disabled = false; // Re-enable the dropdown after populating
        console.log(`populateProjectDropdown: Populated ${projects.length} projects, enabled ${dropdownId}`);
    }
}

// Quick Check Versions for All Projects (1 click!)
async function quickCheckVersionsAll() {
    // Get branch from input field first to preserve user input
    const branchInput = document.getElementById('branchInput');
    const customBranch = branchInput ? branchInput.value.trim() : '';
    const branch = customBranch || currentBranch || 'develop';
    
    // Update current branch without overriding input if user entered custom branch
    if (!customBranch) {
        setBranch(branch);
    } else {
        currentBranch = branch;
    }
    
    // Clear original successful builds cache when getting fresh versions
    originalSuccessfulBuilds.clear();
    
    showResults('Project Versions', `<div class="spinner-border me-2"></div>Checking versions for all projects on branch: ${branch}`);
    
    // Show reset button
    const resetBtn = document.getElementById('resetBtn');
    if (resetBtn) resetBtn.style.display = 'inline-block';
    
    try {
        const allProjectNames = availableProjects.map(p => p.name);
        const results = await Promise.all(
            allProjectNames.map(project => getProjectBuildVersion(project, branch))
        );
        
        // Store results for action selection
        lastBuildResults = results.filter(r => r.success);
        
        displayBuildVersionResults(document.getElementById('resultsContent'), results, true);
        showActionSelection();
        
    } catch (error) {
        console.error('Error checking versions:', error);
        showResults('Project Versions', `<div class="alert alert-danger">Failed to get project versions</div>`);
    }
}

// Show project selector for advanced options
function showProjectSelector() {
    console.log('showProjectSelector() called');
    console.log('availableProjects.length:', availableProjects.length);
    console.log('currentMonorepo:', currentMonorepo);
    
    // Sync the branch from current selection
    setBranch(currentBranch);
    
    document.getElementById('projectSelectorCard').style.display = 'block';
    
    // Load projects if they haven't been loaded yet
    if (availableProjects.length === 0) {
        console.log('Loading projects because availableProjects is empty');
        loadProjects();
    } else {
        console.log('Projects already loaded, populating dropdown');
        populateProjectDropdown('projectDropdown', availableProjects);
    }
    
    // Scroll to the selector
    document.getElementById('projectSelectorCard').scrollIntoView({ behavior: 'smooth' });
}

// Hide project selector
function hideProjectSelector() {
    document.getElementById('projectSelectorCard').style.display = 'none';
}

// Update execute button state and text
// Advanced: Get versions for selected projects
async function getVersionsForSelected() {
    const branch = document.getElementById('advancedBranchInput').value.trim() || 'develop';
    
    // Update the global branch and sync all inputs
    setBranch(branch);
    
    if (selectedProjects.length === 0) {
        alert('Please select at least one project');
        return;
    }
    
    showResults('Project Versions', `<div class="spinner-border me-2"></div>Getting versions for ${selectedProjects.length} projects on branch: ${branch}`);
    
    // Show reset button
    const resetBtn = document.getElementById('resetBtn');
    if (resetBtn) resetBtn.style.display = 'inline-block';
    
    try {
        const results = await Promise.all(
            selectedProjects.map(project => getProjectBuildVersion(project, branch))
        );
        
        // Store results for action selection
        lastBuildResults = results.filter(r => r.success);
        currentBranch = branch;
        displayBuildVersionResults(document.getElementById('resultsContent'), results, true);
        
        // Check if any builds are running and start auto-refresh if needed
        checkAndStartVersionAutoRefresh(results);
        
        // Hide project selector and show action selection
        hideProjectSelector();
        showActionSelection();
        
    } catch (error) {
        console.error('Error getting versions:', error);
        showResults('Project Versions', '<div class="alert alert-danger">Failed to get versions</div>');
    }
}

// Add project to selected list
function addProject() {
    const dropdown = document.getElementById('projectDropdown');
    const selectedValue = dropdown.value;
    
    if (selectedValue && !selectedProjects.includes(selectedValue)) {
        selectedProjects.push(selectedValue);
        updateSelectedProjectsList('selectedProjects', selectedProjects);
    }
}

// Select all projects
function selectAllProjects() {
    selectedProjects = [...availableProjects.map(p => p.name)];
    updateSelectedProjectsList('selectedProjects', selectedProjects);
}

// Update selected projects display
function updateSelectedProjectsList(containerId, projectsList) {
    const container = document.getElementById(containerId);
    
    if (projectsList.length === 0) {
        container.innerHTML = '<em class="text-muted">No projects selected</em>';
        return;
    }
    
    container.innerHTML = projectsList.map(project => `
        <span class="badge bg-primary me-1 mb-1">
            ${project}
            <button type="button" class="btn-close btn-close-white ms-1" 
                    onclick="removeProject('${project}')" 
                    aria-label="Remove ${project}"></button>
        </span>
    `).join('');
}

// Remove project from selected list
function removeProject(projectName) {
    selectedProjects = selectedProjects.filter(p => p !== projectName);
    updateSelectedProjectsList('selectedProjects', selectedProjects);
}

// Show results section
function showResults(title, content) {
    document.getElementById('resultsTitle').textContent = title;
    document.getElementById('resultsContent').innerHTML = content;
    document.getElementById('resultsCard').style.display = 'block';
    
    // Scroll to results
    document.getElementById('resultsCard').scrollIntoView({ behavior: 'smooth' });
}

// Get build version for a specific project and branch
async function getProjectBuildVersion(projectName, branchName) {
    try {
        // Double encode branch names to handle special characters like "feature/SEVT-630-test"
        const encodedBranch = encodeURIComponent(encodeURIComponent(branchName));
        const monorepoParam = currentMonorepo ? `&monorepo=${encodeURIComponent(currentMonorepo)}` : '';
        const response = await fetch(`/api/jenkins/build-version?project=${encodeURIComponent(projectName)}&branch=${encodedBranch}${monorepoParam}`);
        
        if (!response.ok) {
            throw new Error(`Failed to get build version for ${projectName}`);
        }
        
        const result = await response.json();
        return {
            project: projectName,
            branch: branchName,
            success: true,
            status: result.status || 'UNKNOWN',
            isBuilding: result.isBuilding || false,
            ...result
        };
        
    } catch (error) {
        console.error(`Error getting build version for ${projectName}:`, error);
        
        // If it's a 404 error (no builds found), treat it as valid but with no previous builds
        if (error.message.includes('404') || error.message.includes('not found')) {
            return {
                project: projectName,
                branch: branchName,
                success: true,
                buildNumber: 0,
                version: 'No previous builds',
                buildUrl: null,
                timestamp: null,
                devDeploy: { buildNumber: 0, version: 'No previous deploys', url: null, timestamp: null },
                stagingDeploy: { buildNumber: 0, version: 'No previous deploys', url: null, timestamp: null },
                productionDeploy: { buildNumber: 0, version: 'No previous deploys', url: null, timestamp: null }
            };
        }
        
        return {
            project: projectName,
            branch: branchName,
            success: false,
            error: error.message
        };
    }
}

// Display build version results
function displayBuildVersionResults(container, results, showExecuteButton = false) {
    const successResults = results.filter(r => r.success);
    const errorResults = results.filter(r => !r.success);
    
    let html = '';
    
    if (successResults.length > 0) {
        html += '<div class="alert alert-success"><h6>✅ Latest Pipeline Versions:</h6>';
        html += '<div class="table-responsive"><table class="table table-sm mb-0">';
        
        // Add table headers including environment-specific deploy information
        const selectHeader = showExecuteButton ? '<th><input type="checkbox" id="selectAllProjects" onchange="toggleAllProjectSelection()" title="Select/Deselect All"></th>' : '';
        html += `<thead><tr>${selectHeader}<th>Project</th><th>Branch</th><th>Build Version</th><th>Status</th><th>DEV Deploy</th><th>STG Deploy</th><th>PRD Deploy</th></tr></thead><tbody>`;
        
        successResults.forEach(result => {
            const buildNumber = result.buildNumber || 0;
            const version = result.version || 'No previous builds';
            
            // Environment deploy information
            const devDeploy = result.devDeploy || { buildNumber: 0, version: 'No previous deploys' };
            const stagingDeploy = result.stagingDeploy || { buildNumber: 0, version: 'No previous deploys' };
            const productionDeploy = result.productionDeploy || { buildNumber: 0, version: 'No previous deploys' };
            
            // Add checkbox for project selection
            const selectCell = showExecuteButton ? 
                `<td>
                    <input type="checkbox" class="project-checkbox" value="${result.project}" 
                           data-branch="${result.branch}" checked onchange="updateBulkExecuteButton()">
                </td>` : '';
            
            // Format deploy cells with version only
            const formatDeployCell = (deploy, envName) => {
                if (deploy.buildNumber === 0) {
                    return `<small class="text-muted">No deploys</small>`;
                }
                return `<code class="small">${deploy.version}</code>`;
            };

            // Format status cell with appropriate badge and spinner for running builds
            const formatStatusCell = (status, isBuilding) => {
                if (isBuilding) {
                    return `<span class="badge bg-primary">
                        <span class="spinner-border spinner-border-sm me-1" role="status"></span>RUNNING
                    </span>`;
                }
                
                switch (status) {
                    case 'SUCCESS':
                        return `<span class="badge bg-success">SUCCESS</span>`;
                    case 'FAILED':
                    case 'FAILURE':
                        return `<span class="badge bg-danger">FAILED</span>`;
                    case 'UNSTABLE':
                        return `<span class="badge bg-warning text-dark">UNSTABLE</span>`;
                    case 'ABORTED':
                        return `<span class="badge bg-secondary">ABORTED</span>`;
                    default:
                        return `<span class="badge bg-secondary">${status || 'UNKNOWN'}</span>`;
                }
            };
            
            html += `<tr>
                ${selectCell}
                <td><strong>${result.project}</strong></td>
                <td><span class="badge bg-secondary">${result.branch}</span></td>
                <td><code class="small">${version}</code></td>
                <td>${formatStatusCell(result.status, result.isBuilding)}</td>
                <td>${formatDeployCell(devDeploy, 'DEV')}</td>
                <td>${formatDeployCell(stagingDeploy, 'STG')}</td>
                <td>${formatDeployCell(productionDeploy, 'PRD')}</td>
            </tr>`;
        });
        html += '</tbody></table></div>';
        
        // Add execute button if requested
        if (showExecuteButton && successResults.length > 0) {
            const actionText = currentMode === 'build' ? 'BUILD' : 'DEPLOY';
            const buttonClass = currentMode === 'build' ? 'btn-danger' : 'btn-warning';
            
            // For deploy mode, check if environments are selected
            let buttonText = `🚀 Execute ${actionText} Jobs`;
            let buttonDisabled = false;
            let onClickHandler = 'executeJenkinsJobs()';
            
            if (currentMode === 'deploy' && (!currentDeployConfig || (!currentDeployConfig.dev && !currentDeployConfig.stg && !currentDeployConfig.prd))) {
                buttonText = '⚠️ Select Environment First';
                buttonDisabled = true;
                onClickHandler = 'return false;'; // Prevent any action when disabled
            }
            
            html += `<div class="mt-3 text-center">
                <hr>
                <h6>Ready to execute ${actionText.toLowerCase()} jobs?</h6>
                <p class="text-muted">Select projects above and click the button below to execute Jenkins ${actionText.toLowerCase()} jobs on branch <strong>${currentBranch}</strong></p>
                <div class="d-flex justify-content-center">
                    <button type="button" class="btn ${buttonClass} btn-lg ${buttonDisabled ? 'disabled' : ''}" id="bulkExecuteBtn" onclick="${onClickHandler}" ${buttonDisabled ? 'disabled' : ''}>
                        ${buttonText} (<span id="selectedCount">${successResults.length}</span> selected)
                    </button>
                </div>
            </div>`;
        }
        
        html += '</div>';
    }
    
    if (errorResults.length > 0) {
        html += '<div class="alert alert-warning"><h6>⚠️ Errors:</h6><ul class="mb-0">';
        errorResults.forEach(result => {
            html += `<li><strong>${result.project}</strong>: ${result.error}</li>`;
        });
        html += '</ul></div>';
    }
    
    container.innerHTML = html;
}

// New unified workflow functions
function showActionSelection() {
    const actionSection = document.getElementById('actionSection');
    const actionInfo = document.getElementById('actionInfo');
    
    if (actionSection && actionInfo) {
        actionInfo.textContent = `${lastBuildResults.length} projects ready`;
        actionSection.style.display = 'block';
    }
}

function selectAction(action) {
    // Save current project selections before switching modes
    saveProjectSelections();
    
    if (action === 'build') {
        showBuildExecute();
    } else if (action === 'deploy') {
        showDeployOptions();
    }
}

function showBuildExecute() {
    // Hide action selection and deploy options
    document.getElementById('actionSection').style.display = 'none';
    document.getElementById('deployOptionsCard').style.display = 'none';
    
    // Set mode to build so the bottom execute button will work for builds
    currentMode = 'build';
    
    // Update the results to show the execute button
    displayBuildVersionResults(document.getElementById('resultsContent'), lastBuildResults, true);
    
    // Restore previously saved project selections after a short delay to ensure DOM is updated
    setTimeout(() => {
        restoreProjectSelections();
    }, 100);
}

function showDeployOptions() {
    // Hide action selection
    document.getElementById('actionSection').style.display = 'none';
    
    // Show deploy options card
    const deployOptionsCard = document.getElementById('deployOptionsCard');
    deployOptionsCard.style.display = 'block';
    
    // Reset deploy form to ensure clean state
    resetDeployForm();
    
    // Initialize deploy button state
    updateDeployButtonState();
    
    // Scroll to the deploy options
    deployOptionsCard.scrollIntoView({ behavior: 'smooth' });
}

function toggleChangeDescription() {
    // Safety check: only run if deploy options card is visible
    const deployOptionsCard = document.getElementById('deployOptionsCard');
    if (!deployOptionsCard || deployOptionsCard.style.display === 'none') {
        return;
    }
    
    const isDevSelected = document.getElementById('deployToDev').checked;
    const isStgSelected = document.getElementById('deployToStg').checked;
    const isProdSelected = document.getElementById('deployToPrd').checked;
    const changeDesc = document.getElementById('changeDescription');
    const hint = document.getElementById('changeDescHint');
    
    // Show change description if any environment is selected
    const anyEnvironmentSelected = isDevSelected || isStgSelected || isProdSelected;
    
    if (anyEnvironmentSelected) {
        changeDesc.style.display = 'block';
        hint.style.display = 'block';
        // Make it required for production, optional for others
        if (isProdSelected) {
            changeDesc.setAttribute('required', 'true');
        } else {
            changeDesc.removeAttribute('required');
        }
    } else {
        changeDesc.style.display = 'none';
        hint.style.display = 'none';
        changeDesc.removeAttribute('required');
    }
    
    updateDeployButtonState();
}

function updateDeployButtonState() {
    const devChecked = document.getElementById('deployToDev').checked;
    const stgChecked = document.getElementById('deployToStg').checked;
    const prdChecked = document.getElementById('deployToPrd').checked;
    const changeDesc = document.getElementById('changeDescription');
    
    const anyEnvSelected = devChecked || stgChecked || prdChecked;
    const prodValid = !prdChecked || (prdChecked && changeDesc.value.trim().length > 0);
    
    // Set deploy mode and store configuration
    if (anyEnvSelected && prodValid && lastBuildResults.length > 0) {
        currentMode = 'deploy';
        currentDeployConfig = {
            dev: devChecked,
            stg: stgChecked,
            prd: prdChecked,
            changeDescription: changeDesc.value.trim()
        };
        
        // Update the results table to show the execute button
        displayBuildVersionResults(document.getElementById('resultsContent'), lastBuildResults, true);
        
        // Restore previously saved project selections after a short delay
        setTimeout(() => {
            restoreProjectSelections();
        }, 100);
    } else {
        // If no environment is selected or prod is invalid, still update the display to show the disabled button
        if (lastBuildResults.length > 0) {
            currentMode = 'deploy';
            currentDeployConfig = null; // Clear config to trigger the disabled state
            displayBuildVersionResults(document.getElementById('resultsContent'), lastBuildResults, true);
            
            // Restore previously saved project selections after a short delay
            setTimeout(() => {
                restoreProjectSelections();
            }, 100);
        }
    }
}

// Add event listeners for deploy options
document.addEventListener('DOMContentLoaded', function() {
    // Add change listeners for deploy environment checkboxes
    ['deployToDev', 'deployToStg', 'deployToPrd'].forEach(id => {
        const element = document.getElementById(id);
        if (element) {
            element.addEventListener('change', updateDeployButtonState);
        }
    });
    
    // Add change listener for change description
    const changeDesc = document.getElementById('changeDescription');
    if (changeDesc) {
        changeDesc.addEventListener('input', updateDeployButtonState);
    }
});

// Function to go back to action selection
function goBackToActionSelection() {
    // Save current project selections before going back
    saveProjectSelections();
    
    // Reset only mode and deploy config, but preserve project data
    currentMode = null;
    currentDeployConfig = null;
    
    // Hide deploy options and results
    document.getElementById('deployOptionsCard').style.display = 'none';
    document.getElementById('resultsCard').style.display = 'none';
    
    // Show action selection
    document.getElementById('actionSection').style.display = 'block';
    
    // Reset deploy form
    resetDeployForm();
    
    // If we have previous build results, restore them to the results view
    if (lastBuildResults && lastBuildResults.length > 0) {
        // Show the results card with preserved project data
        document.getElementById('resultsCard').style.display = 'block';
        document.getElementById('resultsTitle').textContent = 'Project Versions - Choose Action Below';
        
        // Display the results without execute button, so user can see their previously loaded projects
        displayBuildVersionResults(document.getElementById('resultsContent'), lastBuildResults, false);
        
        // Restore project selections after a short delay to ensure DOM is updated
        setTimeout(() => {
            restoreProjectSelections();
        }, 100);
        
        // Update action info to show how many projects are ready
        const actionInfo = document.getElementById('actionInfo');
        if (actionInfo) {
            actionInfo.textContent = `${lastBuildResults.length} projects ready`;
        }
    } else {
        // Clear results if no previous data
        document.getElementById('resultsContent').innerHTML = '';
    }
    
    // Scroll back to action selection
    document.getElementById('actionSection').scrollIntoView({ behavior: 'smooth' });
}

// Execute Jenkins jobs for the selected projects
async function executeJenkinsJobs() {
    console.log('executeJenkinsJobs() called');
    console.log('lastBuildResults:', lastBuildResults);
    console.log('currentMode:', currentMode);
    console.log('currentBranch:', currentBranch);
    
    // Get only the checked projects from the table
    const selectedCheckboxes = document.querySelectorAll('.project-checkbox:checked');
    const selectedProjects = Array.from(selectedCheckboxes).map(checkbox => {
        const projectName = checkbox.value;
        const projectData = lastBuildResults.find(p => p.project === projectName);
        return projectData;
    }).filter(Boolean); // Remove any undefined entries
    
    console.log('Selected projects for execution:', selectedProjects);
    
    if (selectedProjects.length === 0) {
        alert('No projects selected for execution. Please check at least one project in the Latest Pipeline Versions table.');
        return;
    }

    const actionText = currentMode === 'build' ? 'BUILD' : 'DEPLOY';
    const confirmation = confirm(`Are you sure you want to execute ${actionText} jobs for ${selectedProjects.length} projects on branch ${currentBranch}?`);
    
    if (!confirmation) return;

    // Update results to show execution in progress
    showResults(`${actionText} Execution`, `<div class="spinner-border me-2"></div>Executing ${actionText.toLowerCase()} jobs for ${selectedProjects.length} projects...`);

    try {
        // For deploy jobs, skip parameter checking and execute directly
        if (currentMode === 'deploy') {
            console.log('Deploy mode detected - executing jobs without parameter checking');
            
            // Execute all projects without parameters for deploy
            const executionResults = await Promise.all(
                selectedProjects.map(project =>
                    executeJobWithoutParameters(project.project, currentBranch, currentMode, currentMonorepo)
                )
            );

            displayExecutionResults(document.getElementById('resultsContent'), executionResults);
            return;
        }
        
        // For build jobs, check which projects need parameters
        const projectsWithParameters = [];
        const projectsWithoutParameters = [];
        
        // Check parameters for all selected projects
        for (const project of selectedProjects) {
            const parametersCheck = await checkJobParameters(project.project, currentBranch, currentMode);
            if (parametersCheck.hasParameters && parametersCheck.parameterDefinitions.length > 0) {
                projectsWithParameters.push({
                    ...project,
                    parameterDefinitions: parametersCheck.parameterDefinitions
                });
            } else {
                projectsWithoutParameters.push(project);
            }
        }
        
        let sharedParameters = null;
        
        // If any projects have parameters, show a single modal for all
        if (projectsWithParameters.length > 0) {
            // Use the first project's parameter definitions as the template
            // (assuming all projects with parameters have similar structure)
            const templateParams = projectsWithParameters[0].parameterDefinitions;
            const projectNames = projectsWithParameters.map(p => p.project).join(', ');
            
            sharedParameters = await showParameterInputModal(
                `${projectsWithParameters.length} projects (${projectNames})`, 
                currentBranch, 
                currentMode, 
                templateParams,
                projectsWithParameters // Pass the projects data for version lookup
            );
            
            if (sharedParameters === null) {
                // User cancelled the parameter input
                showResults(`${actionText} Execution`, '<div class="alert alert-warning">Parameter input cancelled by user</div>');
                return;
            }
        }
        
        // Execute all projects (with or without parameters)
        const executionResults = await Promise.all([
            // Execute projects with parameters (using shared parameters)
            ...projectsWithParameters.map(project => 
                executeJobWithParameters(project.project, currentBranch, currentMode, sharedParameters, currentMonorepo)
            ),
            // Execute projects without parameters
            ...projectsWithoutParameters.map(project =>
                executeJobWithoutParameters(project.project, currentBranch, currentMode, currentMonorepo)
            )
        ]);

        displayExecutionResults(document.getElementById('resultsContent'), executionResults);

    } catch (error) {
        console.error('Error executing Jenkins jobs:', error);
        showResults(`${actionText} Execution`, '<div class="alert alert-danger">Failed to execute Jenkins jobs</div>');
    }
}

// Execute a single Jenkins job with parameter checking
async function executeJenkinsJob(projectName, branch, mode) {
    console.log(`executeJenkinsJob called for ${projectName}, branch: ${branch}, mode: ${mode}`);
    console.log(`executeJenkinsJob - currentMonorepo at start: ${currentMonorepo}`);
    
    // Capture the current monorepo at the start to avoid losing it during the process
    const selectedMonorepo = currentMonorepo;
    console.log(`executeJenkinsJob - captured selectedMonorepo: ${selectedMonorepo}`);
    
    try {
        // For deploy jobs, skip parameter checking and execute directly
        if (mode === 'deploy') {
            console.log(`Deploy mode detected for ${projectName} - executing without parameter checking`);
            return await executeJobWithoutParameters(projectName, branch, mode, selectedMonorepo);
        }
        
        // For build jobs, first check if the job has parameters
        const parametersCheck = await checkJobParameters(projectName, branch, mode);
        console.log(`Parameter check for ${projectName}:`, parametersCheck);
        
        if (parametersCheck.hasParameters && parametersCheck.parameterDefinitions.length > 0) {
            // Get project data from lastBuildResults for version lookup
            const projectData = lastBuildResults.find(p => p.project === projectName);
            const projectsDataArray = projectData ? [projectData] : null;
            
            // Show parameter input modal
            const userParameters = await showParameterInputModal(projectName, branch, mode, parametersCheck.parameterDefinitions, projectsDataArray);
            
            if (userParameters === null) {
                // User cancelled the parameter input
                return {
                    project: projectName,
                    branch: branch,
                    success: false,
                    jobType: mode,
                    error: 'User cancelled parameter input'
                };
            }
            
            // Execute job with parameters, using the captured monorepo
            return await executeJobWithParameters(projectName, branch, mode, userParameters, selectedMonorepo);
        } else {
            // Execute job without parameters (existing logic), using the captured monorepo
            return await executeJobWithoutParameters(projectName, branch, mode, selectedMonorepo);
        }
    } catch (error) {
        console.error(`Error executing ${mode} job for ${projectName}:`, error);
        return {
            project: projectName,
            branch: branch,
            success: false,
            jobType: mode,
            error: error.message
        };
    }
}

// Display execution results with persistent monitoring
function displayExecutionResults(container, results) {
    const successResults = results.filter(r => r.success);
    const errorResults = results.filter(r => !r.success);
    
    let html = '';
    
    if (successResults.length > 0) {
        const actionText = currentMode === 'build' ? 'BUILD' : 'DEPLOY';
        html += `<div class="alert alert-success"><h6>✅ ${actionText} Jobs Executed Successfully:</h6>`;
        html += '<div class="table-responsive"><table class="table table-sm mb-0">';
        html += '<thead><tr><th>Project</th><th>Branch</th><th>Job Type</th><th>Previous Version</th><th>Current Version</th><th>Triggered Build</th><th>Status</th><th>Job URL</th><th>Actions</th></tr></thead><tbody>';
        
        successResults.forEach(result => {
            const triggeredBuild = triggeredBuilds.get(result.project);
            const buildNumber = triggeredBuild ? `#${triggeredBuild.buildNumber}` : 'Unknown';
            
            // Get version information for this project - separate previous and current versions
            const projectVersion = lastBuildResults.find(r => r.project === result.project);
            let previousVersionDisplay = '<span class="badge bg-secondary">Unknown</span>';
            let currentVersionDisplay = '<span class="badge bg-warning"><strong>Building...</strong></span>';
            
            // Set previous version from last successful build
            if (projectVersion && projectVersion.version && projectVersion.version !== 'Unknown version') {
                previousVersionDisplay = `<span class="badge bg-success">${projectVersion.version}</span>`;
            }
            
            // For triggered builds, current version will be updated by auto-refresh
            if (triggeredBuild) {
                currentVersionDisplay = '<span class="badge bg-warning"><strong>Building...</strong></span>';
            } else {
                // If not building, current = previous
                currentVersionDisplay = previousVersionDisplay;
            }
            
            html += `<tr>
                <td><strong>${result.project}</strong></td>
                <td><span class="badge bg-secondary">${result.branch}</span></td>
                <td><span class="badge bg-primary">${result.jobType.toUpperCase()}</span></td>
                <td>${previousVersionDisplay}</td>
                <td>${currentVersionDisplay}</td>
                <td><span class="badge bg-info">${buildNumber}</span></td>
                <td><span class="badge bg-warning">IN PROGRESS</span></td>
                <td><a href="${result.jobUrl || '#'}" target="_blank" class="btn btn-sm btn-outline-primary">View Job</a></td>
                <td><span class="retry-button-placeholder-${result.project.replace(/[^a-zA-Z0-9]/g, '_')}"></span></td>
            </tr>`;
        });
        html += '</tbody></table></div></div>';
    }
    
    if (errorResults.length > 0) {
        html += '<div class="alert alert-danger"><h6>❌ Failed to Execute:</h6><ul class="mb-0">';
        errorResults.forEach(result => {
            html += `<li><strong>${result.project}</strong>: ${result.error}</li>`;
        });
        html += '</ul></div>';
    }

    // Add auto-refresh functionality and status monitoring
    if (successResults.length > 0) {
        html += `<div class="mt-3 text-center">
            <div class="alert alert-info">
                <h6>🔄 Auto-monitoring builds in progress...</h6>
                <p class="mb-2">This view will automatically refresh every 30 seconds while builds are running.</p>
                <div class="d-flex justify-content-center gap-2 flex-wrap">
                    <button type="button" class="btn btn-info" onclick="refreshExecutionStatus()" id="refreshExecutionBtn">
                        🔄 Refresh Now
                    </button>
                    <button type="button" class="btn btn-outline-secondary" onclick="stopAutoRefresh()">
                        ⏹️ Stop Auto-refresh
                    </button>
                </div>
            </div>
        </div>`;
        
        // Start auto-refresh for monitoring builds
        startAutoRefreshForExecution();
    }
    
    container.innerHTML = html;
}

// Toggle all project selection checkboxes
function toggleAllProjectSelection() {
    const selectAllCheckbox = document.getElementById('selectAllProjects');
    const projectCheckboxes = document.querySelectorAll('.project-checkbox');
    
    projectCheckboxes.forEach(checkbox => {
        checkbox.checked = selectAllCheckbox.checked;
    });
    
    updateBulkExecuteButton();
}

// Update the bulk execute button based on selected projects
function updateBulkExecuteButton() {
    const selectedCheckboxes = document.querySelectorAll('.project-checkbox:checked');
    const selectedCountSpan = document.getElementById('selectedCount');
    const bulkExecuteBtn = document.getElementById('bulkExecuteBtn');
    
    if (selectedCountSpan) {
        selectedCountSpan.textContent = selectedCheckboxes.length;
    }
    
    if (bulkExecuteBtn) {
        bulkExecuteBtn.disabled = selectedCheckboxes.length === 0;
        if (selectedCheckboxes.length === 0) {
            bulkExecuteBtn.classList.add('disabled');
        } else {
            bulkExecuteBtn.classList.remove('disabled');
        }
    }
    
    // Update select all checkbox state
    const selectAllCheckbox = document.getElementById('selectAllProjects');
    const allCheckboxes = document.querySelectorAll('.project-checkbox');
    if (selectAllCheckbox && allCheckboxes.length > 0) {
        const allChecked = Array.from(allCheckboxes).every(cb => cb.checked);
        const noneChecked = Array.from(allCheckboxes).every(cb => !cb.checked);
        
        selectAllCheckbox.checked = allChecked;
        selectAllCheckbox.indeterminate = !allChecked && !noneChecked;
    }
}

// Check if a job has parameters
async function checkJobParameters(projectName, branch, mode) {
    console.log(`checkJobParameters called - currentMonorepo: ${currentMonorepo}`);
    try {
        const encodedBranch = encodeURIComponent(encodeURIComponent(branch));
        const monorepoParam = currentMonorepo ? `&monorepo=${encodeURIComponent(currentMonorepo)}` : '';
        console.log(`checkJobParameters - using monorepoParam: ${monorepoParam}`);
        const response = await fetch(`/api/jenkins/job-parameters?project=${encodeURIComponent(projectName)}&branch=${encodedBranch}&jobType=${mode}${monorepoParam}`);
        
        if (!response.ok) {
            throw new Error(`Failed to check job parameters for ${projectName}`);
        }
        
        return await response.json();
    } catch (error) {
        console.error(`Error checking job parameters for ${projectName}:`, error);
        return {
            project: projectName,
            branch: branch,
            jobType: mode,
            hasParameters: false,
            parameterDefinitions: []
        };
    }
}

// Show parameter input modal and return user input
function showParameterInputModal(projectName, branch, mode, parameterDefinitions, projectsData = null) {
    return new Promise((resolve) => {
        // Create modal HTML
        const modalHTML = createParameterModal(projectName, branch, mode, parameterDefinitions, projectsData);
        
        // Add modal to DOM
        const modalContainer = document.createElement('div');
        modalContainer.innerHTML = modalHTML;
        document.body.appendChild(modalContainer);
        
        // Initialize Bootstrap modal
        const modalElement = modalContainer.querySelector('#parameterModal');
        const modal = new bootstrap.Modal(modalElement);
        
        // Set up event handlers
        const submitBtn = modalElement.querySelector('#parameterSubmitBtn');
        const cancelBtn = modalElement.querySelector('#parameterCancelBtn');
        
        submitBtn.addEventListener('click', () => {
            console.log(`Parameter modal submit clicked - currentMonorepo: ${currentMonorepo}`);
            const parameters = collectParameterValues(modalElement, parameterDefinitions);
            modal.hide();
            resolve(parameters);
        });
        
        cancelBtn.addEventListener('click', () => {
            modal.hide();
            resolve(null);
        });
        
        // Clean up modal when hidden
        modalElement.addEventListener('hidden.bs.modal', () => {
            modalContainer.remove();
        });
        
        // Show modal
        modal.show();
    });
}

// Create parameter modal HTML
function createParameterModal(projectName, branch, mode, parameterDefinitions, projectsData = null) {
    const actionText = mode === 'build' ? 'BUILD' : 'DEPLOY';
    
    let parametersHTML = '';
    parameterDefinitions.forEach((param, index) => {
        parametersHTML += createParameterInput(param, index, mode, projectsData);
    });
    
    // Check if this is for multiple projects
    const isMultipleProjects = projectName.includes(' projects (');
    const titleText = isMultipleProjects 
        ? `🔧 ${actionText} Job Parameters - ${projectName}` 
        : `🔧 ${actionText} Job Parameters - ${projectName}`;
    
    const infoText = isMultipleProjects
        ? '<div class="alert alert-info mb-3"><i class="bi bi-info-circle"></i> <strong>Note:</strong> These parameter values will be applied to all selected projects that require parameters.</div>'
        : '';
    
    return `
        <div class="modal fade" id="parameterModal" tabindex="-1" aria-labelledby="parameterModalLabel" aria-hidden="true">
            <div class="modal-dialog modal-lg">
                <div class="modal-content">
                    <div class="modal-header">
                        <h5 class="modal-title" id="parameterModalLabel">
                            ${titleText}
                        </h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                    </div>
                    <div class="modal-body">
                        ${infoText}
                        <div class="alert alert-info">
                            <strong>Job:</strong> ${projectName}<br>
                            <strong>Branch:</strong> ${branch}<br>
                            <strong>Action:</strong> ${actionText}<br>
                            <strong>Parameters Required:</strong> ${parameterDefinitions.length}
                        </div>
                        <form id="parameterForm">
                            ${parametersHTML}
                        </form>
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary" id="parameterCancelBtn">Cancel</button>
                        <button type="button" class="btn btn-primary" id="parameterSubmitBtn">
                            🚀 Execute ${actionText} with Parameters
                        </button>
                    </div>
                </div>
            </div>
        </div>
    `;
}

// Create input for a single parameter
function createParameterInput(param, index, mode = 'build', projectsData = null) {
    let defaultValue = param.defaultValue !== null && param.defaultValue !== undefined ? param.defaultValue : '';
    
    // For deploy jobs, auto-populate APP_VERSION with the build version from the table
    if (mode === 'deploy' && param.name === 'APP_VERSION' && projectsData && projectsData.length > 0) {
        // Use the first project's version as default (assuming all selected projects have same version)
        const firstProject = projectsData[0];
        if (firstProject.version && firstProject.version !== 'No previous builds') {
            defaultValue = firstProject.version;
        }
    }
    
    switch (param.type) {
        case 'boolean':
            const checked = defaultValue === true || defaultValue === 'true' ? 'checked' : '';
            return `
                <div class="mb-3">
                    <div class="form-check">
                        <input class="form-check-input" type="checkbox" id="param_${index}" name="${param.name}" ${checked}>
                        <label class="form-check-label" for="param_${index}">
                            <strong>${param.name}</strong>
                            ${param.description ? `<br><small class="text-muted">${param.description}</small>` : ''}
                        </label>
                    </div>
                </div>
            `;
            
        case 'choice':
            let optionsHTML = '';
            if (param.choices && param.choices.length > 0) {
                param.choices.forEach(choice => {
                    const selected = choice === defaultValue ? 'selected' : '';
                    optionsHTML += `<option value="${choice}" ${selected}>${choice}</option>`;
                });
            }
            return `
                <div class="mb-3">
                    <label for="param_${index}" class="form-label">
                        <strong>${param.name}</strong>
                        ${param.description ? `<br><small class="text-muted">${param.description}</small>` : ''}
                    </label>
                    <select class="form-select" id="param_${index}" name="${param.name}">
                        ${optionsHTML}
                    </select>
                </div>
            `;
            
        case 'text':
            return `
                <div class="mb-3">
                    <label for="param_${index}" class="form-label">
                        <strong>${param.name}</strong>
                        ${param.description ? `<br><small class="text-muted">${param.description}</small>` : ''}
                    </label>
                    <textarea class="form-control" id="param_${index}" name="${param.name}" rows="3">${defaultValue}</textarea>
                </div>
            `;
            
        case 'password':
            return `
                <div class="mb-3">
                    <label for="param_${index}" class="form-label">
                        <strong>${param.name}</strong>
                        ${param.description ? `<br><small class="text-muted">${param.description}</small>` : ''}
                    </label>
                    <input type="password" class="form-control" id="param_${index}" name="${param.name}" value="${defaultValue}">
                </div>
            `;
            
        default: // string and others
            // Add special note for APP_VERSION in deploy mode
            const versionNote = (mode === 'deploy' && param.name === 'APP_VERSION') 
                ? '<br><small class="text-success"><i class="bi bi-check-circle"></i> Auto-populated from build version in table</small>'
                : '';
            
            return `
                <div class="mb-3">
                    <label for="param_${index}" class="form-label">
                        <strong>${param.name}</strong>
                        ${param.description ? `<br><small class="text-muted">${param.description}</small>` : ''}
                        ${versionNote}
                    </label>
                    <input type="text" class="form-control" id="param_${index}" name="${param.name}" value="${defaultValue}">
                </div>
            `;
    }
}

// Collect parameter values from the modal form
function collectParameterValues(modalElement, parameterDefinitions) {
    const parameters = {};
    
    parameterDefinitions.forEach((param, index) => {
        const input = modalElement.querySelector(`#param_${index}`);
        
        if (input) {
            switch (param.type) {
                case 'boolean':
                    parameters[param.name] = input.checked;
                    break;
                default:
                    parameters[param.name] = input.value;
                    break;
            }
        }
    });
    
    return parameters;
}

// Execute job with parameters
async function executeJobWithParameters(projectName, branch, mode, parameters, monorepo = null) {
    const selectedMonorepo = monorepo || currentMonorepo;
    console.log(`executeJobWithParameters called - using monorepo: ${selectedMonorepo}`);
    try {
        let requestBody = {
            project: projectName,
            branch: branch,
            jobType: mode,
            parameters: parameters,
            monorepo: selectedMonorepo
        };
        console.log(`executeJobWithParameters - request body:`, requestBody);
        
        // Add deploy parameters if in deploy mode
        if (mode === 'deploy' && currentDeployConfig) {
            // Get the version from the project's last successful build
            const projectResult = lastBuildResults.find(r => r.project === projectName);
            const version = projectResult?.version || '0.0.0';
            
            requestBody.deployParams = {
                APP_VERSION: version,
                DEPLOY_TO_DEV: currentDeployConfig.dev,
                DEPLOY_TO_STG: currentDeployConfig.stg,
                DEPLOY_TO_PRD: currentDeployConfig.prd,
                CHANGE_DESCRIPTION: currentDeployConfig.changeDescription || ''
            };
        }
        
        const response = await fetch('/api/jenkins/execute-job-with-parameters', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(requestBody)
        });

        if (!response.ok) {
            throw new Error(`Failed to execute ${mode} job with parameters for ${projectName}`);
        }

        const result = await response.json();
        
        // Store the triggered build number for this project
        if (result.nextBuildNumber) {
            triggeredBuilds.set(projectName, {
                buildNumber: result.nextBuildNumber,
                mode: mode,
                branch: branch
            });
        }
        
        return {
            project: projectName,
            branch: branch,
            success: true,
            jobType: mode,
            ...result
        };

    } catch (error) {
        console.error(`Error executing ${mode} job with parameters for ${projectName}:`, error);
        return {
            project: projectName,
            branch: branch,
            success: false,
            jobType: mode,
            error: error.message
        };
    }
}

// Execute job without parameters (existing logic)
async function executeJobWithoutParameters(projectName, branch, mode, monorepo = null) {
    const selectedMonorepo = monorepo || currentMonorepo;
    console.log(`executeJobWithoutParameters called - using monorepo: ${selectedMonorepo}`);
    try {
        let requestBody = {
            project: projectName,
            branch: branch,
            jobType: mode,
            monorepo: selectedMonorepo
        };
        console.log(`executeJobWithoutParameters - request body:`, requestBody);
        
        // Add deploy parameters if in deploy mode
        if (mode === 'deploy' && currentDeployConfig) {
            // Get the version from the project's last successful build
            const projectResult = lastBuildResults.find(r => r.project === projectName);
            const version = projectResult?.version || '0.0.0';
            
            requestBody.deployParams = {
                APP_VERSION: version,
                DEPLOY_TO_DEV: currentDeployConfig.dev,
                DEPLOY_TO_STG: currentDeployConfig.stg,
                DEPLOY_TO_PRD: currentDeployConfig.prd,
                CHANGE_DESCRIPTION: currentDeployConfig.changeDescription || ''
            };
        }
        
        const response = await fetch('/api/jenkins/execute-job', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(requestBody)
        });

        if (!response.ok) {
            throw new Error(`Failed to execute ${mode} job for ${projectName}`);
        }

        const result = await response.json();
        
        // Store the triggered build number for this project
        if (result.nextBuildNumber) {
            triggeredBuilds.set(projectName, {
                buildNumber: result.nextBuildNumber,
                mode: mode,
                branch: branch
            });
        }
        
        return {
            project: projectName,
            branch: branch,
            success: true,
            jobType: mode,
            ...result
        };

    } catch (error) {
        console.error(`Error executing ${mode} job for ${projectName}:`, error);
        return {
            project: projectName,
            branch: branch,
            success: false,
            jobType: mode,
            error: error.message
        };
    }
}
// Check build status for a specific project and build
async function checkBuildStatus(projectName, branch, mode, buildNumber = null) {
    try {
        // Double encode branch names to handle special characters like "feature/SEVT-630-test"
        const encodedBranch = encodeURIComponent(encodeURIComponent(branch));
        const monorepoParam = currentMonorepo ? `&monorepo=${encodeURIComponent(currentMonorepo)}` : '';
        let url = `/api/jenkins/build-status?project=${encodeURIComponent(projectName)}&branch=${encodedBranch}&jobType=${mode}${monorepoParam}`;
        
        // If buildNumber is provided, add it to the query string
        if (buildNumber) {
            url += `&buildNumber=${buildNumber}`;
        }
        
        const response = await fetch(url);
        
        if (!response.ok) {
            throw new Error(`Failed to get build status for ${projectName}`);
        }

        const result = await response.json();
        return result;

    } catch (error) {
        console.error(`Error checking build status for ${projectName}:`, error);
        return {
            project: projectName,
            branch: branch,
            jobType: mode,
            status: 'ERROR',
            isBuilding: false,
            error: error.message
        };
    }
}

// Start auto-refresh for execution monitoring
function startAutoRefreshForExecution() {
    // Clear any existing interval
    if (autoRefreshInterval) {
        clearInterval(autoRefreshInterval);
    }
    
    // Set up auto-refresh every 30 seconds
    autoRefreshInterval = setInterval(async () => {
        // Only auto-refresh if we have triggered builds
        if (triggeredBuilds.size > 0) {
            await refreshExecutionStatus();
        } else {
            // No more builds in progress, stop auto-refresh
            stopAutoRefresh();
        }
    }, 30000);
    
    console.log('Started auto-refresh for execution monitoring');
}

// Stop auto-refresh monitoring
function stopAutoRefresh() {
    if (autoRefreshInterval) {
        clearInterval(autoRefreshInterval);
        autoRefreshInterval = null;
        
        // Update the UI to show auto-refresh is stopped
        const stopBtn = document.querySelector('button[onclick="stopAutoRefresh()"]');
        if (stopBtn) {
            stopBtn.innerHTML = '✅ Auto-refresh Stopped';
            stopBtn.disabled = true;
            
            // Re-enable after 3 seconds
            setTimeout(() => {
                stopBtn.innerHTML = '▶️ Start Auto-refresh';
                stopBtn.disabled = false;
                stopBtn.setAttribute('onclick', 'startAutoRefreshForExecution()');
            }, 3000);
        }
        
        console.log('Stopped auto-refresh for execution monitoring');
    }
}

// Version Auto-Refresh Functions for Running Builds

// Global variable for version auto-refresh
let versionAutoRefreshInterval;

// Check if any builds are running and start auto-refresh if needed
function checkAndStartVersionAutoRefresh(results) {
    const runningBuilds = results.filter(r => r.success && r.isBuilding);
    
    if (runningBuilds.length > 0) {
        console.log(`Found ${runningBuilds.length} running builds, starting version auto-refresh`);
        startVersionAutoRefresh();
        
        // Show auto-refresh indicator
        showVersionAutoRefreshIndicator(runningBuilds.length);
    } else {
        console.log('No running builds found, stopping version auto-refresh');
        stopVersionAutoRefresh();
    }
}

// Start auto-refresh for version checking when builds are running
function startVersionAutoRefresh() {
    // Clear any existing interval
    if (versionAutoRefreshInterval) {
        clearInterval(versionAutoRefreshInterval);
    }
    
    // Set up auto-refresh every 30 seconds
    versionAutoRefreshInterval = setInterval(async () => {
        console.log('Auto-refreshing versions...');
        await refreshVersionsForRunningBuilds();
    }, 30000);
    
    console.log('Started version auto-refresh (30 seconds interval)');
}

// Stop version auto-refresh
function stopVersionAutoRefresh() {
    if (versionAutoRefreshInterval) {
        clearInterval(versionAutoRefreshInterval);
        versionAutoRefreshInterval = null;
        hideVersionAutoRefreshIndicator();
        console.log('Stopped version auto-refresh');
    }
}

// Refresh versions for all selected projects to check for running builds
async function refreshVersionsForRunningBuilds() {
    if (!selectedProjects || selectedProjects.length === 0) {
        stopVersionAutoRefresh();
        return;
    }
    
    try {
        const results = await Promise.all(
            selectedProjects.map(project => getProjectBuildVersion(project, currentBranch))
        );
        
        // Update the stored results
        lastBuildResults = results.filter(r => r.success);
        
        // Update the display 
        displayBuildVersionResults(document.getElementById('resultsContent'), results, true);
        
        // Check if there are still running builds
        const runningBuilds = results.filter(r => r.success && r.isBuilding);
        
        if (runningBuilds.length === 0) {
            console.log('No more running builds found, stopping auto-refresh');
            stopVersionAutoRefresh();
        } else {
            console.log(`Still ${runningBuilds.length} builds running, continuing auto-refresh`);
            updateVersionAutoRefreshIndicator(runningBuilds.length);
        }
        
    } catch (error) {
        console.error('Error refreshing versions for running builds:', error);
    }
}

// Show auto-refresh indicator in the UI
function showVersionAutoRefreshIndicator(runningCount) {
    // Add indicator to the results section
    const resultsContent = document.getElementById('resultsContent');
    if (resultsContent) {
        let indicator = document.getElementById('versionAutoRefreshIndicator');
        if (!indicator) {
            indicator = document.createElement('div');
            indicator.id = 'versionAutoRefreshIndicator';
            indicator.className = 'alert alert-info mb-3';
            resultsContent.insertBefore(indicator, resultsContent.firstChild);
        }
        
        indicator.innerHTML = `
            <div class="d-flex align-items-center">
                <span class="spinner-border spinner-border-sm me-2" role="status"></span>
                <span><strong>Auto-refresh active:</strong> ${runningCount} build(s) currently running. Refreshing every 30 seconds...</span>
                <button type="button" class="btn btn-outline-secondary btn-sm ms-auto" onclick="stopVersionAutoRefresh()">
                    Stop Auto-refresh
                </button>
            </div>
        `;
    }
}

// Update auto-refresh indicator
function updateVersionAutoRefreshIndicator(runningCount) {
    const indicator = document.getElementById('versionAutoRefreshIndicator');
    if (indicator) {
        indicator.innerHTML = `
            <div class="d-flex align-items-center">
                <span class="spinner-border spinner-border-sm me-2" role="status"></span>
                <span><strong>Auto-refresh active:</strong> ${runningCount} build(s) currently running. Refreshing every 30 seconds...</span>
                <button type="button" class="btn btn-outline-secondary btn-sm ms-auto" onclick="stopVersionAutoRefresh()">
                    Stop Auto-refresh
                </button>
            </div>
        `;
    }
}

// Hide auto-refresh indicator
function hideVersionAutoRefreshIndicator() {
    const indicator = document.getElementById('versionAutoRefreshIndicator');
    if (indicator) {
        indicator.remove();
    }
}

// Refresh execution status for all triggered builds
async function refreshExecutionStatus() {
    if (triggeredBuilds.size === 0) {
        return;
    }
    
    const refreshBtn = document.getElementById('refreshExecutionBtn');
    if (refreshBtn) {
        refreshBtn.disabled = true;
        refreshBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Refreshing...';
    }
    
    try {
        const statusChecks = [];
        
        // Check status for all triggered builds
        for (let [projectName, buildInfo] of triggeredBuilds) {
            statusChecks.push(checkBuildStatus(projectName, buildInfo.branch, buildInfo.mode, buildInfo.buildNumber));
        }
        
        const statusResults = await Promise.all(statusChecks);
        
        // Update the display with current status
        updateExecutionStatusDisplay(statusResults);
        
        // Remove completed builds from tracking
        const completedBuildJobs = [];
        statusResults.forEach(result => {
            if (!result.isBuilding) {
                triggeredBuilds.delete(result.project);
                // Track completed build jobs for refreshing versions
                if (result.jobType === 'build' && result.status === 'SUCCESS') {
                    completedBuildJobs.push(result.project);
                }
            }
        });
        
        // If no more builds are running, stop auto-refresh
        if (triggeredBuilds.size === 0) {
            stopAutoRefresh();
            
            // If any build jobs completed successfully, refresh project versions
            if (completedBuildJobs.length > 0) {
                await refreshProjectVersionsAfterBuild(completedBuildJobs);
            }
        }
        
    } catch (error) {
        console.error('Error refreshing execution status:', error);
    } finally {
        if (refreshBtn) {
            refreshBtn.disabled = false;
            refreshBtn.innerHTML = '🔄 Refresh Now';
        }
    }
}

// Refresh project versions after successful build completion
async function refreshProjectVersionsAfterBuild(completedProjects) {
    try {
        console.log(`Refreshing versions for ${completedProjects.length} projects after build completion:`, completedProjects);
        
        // Show notification that we're refreshing versions
        showToast(`Build completed! Refreshing versions for ${completedProjects.length} projects...`, 'info');
        
        // Get fresh versions for the completed projects
        const branch = currentBranch || 'develop';
        const results = await Promise.all(
            completedProjects.map(project => getProjectBuildVersion(project, branch))
        );
        
        // Update the lastBuildResults with fresh data
        results.forEach(result => {
            const existingIndex = lastBuildResults.findIndex(r => r.project === result.project);
            if (existingIndex >= 0) {
                lastBuildResults[existingIndex] = result;
            } else {
                lastBuildResults.push(result);
            }
        });
        
        // Show success notification
        showToast(`Latest build versions refreshed for deploy operations!`, 'success');
        
        // If the user is on the build results view, update it
        const resultsContent = document.getElementById('resultsContent');
        if (resultsContent && resultsContent.innerHTML.includes('Project Versions')) {
            // Refresh the version display if currently showing versions
            quickCheckVersionsAll();
        }
        
    } catch (error) {
        console.error('Error refreshing project versions after build:', error);
        showToast('Failed to refresh project versions after build completion', 'error');
    }
}

// Update the execution status display
function updateExecutionStatusDisplay(statusResults) {
    const resultsContent = document.getElementById('resultsContent');
    if (!resultsContent) return;
    
    // Find the status table in the results
    const statusTable = resultsContent.querySelector('table tbody');
    if (!statusTable) return;
    
    statusResults.forEach(result => {
        const projectRow = Array.from(statusTable.querySelectorAll('tr')).find(row => {
            const projectCell = row.querySelector('td:first-child strong');
            return projectCell && projectCell.textContent === result.project;
        });
        
        if (projectRow) {
            const previousVersionCell = projectRow.querySelector('td:nth-child(4) .badge');
            const currentVersionCell = projectRow.querySelector('td:nth-child(5) .badge');
            const statusCell = projectRow.querySelector('td:nth-child(7) .badge');
            const actionsCell = projectRow.querySelector('td:nth-child(9)');
            
            // Enhanced debug logging to see exactly what we're getting
            console.log(`Updating version for ${result.project}:`, JSON.stringify({
                isBuilding: result.isBuilding,
                version: result.version,
                inProgressVersion: result.inProgressVersion,
                InProgressVersion: result.InProgressVersion,
                hasInProgressBuild: result.hasInProgressBuild,
                HasInProgressBuild: result.HasInProgressBuild,
                lastSuccessfulVersion: result.lastSuccessfulVersion,
                LastSuccessfulVersion: result.LastSuccessfulVersion,
                allProperties: Object.keys(result).sort()
            }, null, 2));
            
            // Try multiple property name variations for version extraction
            const inProgressVersion = result.inProgressVersion || result.InProgressVersion || result.currentVersion || result.CurrentVersion;
            const hasInProgressBuild = result.hasInProgressBuild || result.HasInProgressBuild;
            const lastSuccessfulVersion = result.lastSuccessfulVersion || result.LastSuccessfulVersion;
            
            // Update previous version column (should remain stable)
            if (previousVersionCell && lastSuccessfulVersion && lastSuccessfulVersion !== 'Unknown version') {
                previousVersionCell.className = 'badge bg-success';
                previousVersionCell.textContent = lastSuccessfulVersion;
            }
            
            // Update current version column based on build status
            if (currentVersionCell) {
                if (result.isBuilding) {
                    currentVersionCell.className = 'badge bg-warning';
                    
                    // Priority order for finding the current building version:
                    // 1. inProgressVersion (specific in-progress version)
                    // 2. version when building (should be the current build version)
                    // 3. fallback to "Building..."
                    
                    if (inProgressVersion && inProgressVersion !== lastSuccessfulVersion) {
                        currentVersionCell.innerHTML = `<strong>Building: ${inProgressVersion}</strong>`;
                        console.log(`✅ Found in-progress version for ${result.project}: ${inProgressVersion}`);
                    } else if (result.version && result.version !== lastSuccessfulVersion && result.version !== 'Unknown version') {
                        currentVersionCell.innerHTML = `<strong>Building: ${result.version}</strong>`;
                        console.log(`✅ Using current version for ${result.project}: ${result.version}`);
                    } else {
                        currentVersionCell.innerHTML = '<strong>Building...</strong>';
                        console.log(`⚠️ No specific version found for building ${result.project}, falling back to "Building..."`);
                    }
                } else if (result.version && result.version !== 'Unknown version') {
                    // Build completed - show the final version
                    currentVersionCell.className = 'badge bg-success';
                    currentVersionCell.textContent = result.version;
                    console.log(`✅ Build completed for ${result.project}: ${result.version}`);
                } else {
                    // No version available
                    currentVersionCell.className = 'badge bg-secondary';
                    currentVersionCell.textContent = 'Unknown';
                }
            }
            
            // Update status display
            if (statusCell) {
                if (result.isBuilding) {
                    statusCell.className = 'badge bg-warning';
                    statusCell.textContent = 'IN PROGRESS';
                    // Clear retry button while job is running
                    if (actionsCell) {
                        actionsCell.innerHTML = '<span class="text-muted">Running...</span>';
                    }
                } else if (result.status === 'SUCCESS') {
                    statusCell.className = 'badge bg-success';
                    statusCell.textContent = 'COMPLETED';
                    // Clear retry button for successful jobs
                    if (actionsCell) {
                        actionsCell.innerHTML = '<span class="text-success">✓</span>';
                    }
                } else if (result.status && result.status !== 'SUCCESS') {
                    // Show retry button for any non-success status (FAILED, ABORTED, UNSTABLE, etc.)
                    if (result.status === 'FAILURE' || result.status === 'ERROR') {
                        statusCell.className = 'badge bg-danger';
                        statusCell.textContent = 'FAILED';
                    } else {
                        statusCell.className = 'badge bg-warning';
                        statusCell.textContent = result.status;
                    }
                    
                    // Show retry button for failed deploy jobs
                    if (actionsCell && result.jobType === 'deploy') {
                        const branch = projectRow.querySelector('td:nth-child(2) .badge').textContent;
                        actionsCell.innerHTML = `<button type="button" class="btn btn-sm btn-outline-danger" onclick="retryDeployJob('${result.project}', '${branch}', '${result.jobType}')" title="Retry failed deploy">🔄 Retry</button>`;
                    } else if (actionsCell) {
                        actionsCell.innerHTML = '<span class="text-danger">✗</span>';
                    }
                } else {
                    statusCell.className = 'badge bg-secondary';
                    statusCell.textContent = result.status || 'UNKNOWN';
                    if (actionsCell) {
                        actionsCell.innerHTML = '<span class="text-muted">-</span>';
                    }
                }
            }
        }
    });
}

// Retry a failed deploy job
async function retryDeployJob(projectName, branch, jobType) {
    try {
        // Show loading state
        const retryButton = document.querySelector(`[onclick="retryDeployJob('${projectName}', '${branch}', '${jobType}')"]`);
        if (retryButton) {
            retryButton.disabled = true;
            retryButton.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Retrying...';
        }

        // Get current monorepo from dropdown
        const monorepoDropdown = document.getElementById('monorepoSelect');
        const monorepo = monorepoDropdown ? monorepoDropdown.value : '';

        if (!monorepo) {
            throw new Error('Please select a monorepo first');
        }

        // Execute the deploy job again (skip parameter checking since it's a deploy)
        await executeJobWithoutParameters(projectName, branch, jobType, monorepo);
        
        // Show success message
        showToast('Retry initiated successfully!', 'success');
        
        // Reset button state
        if (retryButton) {
            retryButton.disabled = false;
            retryButton.innerHTML = '🔄 Retry';
        }

    } catch (error) {
        console.error('Error retrying deploy job:', error);
        showToast(`Failed to retry deploy: ${error.message}`, 'error');
        
        // Reset button state
        const retryButton = document.querySelector(`[onclick="retryDeployJob('${projectName}', '${branch}', '${jobType}')"]`);
        if (retryButton) {
            retryButton.disabled = false;
            retryButton.innerHTML = '🔄 Retry';
        }
    }
}

// Show toast notification
function showToast(message, type = 'info') {
    // Create toast container if it doesn't exist
    let toastContainer = document.getElementById('toast-container');
    if (!toastContainer) {
        toastContainer = document.createElement('div');
        toastContainer.id = 'toast-container';
        toastContainer.className = 'position-fixed top-0 end-0 p-3';
        toastContainer.style.zIndex = '1050';
        document.body.appendChild(toastContainer);
    }

    // Create toast element
    const toastId = 'toast-' + Date.now();
    const toast = document.createElement('div');
    toast.id = toastId;
    toast.className = `toast align-items-center text-white bg-${type === 'error' ? 'danger' : type === 'success' ? 'success' : 'info'} border-0`;
    toast.setAttribute('role', 'alert');
    toast.setAttribute('aria-live', 'assertive');
    toast.setAttribute('aria-atomic', 'true');
    
    toast.innerHTML = `
        <div class="d-flex">
            <div class="toast-body">
                ${message}
            </div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
        </div>
    `;

    toastContainer.appendChild(toast);

    // Initialize and show toast
    const bsToast = new bootstrap.Toast(toast, { delay: 3000 });
    bsToast.show();

    // Remove toast element after it's hidden
    toast.addEventListener('hidden.bs.toast', () => {
        toast.remove();
    });
}
