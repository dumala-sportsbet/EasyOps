# Game Replay Feature - Implementation Guide

## Overview

The Game Replay feature allows you to:
1. **Fetch game data** from production database and store it locally
2. **Execute replays** of saved games to non-production environments

This feature is built with placeholder API implementations that you can customize with your actual business logic.

## Architecture

### Database Models

**ReplayGame** - Stores game metadata
- `Id` - Auto-increment primary key
- `GameId` - Production game identifier
- `GameName` - Friendly name for the game
- `FetchedAt` - Timestamp when data was fetched
- `FetchedBy` - Username who fetched the data
- `TotalEvents` - Count of events
- `Notes` - Optional notes

**ReplayGameEvent** - Stores individual game events
- `Id` - Auto-increment primary key
- `ReplayGameId` - Foreign key to ReplayGame
- `EventIdentifier` - Event identifier from production (matches `event_identifier` column)
- `Sequence` - Event sequence number (matches `sequence` column)
- `Payload` - JSON payload data (matches `payload` column)
- `PayloadType` - Type of payload (matches `payload_type` column)
- `CreatedAt` - Timestamp when stored locally

### API Endpoints

**ReplayController** (`/api/replay`)

1. `POST /api/replay/fetch` - Fetch game from production
   - Request: `{ gameId, gameName, notes? }`
   - Response: `{ success, message, replayGameId?, eventCount }`

2. `GET /api/replay/games` - List all saved games
   - Response: Array of `SavedGameDto`

3. `GET /api/replay/games/{id}` - Get specific game
   - Response: `SavedGameDto`

4. `POST /api/replay/execute` - Execute replay
   - Request: `{ replayGameId, targetEnvironment, dryRun }`
   - Response: `{ success, message, eventsProcessed, eventsFailed, errors[] }`

5. `DELETE /api/replay/games/{id}` - Delete saved game
   - Response: Success message

6. `GET /api/replay/validate-connection` - Check prod DB connection
   - Response: `{ connected, message }`

### Services

**ReplayService** - Business logic layer

Key methods:
- `FetchGameFromProdAsync()` - Fetches game data from production
- `GetSavedGamesAsync()` - Retrieves all saved games
- `GetSavedGameByIdAsync()` - Retrieves specific game
- `ExecuteReplayAsync()` - Executes replay process
- `DeleteSavedGameAsync()` - Deletes saved game

**Helper methods (TODO - Implement these)**:
- `FetchEventsFromProdDbAsync()` - Query production database
- `ProcessEventAsync()` - Send event to target environment

### User Interface

**Replay.cshtml** - Two-tab interface

**Tab 1: Fetch from Production**
- Form to enter Game ID and Name
- Notes field (optional)
- Fetch button triggers API call
- Progress indicator
- Result display

**Tab 2: Execute Replay**
- Table showing all saved games
- Select game for replay
- Choose target environment (Dev/Staging/UAT)
- Dry run option
- Execute button
- Progress tracking
- Results display with errors

## Implementation Steps

### Step 1: Database Migration

After adding the new models, create and run a migration:

```powershell
# Add migration
dotnet ef migrations add AddReplayTables

# Update database
dotnet ef database update
```

### Step 2: Configure Production Database Connection

Add to `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "ProductionDatabase": "YOUR_PROD_CONNECTION_STRING"
  }
}
```

### Step 3: Implement Production DB Query

Update `ReplayService.FetchEventsFromProdDbAsync()`:

```csharp
private async Task<List<GameEventDto>> FetchEventsFromProdDbAsync(string gameId)
{
    // Option 1: Using a separate DbContext for production
    var connectionString = _configuration.GetConnectionString("ProductionDatabase");
    var optionsBuilder = new DbContextOptionsBuilder<ProductionDbContext>();
    optionsBuilder.UseSqlServer(connectionString); // or UseNpgsql, UseMySql, etc.
    
    using var prodContext = new ProductionDbContext(optionsBuilder.Options);
    
    var events = await prodContext.GameEvents
        .Where(e => e.GameId == gameId)
        .OrderBy(e => e.Sequence)
        .Select(e => new GameEventDto
        {
            EventIdentifier = e.EventIdentifier,
            Sequence = e.Sequence,
            Payload = e.Payload,
            PayloadType = e.PayloadType
        })
        .ToListAsync();
    
    return events;
}
```

### Step 4: Implement Event Processing

Update `ReplayService.ProcessEventAsync()`:

```csharp
private async Task ProcessEventAsync(ReplayGameEvent gameEvent, string targetEnvironment)
{
    // Example: Send to API endpoint
    var targetUrl = _configuration[$"Environments:{targetEnvironment}:ApiUrl"];
    
    using var httpClient = new HttpClient();
    var content = new StringContent(gameEvent.Payload, Encoding.UTF8, "application/json");
    
    var response = await httpClient.PostAsync($"{targetUrl}/api/game-events", content);
    response.EnsureSuccessStatusCode();
    
    _logger.LogInformation("Processed event {EventId} to {Environment}", 
        gameEvent.EventIdentifier, targetEnvironment);
}
```

### Step 5: Add Environment Configuration

Add to `appsettings.json`:

```json
{
  "Environments": {
    "dev": {
      "ApiUrl": "https://dev.yourapp.com",
      "Description": "Development Environment"
    },
    "staging": {
      "ApiUrl": "https://staging.yourapp.com",
      "Description": "Staging Environment"
    },
    "uat": {
      "ApiUrl": "https://uat.yourapp.com",
      "Description": "UAT Environment"
    }
  }
}
```

## Testing the Feature

### 1. Test Fetch Functionality

1. Navigate to `/Replay`
2. Click "Fetch from Production" tab
3. Enter a Game ID and Name
4. Click "Fetch Game Data"
5. Verify data is saved in local database

### 2. Test Replay Functionality

1. Click "Execute Replay" tab
2. Select a saved game
3. Choose target environment
4. Enable "Dry Run" for testing
5. Click "Execute Replay"
6. Verify progress and results

### 3. Test Delete Functionality

1. In the saved games table
2. Click "Delete" on a game
3. Confirm deletion
4. Verify game is removed

## Production Database Schema

Based on your screenshot, the production table has these columns:
- `id` - Primary key
- `event_identifier` - Event identifier (e.g., "af2360dc-5314-4c84-b37f-264df3b798f0")
- `sequence` - Sequence number (0, 1, 2, ...)
- `payload` - JSON payload data
- `payload_type` - Type classification (e.g., "flutter.smf.se.game.afl.AflGameScheduled")

Make sure your production database query maps these correctly to the `GameEventDto` structure.

## Error Handling

The implementation includes:
- Try-catch blocks in all service methods
- Logging at key points
- User-friendly error messages
- Validation for required fields
- Confirmation dialogs for destructive actions

## Security Considerations

**TODO: Add these security features**

1. **Authentication** - Require user login
2. **Authorization** - Restrict replay to authorized users
3. **Audit Trail** - Log who fetched/replayed what and when
4. **Environment Protection** - Prevent replay to production
5. **Rate Limiting** - Prevent abuse of fetch/replay
6. **Connection String Security** - Use Azure Key Vault or similar

## Next Steps

1. ✅ Database models created
2. ✅ API endpoints implemented (placeholder)
3. ✅ UI created
4. ⏳ Implement production DB connection
5. ⏳ Implement actual replay logic
6. ⏳ Add authentication/authorization
7. ⏳ Add comprehensive error handling
8. ⏳ Add progress tracking (SignalR for real-time updates)
9. ⏳ Add replay history/audit logging
10. ⏳ Performance testing with large datasets

## File Structure

```
EasyOps/
├── Controllers/
│   └── ReplayController.cs          # API endpoints
├── DTOs/
│   └── ReplayDTOs.cs                # Data transfer objects
├── Models/
│   ├── AppDbContext.cs              # Updated with DbSets
│   └── ReplayModels.cs              # ReplayGame & ReplayGameEvent
├── Pages/
│   ├── Replay.cshtml                # UI markup
│   └── Replay.cshtml.cs             # Page model
├── Services/
│   └── ReplayService.cs             # Business logic
└── wwwroot/
    └── js/
        └── replay.js                # Client-side JavaScript
```

## Notes

- All placeholder methods are marked with `// TODO:` comments
- The service returns empty collections/default responses until you implement the actual logic
- The UI is fully functional and ready to use once you wire up the backend
- Consider adding SignalR for real-time progress updates during replay
- Consider batch processing for large numbers of events
- Add retry logic for failed event processing

## Questions?

Common implementation questions:

**Q: How do I connect to the production database?**
A: Create a separate DbContext, configure the connection string, and query the events table.

**Q: How do I send events to the target environment?**
A: Use HttpClient to POST to the target API, or use a message queue like RabbitMQ/Azure Service Bus.

**Q: Can I pause/resume a replay?**
A: Not currently, but you could add this by tracking the last processed sequence number.

**Q: What if an event fails during replay?**
A: The service logs the error and continues. You can implement retry logic or stop-on-error behavior.
