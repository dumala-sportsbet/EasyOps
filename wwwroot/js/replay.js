// Game Replay JavaScript functionality

// Global variable to store current environment info
let currentEnvironment = null;

$(document).ready(function () {
    console.log('Replay page loaded');

    // Load current environment
    loadCurrentEnvironment();

    // Initialize
    if ($('#replay-panel').hasClass('show')) {
        loadSavedGames();
    }

    // Tab change event
    $('#replayTabs button[data-bs-toggle="tab"]').on('shown.bs.tab', function (e) {
        if (e.target.id === 'replay-tab') {
            loadSavedGames();
        }
    });

    // Fetch game form submission
    $('#fetchGameForm').on('submit', function (e) {
        e.preventDefault();
        fetchGameFromProduction();
    });

    // Refresh games button
    $('#refreshGamesButton').on('click', function () {
        loadSavedGames();
    });

    // Execute replay form submission
    $('#executeReplayForm').on('submit', function (e) {
        e.preventDefault();
        executeReplay();
    });

    // Cancel execute button
    $('#cancelExecuteButton').on('click', function () {
        $('#replayExecutionPanel').hide();
    });
});

/**
 * Loads the current AWS environment information
 */
function loadCurrentEnvironment() {
    $.ajax({
        url: '/api/replay/current-environment',
        type: 'GET',
        success: function (env) {
            console.log('Current environment:', env);
            currentEnvironment = env;
            
            // Update the display
            const envName = env.environmentName || 'Development';
            $('#currentEnvironmentDisplay').text(envName);
            $('#targetEnvironment').val(env.environment || 'dev');
        },
        error: function (xhr, status, error) {
            console.error('Error loading current environment:', error);
            // Default to dev on error
            currentEnvironment = { environment: 'dev', environmentName: 'Development' };
            $('#currentEnvironmentDisplay').text('Development (default)');
            $('#targetEnvironment').val('dev');
        }
    });
}

/**
 * Fetches game data from production database
 */
function fetchGameFromProduction() {
    const gameId = $('#gameId').val().trim();
    const gameName = $('#gameName').val().trim();
    const notes = $('#notes').val().trim();

    if (!gameId || !gameName) {
        showFetchResult('error', 'Please fill in all required fields');
        return;
    }

    // Show progress
    $('#fetchButton').prop('disabled', true);
    $('#fetchProgress').show();
    $('#fetchResult').hide();

    const requestData = {
        gameId: gameId,
        gameName: gameName,
        notes: notes || null
    };

    $.ajax({
        url: '/api/replay/fetch',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(requestData),
        success: function (response) {
            console.log('Fetch response:', response);
            $('#fetchProgress').hide();
            $('#fetchButton').prop('disabled', false);
            
            if (response.success) {
                showFetchResult('success', response.message);
                $('#fetchGameForm')[0].reset();
                
                // Switch to replay tab after a short delay
                setTimeout(function () {
                    $('#replay-tab').tab('show');
                }, 2000);
            } else {
                showFetchResult('error', response.message);
            }
        },
        error: function (xhr, status, error) {
            console.error('Fetch error:', error);
            $('#fetchProgress').hide();
            $('#fetchButton').prop('disabled', false);
            
            let errorMessage = 'Failed to fetch game data';
            if (xhr.responseJSON && xhr.responseJSON.message) {
                errorMessage = xhr.responseJSON.message;
            } else if (xhr.responseText) {
                errorMessage = xhr.responseText;
            }
            
            showFetchResult('error', errorMessage);
        }
    });
}

/**
 * Displays fetch result message
 */
function showFetchResult(type, message) {
    const alertClass = type === 'success' ? 'alert-success' : 'alert-danger';
    const icon = type === 'success' ? 'bi-check-circle' : 'bi-exclamation-circle';
    
    $('#fetchResult').html(`
        <div class="alert ${alertClass} alert-dismissible fade show" role="alert">
            <i class="bi ${icon}"></i> ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        </div>
    `).show();
}

/**
 * Loads saved games from the database
 */
function loadSavedGames() {
    $('#gamesTableContainer').html(`
        <div class="text-center py-5">
            <div class="spinner-border" role="status">
                <span class="visually-hidden">Loading...</span>
            </div>
            <p class="mt-2">Loading saved games...</p>
        </div>
    `);

    $.ajax({
        url: '/api/replay/games',
        type: 'GET',
        success: function (games) {
            console.log('Loaded games:', games);
            displayGamesTable(games);
        },
        error: function (xhr, status, error) {
            console.error('Error loading games:', error);
            $('#gamesTableContainer').html(`
                <div class="alert alert-danger">
                    <i class="bi bi-exclamation-circle"></i> Failed to load saved games: ${error}
                </div>
            `);
        }
    });
}

/**
 * Displays the games table
 */
function displayGamesTable(games) {
    if (games.length === 0) {
        $('#gamesTableContainer').html(`
            <div class="alert alert-info">
                <i class="bi bi-info-circle"></i> No saved games found. 
                Go to "Fetch from Production" tab to add games.
            </div>
        `);
        return;
    }

    let tableHtml = `
        <div class="table-responsive">
            <table class="table table-hover">
                <thead>
                    <tr>
                        <th>Game ID</th>
                        <th>Game Name</th>
                        <th>Events</th>
                        <th>Fetched At</th>
                        <th>Fetched By</th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody>
    `;

    games.forEach(game => {
        const fetchedAt = new Date(game.fetchedAt).toLocaleString();
        tableHtml += `
            <tr>
                <td><code>${escapeHtml(game.gameId)}</code></td>
                <td>${escapeHtml(game.gameName)}</td>
                <td><span class="badge bg-info">${game.totalEvents}</span></td>
                <td>${fetchedAt}</td>
                <td>${escapeHtml(game.fetchedBy || 'N/A')}</td>
                <td>
                    <button class="btn btn-sm btn-success" onclick="selectGameForReplay(${game.id}, '${escapeHtml(game.gameId)}', '${escapeHtml(game.gameName)}', ${game.totalEvents})">
                        <i class="bi bi-play-circle"></i> Replay
                    </button>
                    <button class="btn btn-sm btn-danger" onclick="deleteGame(${game.id}, '${escapeHtml(game.gameId)}')">
                        <i class="bi bi-trash"></i> Delete
                    </button>
                </td>
            </tr>
        `;
    });

    tableHtml += `
                </tbody>
            </table>
        </div>
    `;

    $('#gamesTableContainer').html(tableHtml);
}

/**
 * Selects a game for replay
 */
function selectGameForReplay(id, gameId, gameName, totalEvents) {
    $('#selectedGameId').val(id);
    $('#selectedGameIdDisplay').text(gameId);
    $('#selectedGameName').text(gameName);
    $('#selectedGameEvents').text(totalEvents);
    
    // Reset form
    $('#gameStartDateTime').val('');
    $('#dryRun').prop('checked', false);
    $('#executionResult').hide();
    
    // Reload current environment to ensure it's up to date
    loadCurrentEnvironment();
    
    // Show execution panel
    $('#replayExecutionPanel').show();
    
    // Scroll to panel
    $('html, body').animate({
        scrollTop: $('#replayExecutionPanel').offset().top - 100
    }, 500);
}

/**
 * Executes the replay
 */
function executeReplay() {
    const replayGameId = parseInt($('#selectedGameId').val());
    const targetEnvironment = $('#targetEnvironment').val();
    const gameStartDateTime = $('#gameStartDateTime').val();
    const dryRun = $('#dryRun').is(':checked');

    if (!targetEnvironment) {
        alert('Unable to detect current environment. Please refresh the page and try again.');
        return;
    }

    if (!gameStartDateTime) {
        alert('Please select a game start date and time');
        return;
    }

    // Confirm execution with environment info
    const envName = currentEnvironment ? currentEnvironment.environmentName : targetEnvironment;
    const confirmMessage = dryRun 
        ? `Are you sure you want to run a dry-run replay in ${envName}?`
        : `Are you sure you want to execute this replay in ${envName}? This cannot be undone.`;
    
    if (!confirm(confirmMessage)) {
        return;
    }

    // Show progress
    $('#executeButton').prop('disabled', true);
    $('#executionProgress').show();
    $('#executionProgressBar').css('width', '0%');
    $('#executionProgressText').text('Starting replay...');
    $('#executionResult').hide();

    const requestData = {
        replayGameId: replayGameId,
        targetEnvironment: targetEnvironment,
        gameStartDateTime: gameStartDateTime,
        dryRun: dryRun
    };

    // Simulate progress (since we don't have real-time updates yet)
    let progress = 0;
    const progressInterval = setInterval(function () {
        progress += 10;
        if (progress <= 90) {
            $('#executionProgressBar').css('width', progress + '%');
        }
    }, 500);

    $.ajax({
        url: '/api/replay/execute',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(requestData),
        success: function (response) {
            console.log('Execution response:', response);
            clearInterval(progressInterval);
            
            $('#executionProgressBar').css('width', '100%');
            $('#executionProgress').hide();
            $('#executeButton').prop('disabled', false);
            
            if (response.success) {
                showExecutionResult('success', response);
            } else {
                showExecutionResult('error', response);
            }
        },
        error: function (xhr, status, error) {
            console.error('Execution error:', error);
            clearInterval(progressInterval);
            
            $('#executionProgress').hide();
            $('#executeButton').prop('disabled', false);
            
            let errorMessage = 'Failed to execute replay';
            if (xhr.responseJSON && xhr.responseJSON.message) {
                errorMessage = xhr.responseJSON.message;
            }
            
            showExecutionResult('error', { message: errorMessage });
        }
    });
}

/**
 * Displays execution result
 */
function showExecutionResult(type, response) {
    const alertClass = type === 'success' ? 'alert-success' : 'alert-danger';
    const icon = type === 'success' ? 'bi-check-circle' : 'bi-exclamation-circle';
    
    let html = `
        <div class="alert ${alertClass}">
            <h6><i class="bi ${icon}"></i> ${response.message}</h6>
    `;
    
    if (response.eventsProcessed !== undefined) {
        html += `
            <hr>
            <p class="mb-1"><strong>Events Processed:</strong> ${response.eventsProcessed}</p>
        `;
    }
    
    if (response.eventsFailed !== undefined && response.eventsFailed > 0) {
        html += `<p class="mb-1"><strong>Events Failed:</strong> ${response.eventsFailed}</p>`;
    }
    
    // Add game link if replay was successful and we have a new game ID
    if (type === 'success' && response.newGameId) {
        const gameUrl = buildGameUrl(response.newGameId);
        html += `
            <hr>
            <div class="d-flex align-items-center">
                <i class="bi bi-link-45deg me-2"></i>
                <div>
                    <strong>Game Created:</strong><br>
                    <a href="${gameUrl}" target="_blank" class="btn btn-sm btn-outline-primary mt-2">
                        <i class="bi bi-box-arrow-up-right"></i> Open Game in Frontend
                    </a>
                </div>
            </div>
        `;
    }
    
    if (response.errors && response.errors.length > 0) {
        html += `
            <hr>
            <p class="mb-1"><strong>Errors:</strong></p>
            <ul class="mb-0">
        `;
        response.errors.forEach(error => {
            html += `<li>${escapeHtml(error)}</li>`;
        });
        html += `</ul>`;
    }
    
    html += `</div>`;
    
    $('#executionResult').html(html).show();
}

/**
 * Builds the game URL based on the environment and game ID
 */
function buildGameUrl(gameId) {
    const env = currentEnvironment ? currentEnvironment.environment : 'dev';
    
    // Map environment to URL subdomain
    let envSubdomain = 'dev';
    if (env === 'staging' || env === 'stg') {
        envSubdomain = 'stg';
    } else if (env === 'dev' || env === 'development') {
        envSubdomain = 'dev';
    }
    
    return `https://sb-rtp-sports-afl-frontend-ecs-${envSubdomain}.int.ts.${envSubdomain}.sbet.cloud/afl/game/${gameId}/pre-match`;
}

/**
 * Deletes a saved game
 */
function deleteGame(id, gameId) {
    if (!confirm(`Are you sure you want to delete game "${gameId}"? This will also delete all associated events.`)) {
        return;
    }

    $.ajax({
        url: `/api/replay/games/${id}`,
        type: 'DELETE',
        success: function (response) {
            console.log('Delete response:', response);
            alert('Game deleted successfully');
            loadSavedGames();
        },
        error: function (xhr, status, error) {
            console.error('Delete error:', error);
            alert('Failed to delete game: ' + error);
        }
    });
}

/**
 * Escapes HTML to prevent XSS
 */
function escapeHtml(text) {
    if (!text) return '';
    const map = {
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#039;'
    };
    return text.toString().replace(/[&<>"']/g, m => map[m]);
}
