# Production Database Configuration Guide

## Overview

The Replay feature fetches game events from a PostgreSQL production database. This guide explains how to configure the connection and what the query does.

## Database Schema

Based on your production database, the `events` table has the following structure:

| Column | Type | Description |
|--------|------|-------------|
| `id` | int4 (integer) | Primary key |
| `event_identifier` | text | UUID identifier for the game/event group |
| `sequence` | text | Sequence number for ordering events |
| `payload` | bytea (binary) | JSON payload data |
| `payload_type` | text | Event type (e.g., `flutter.smf.se.game.afl.GameStarted`) |
| `created_time_utc` | timestamp | UTC timestamp when event was created |

## Configuration

### 1. Update Connection String

Edit your `appsettings.Development.json` or `appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "ProductionDatabase": "Host=your-prod-server;Port=5432;Database=your-database;Username=your-username;Password=your-password;SSL Mode=Require;Trust Server Certificate=true"
  }
}
```

### Connection String Parameters

- **Host**: Your PostgreSQL server hostname or IP
- **Port**: PostgreSQL port (default: 5432)
- **Database**: Database name
- **Username**: Database user with READ access to `events` table
- **Password**: User password
- **SSL Mode**: 
  - `Require` - Always use SSL
  - `Prefer` - Use SSL if available
  - `Disable` - No SSL
- **Trust Server Certificate**: 
  - `true` - Skip certificate validation (for self-signed certs)
  - `false` - Validate certificate

### Example Configurations

**Local Development (No SSL):**
```json
"ProductionDatabase": "Host=localhost;Port=5432;Database=events_db;Username=readonly_user;Password=YourPassword123"
```

**Production with SSL:**
```json
"ProductionDatabase": "Host=prod-db.yourcompany.com;Port=5432;Database=events_production;Username=replay_service;Password=SecurePassword;SSL Mode=Require;Trust Server Certificate=false"
```

**AWS RDS:**
```json
"ProductionDatabase": "Host=mydb.abc123.ap-southeast-2.rds.amazonaws.com;Port=5432;Database=events;Username=readonly;Password=MyPassword;SSL Mode=Require"
```

**Azure PostgreSQL:**
```json
"ProductionDatabase": "Host=myserver.postgres.database.azure.com;Port=5432;Database=events;Username=readonly@myserver;Password=MyPassword;SSL Mode=Require"
```

## How the Fetch Works

### Query Logic

When you fetch a game using an `event_identifier`, the service:

1. **Finds the GameStarted event:**
   ```sql
   SELECT created_time_utc 
   FROM events 
   WHERE event_identifier = '94fdf30a-d77f-4099-98a8-3d9009d2275b' 
     AND payload_type = 'flutter.smf.se.game.afl.GameStarted'
   ORDER BY created_time_utc 
   LIMIT 1
   ```

2. **Fetches all events up to that time:**
   ```sql
   SELECT id, event_identifier, sequence, payload, payload_type, created_time_utc
   FROM events 
   WHERE event_identifier = '94fdf30a-d77f-4099-98a8-3d9009d2275b' 
     AND created_time_utc <= [GameStarted Time]
   ORDER BY sequence, created_time_utc
   ```

3. **Stores locally** in the `ReplayGames` and `ReplayGameEvents` tables

### What Gets Fetched

- **All events** with the matching `event_identifier`
- **Up to and including** the `GameStarted` event
- **Ordered by** sequence number and creation time
- **Example events** you might see:
  - `flutter.smf.se.game.afl.AflGameScheduled` (sequence: 0)
  - `flutter.smf.se.game.afl.TradingOpinionCreated` (sequence: 1)
  - `flutter.smf.se.game.afl.PlayersTradingOpinionUpdated` (sequence: 2)
  - `flutter.smf.se.game.afl.GameStarted` (sequence: 4)

## Security Best Practices

### 1. Use Read-Only User

Create a dedicated read-only database user:

```sql
-- Create user
CREATE USER replay_service WITH PASSWORD 'SecurePassword123';

-- Grant read-only access to events table
GRANT CONNECT ON DATABASE your_database TO replay_service;
GRANT USAGE ON SCHEMA public TO replay_service;
GRANT SELECT ON events TO replay_service;

-- Revoke all other permissions
REVOKE CREATE ON SCHEMA public FROM replay_service;
```

### 2. Use Environment Variables

Instead of hardcoding passwords, use environment variables:

**In production, set environment variable:**
```bash
export ConnectionStrings__ProductionDatabase="Host=...;Password=SecurePassword"
```

**Or use Azure Key Vault, AWS Secrets Manager, etc.**

### 3. Network Security

- Ensure your application server can reach the production database
- Use VPN or private network if possible
- Whitelist application server IP in database firewall
- Use SSL/TLS for all connections

### 4. Connection Pooling

Npgsql automatically handles connection pooling. You can configure it:

```json
"ProductionDatabase": "Host=...;Maximum Pool Size=20;Connection Lifetime=300"
```

## Troubleshooting

### Connection Fails

**Error: "Connection refused"**
- Check host and port
- Verify firewall allows connections
- Ensure PostgreSQL is listening on the network interface

**Error: "SSL connection required"**
- Add `SSL Mode=Require` to connection string

**Error: "Password authentication failed"**
- Verify username and password
- Check user has access to database
- For Azure: username format is `user@servername`

### No Events Found

**Check if event_identifier exists:**
```sql
SELECT COUNT(*) 
FROM events 
WHERE event_identifier = 'your-event-id';
```

**Check if GameStarted event exists:**
```sql
SELECT * 
FROM events 
WHERE event_identifier = 'your-event-id' 
  AND payload_type LIKE '%GameStarted%';
```

### Performance Issues

**Add index on event_identifier:**
```sql
CREATE INDEX idx_events_event_identifier 
ON events(event_identifier, created_time_utc);
```

**Add index on payload_type:**
```sql
CREATE INDEX idx_events_payload_type 
ON events(event_identifier, payload_type);
```

## Testing the Connection

### Method 1: From the UI

1. Navigate to `/Replay` page
2. Enter an `event_identifier` (e.g., `94fdf30a-d77f-4099-98a8-3d9009d2275b`)
3. Enter a game name
4. Click "Fetch Game Data"
5. Check the result message

### Method 2: Check Logs

Look for these log messages:

```
INFO: Connected to production database, fetching events for identifier 94fdf30a-d77f-4099-98a8-3d9009d2275b
INFO: Found GameStarted event at 2025-10-23 14:30:00
INFO: Fetched 5 events from production database for identifier 94fdf30a-d77f-4099-98a8-3d9009d2275b
INFO: Successfully fetched and saved game 94fdf30a-d77f-4099-98a8-3d9009d2275b with 5 events
```

### Method 3: Direct Database Query

Test your connection string using `psql`:

```bash
psql "Host=your-server;Port=5432;Database=your-db;Username=your-user"
```

Then run:
```sql
SELECT COUNT(*) FROM events;
```

## Payload Handling

The `payload` column is stored as `bytea` (binary) in PostgreSQL but contains JSON text. The code handles this automatically:

```csharp
Payload = reader.IsDBNull(reader.GetOrdinal("payload")) 
    ? string.Empty 
    : reader.GetString(reader.GetOrdinal("payload"))
```

If you encounter encoding issues, you might need to:

```csharp
// For binary data
var bytes = (byte[])reader["payload"];
var payload = System.Text.Encoding.UTF8.GetString(bytes);
```

## Example Usage

1. **Find an event_identifier from production:**
   ```sql
   SELECT DISTINCT event_identifier 
   FROM events 
   WHERE payload_type = 'flutter.smf.se.game.afl.GameStarted'
   ORDER BY created_time_utc DESC 
   LIMIT 10;
   ```

2. **Enter in UI:**
   - Event Identifier: `94fdf30a-d77f-4099-98a8-3d9009d2275b`
   - Game Name: `AFL Game - Round 1`
   - Notes: `Test replay for development`

3. **Fetch completes:**
   - Events are saved in local SQLite database
   - Ready for replay to non-prod environments

## Connection String Reference

Full list of Npgsql connection string parameters:

| Parameter | Default | Description |
|-----------|---------|-------------|
| Host | localhost | Server hostname |
| Port | 5432 | Server port |
| Database | | Database name |
| Username | | User name |
| Password | | Password |
| SSL Mode | Prefer | Disable, Allow, Prefer, Require |
| Trust Server Certificate | false | Skip cert validation |
| Timeout | 15 | Connection timeout (seconds) |
| Command Timeout | 30 | Query timeout (seconds) |
| Maximum Pool Size | 100 | Max connections in pool |
| Minimum Pool Size | 0 | Min connections in pool |
| Connection Lifetime | 0 | Max connection age (seconds) |
| Connection Idle Lifetime | 300 | Idle connection timeout |

For full reference: https://www.npgsql.org/doc/connection-string-parameters.html
