# FetchEventsFromProdDbAsync Implementation - Summary

## ✅ What's Been Implemented

### 1. PostgreSQL Connection
- ✅ Installed `Npgsql` package (version 9.0.4)
- ✅ Added `IConfiguration` dependency injection to `ReplayService`
- ✅ Implemented production database connection logic

### 2. Query Logic
The implementation fetches events in two steps:

**Step 1: Find GameStarted Event**
```sql
SELECT created_time_utc 
FROM events 
WHERE event_identifier = @eventIdentifier 
  AND payload_type = 'flutter.smf.se.game.afl.GameStarted'
ORDER BY created_time_utc 
LIMIT 1
```

**Step 2: Fetch All Events Up To That Time**
```sql
SELECT id, event_identifier, sequence, payload, payload_type, created_time_utc
FROM events 
WHERE event_identifier = @eventIdentifier 
  AND created_time_utc <= @gameStartedTime
ORDER BY sequence, created_time_utc
```

### 3. Data Mapping
Maps PostgreSQL columns to `GameEventDto`:
- `event_identifier` → `EventIdentifier`
- `sequence` → `Sequence`
- `payload` → `Payload` (handles NULL/empty values)
- `payload_type` → `PayloadType`

### 4. Error Handling
- ✅ Validates connection string exists
- ✅ Logs connection attempts
- ✅ Logs event counts
- ✅ Throws descriptive exceptions
- ✅ Handles missing GameStarted event gracefully

### 5. Configuration Files Updated
- ✅ `appsettings.json` - Added `ConnectionStrings:ProductionDatabase`
- ✅ `appsettings.Development.json` - Added connection string

## 🔧 Configuration Required

### Update Connection String

Edit `appsettings.Development.json` or `appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "ProductionDatabase": "Host=YOUR_SERVER;Port=5432;Database=YOUR_DB;Username=YOUR_USER;Password=YOUR_PASSWORD;SSL Mode=Require"
  }
}
```

Replace:
- `YOUR_SERVER` - PostgreSQL server hostname
- `YOUR_DB` - Database name
- `YOUR_USER` - Database username (recommend read-only user)
- `YOUR_PASSWORD` - Database password
- `SSL Mode` - Adjust based on your SSL requirements

### Example Connection Strings

**No SSL (Development):**
```json
"Host=localhost;Port=5432;Database=events_db;Username=postgres;Password=MyPassword"
```

**With SSL (Production):**
```json
"Host=prod-db.example.com;Port=5432;Database=events_production;Username=replay_readonly;Password=SecurePass123;SSL Mode=Require;Trust Server Certificate=true"
```

## 🎯 How to Use

### 1. Configure Connection String
Update `appsettings.Development.json` with your production database details.

### 2. Test the Connection
Navigate to `/Replay` page and try fetching a game:
- **Event Identifier**: `94fdf30a-d77f-4099-98a8-3d9009d2275b` (use actual ID from your DB)
- **Game Name**: Any descriptive name
- **Notes**: Optional

### 3. Check Logs
Look for these messages:
```
INFO: Connected to production database, fetching events for identifier ...
INFO: Found GameStarted event at ...
INFO: Fetched X events from production database
INFO: Successfully fetched and saved game ... with X events
```

## 📋 What Gets Fetched

For event_identifier `94fdf30a-d77f-4099-98a8-3d9009d2275b`, you'll get:

1. **flutter.smf.se.game.afl.AflGameScheduled** (sequence: 0)
2. **flutter.smf.se.game.afl.TradingOpinionCreated** (sequence: 1)
3. **flutter.smf.se.game.afl.PlayersTradingOpinionUpdated** (sequence: 2)
4. **flutter.smf.se.game.afl.GameStarted** (sequence: 4)
5. *(Any other events between 0 and GameStarted)*

**Events are:**
- Ordered by `sequence` and `created_time_utc`
- Stored in local SQLite `ReplayGameEvents` table
- Ready for replay to non-prod environments

## 🔍 Testing Queries

### Find a Valid Event Identifier
```sql
SELECT DISTINCT event_identifier 
FROM events 
WHERE payload_type = 'flutter.smf.se.game.afl.GameStarted'
ORDER BY created_time_utc DESC 
LIMIT 10;
```

### Check Events for an Identifier
```sql
SELECT 
    sequence,
    payload_type,
    created_time_utc
FROM events 
WHERE event_identifier = '94fdf30a-d77f-4099-98a8-3d9009d2275b'
ORDER BY sequence;
```

## ⚠️ Important Notes

### Security
1. **Use read-only database user** - Only SELECT permission on `events` table
2. **Store passwords securely** - Use environment variables or Azure Key Vault
3. **Use SSL in production** - Set `SSL Mode=Require`
4. **Whitelist application server** - In database firewall

### Performance
1. **Add indexes** if queries are slow:
   ```sql
   CREATE INDEX idx_events_event_identifier 
   ON events(event_identifier, created_time_utc);
   ```

2. **Connection pooling** - Npgsql handles this automatically

### Edge Cases
- ✅ **No GameStarted event**: Fetches all events for that identifier
- ✅ **NULL payload**: Handled with empty string
- ✅ **Duplicate event_identifier**: Only fetches events before first GameStarted

## 📚 Documentation Files Created

1. **PRODUCTION-DATABASE-CONFIG.md** - Detailed configuration guide
2. **SQL-QUERIES.md** - Useful SQL queries for testing and troubleshooting
3. **REPLAY-FEATURE.md** - Complete feature documentation
4. **REPLAY-QUICKSTART.md** - Quick start guide
5. **REPLAY-ARCHITECTURE.md** - Architecture diagrams

## 🚀 Next Steps

1. ✅ Database migration complete
2. ✅ `FetchEventsFromProdDbAsync` implemented
3. ⏳ **Configure production DB connection string**
4. ⏳ **Test fetching a real game**
5. ⏳ **Implement `ProcessEventAsync` for replay**

## 🐛 Troubleshooting

### "Connection refused"
- Check firewall allows connections
- Verify host and port
- Ensure PostgreSQL listens on network interface

### "Connection string not configured"
- Add `ConnectionStrings:ProductionDatabase` to appsettings.json
- Check spelling matches exactly

### "SSL connection required"
- Add `SSL Mode=Require` to connection string

### "No events found"
- Verify event_identifier exists in production DB
- Check if GameStarted event exists
- Try fetching all events (will work even without GameStarted)

### "Password authentication failed"
- Verify username and password
- Check user has CONNECT and SELECT permissions
- For Azure: format is `username@servername`

## 📞 Need Help?

Check the documentation files:
- Configuration issues → `PRODUCTION-DATABASE-CONFIG.md`
- SQL queries → `SQL-QUERIES.md`
- Feature overview → `REPLAY-FEATURE.md`
