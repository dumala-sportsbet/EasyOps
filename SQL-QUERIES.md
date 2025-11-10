# Production Database - Useful SQL Queries

## Find Event Identifiers for Games

### Recent GameStarted Events
```sql
SELECT 
    event_identifier,
    payload_type,
    created_time_utc,
    sequence
FROM events 
WHERE payload_type = 'flutter.smf.se.game.afl.GameStarted'
ORDER BY created_time_utc DESC 
LIMIT 20;
```

### All Events for a Specific Game
```sql
SELECT 
    id,
    event_identifier,
    sequence,
    payload_type,
    created_time_utc,
    LENGTH(payload) as payload_size_bytes
FROM events 
WHERE event_identifier = '94fdf30a-d77f-4099-98a8-3d9009d2275b'
ORDER BY sequence, created_time_utc;
```

### Count Events by Type for a Game
```sql
SELECT 
    payload_type,
    COUNT(*) as event_count,
    MIN(sequence) as first_sequence,
    MAX(sequence) as last_sequence
FROM events 
WHERE event_identifier = '94fdf30a-d77f-4099-98a8-3d9009d2275b'
GROUP BY payload_type
ORDER BY first_sequence;
```

## Verify GameStarted Event Exists

```sql
SELECT 
    event_identifier,
    sequence,
    payload_type,
    created_time_utc
FROM events 
WHERE event_identifier = '94fdf30a-d77f-4099-98a8-3d9009d2275b'
  AND payload_type = 'flutter.smf.se.game.afl.GameStarted'
ORDER BY created_time_utc;
```

## Find Events Before GameStarted

```sql
WITH game_started AS (
    SELECT created_time_utc 
    FROM events 
    WHERE event_identifier = '94fdf30a-d77f-4099-98a8-3d9009d2275b'
      AND payload_type = 'flutter.smf.se.game.afl.GameStarted'
    LIMIT 1
)
SELECT 
    e.event_identifier,
    e.sequence,
    e.payload_type,
    e.created_time_utc
FROM events e, game_started gs
WHERE e.event_identifier = '94fdf30a-d77f-4099-98a8-3d9009d2275b'
  AND e.created_time_utc <= gs.created_time_utc
ORDER BY e.sequence, e.created_time_utc;
```

## Event Timeline Analysis

### Events Timeline with Duration
```sql
SELECT 
    sequence,
    payload_type,
    created_time_utc,
    LAG(created_time_utc) OVER (ORDER BY sequence) as prev_event_time,
    created_time_utc - LAG(created_time_utc) OVER (ORDER BY sequence) as time_since_previous
FROM events 
WHERE event_identifier = '94fdf30a-d77f-4099-98a8-3d9009d2275b'
ORDER BY sequence;
```

### First and Last Event
```sql
SELECT 
    MIN(sequence) as first_sequence,
    MAX(sequence) as last_sequence,
    MIN(created_time_utc) as first_event_time,
    MAX(created_time_utc) as last_event_time,
    MAX(created_time_utc) - MIN(created_time_utc) as total_duration,
    COUNT(*) as total_events
FROM events 
WHERE event_identifier = '94fdf30a-d77f-4099-98a8-3d9009d2275b';
```

## Search for Games by Date

### Games from Today
```sql
SELECT DISTINCT 
    event_identifier,
    MIN(created_time_utc) as first_event,
    COUNT(*) as event_count
FROM events 
WHERE DATE(created_time_utc) = CURRENT_DATE
  AND payload_type LIKE '%GameScheduled%'
GROUP BY event_identifier
ORDER BY first_event DESC;
```

### Games from Specific Date Range
```sql
SELECT DISTINCT 
    event_identifier,
    MIN(created_time_utc) as game_start,
    COUNT(*) as event_count
FROM events 
WHERE created_time_utc BETWEEN '2025-10-01' AND '2025-10-31'
  AND payload_type LIKE '%GameStarted%'
GROUP BY event_identifier
ORDER BY game_start DESC;
```

## Payload Analysis

### View Payload Content (First 200 chars)
```sql
SELECT 
    sequence,
    payload_type,
    LEFT(payload::text, 200) as payload_preview,
    created_time_utc
FROM events 
WHERE event_identifier = '94fdf30a-d77f-4099-98a8-3d9009d2275b'
ORDER BY sequence;
```

### Extract JSON Field from Payload (if payload is JSON)
```sql
SELECT 
    sequence,
    payload_type,
    payload::json->>'gameId' as game_id,
    payload::json->>'status' as status,
    created_time_utc
FROM events 
WHERE event_identifier = '94fdf30a-d77f-4099-98a8-3d9009d2275b'
  AND payload_type = 'flutter.smf.se.game.afl.GameStarted';
```

## Data Quality Checks

### Check for Missing Sequences
```sql
WITH sequences AS (
    SELECT 
        sequence,
        LAG(sequence) OVER (ORDER BY sequence) as prev_sequence
    FROM events 
    WHERE event_identifier = '94fdf30a-d77f-4099-98a8-3d9009d2275b'
)
SELECT 
    prev_sequence,
    sequence,
    sequence - prev_sequence as gap
FROM sequences
WHERE sequence - prev_sequence > 1;
```

### Check for Duplicate Sequences
```sql
SELECT 
    sequence,
    COUNT(*) as count
FROM events 
WHERE event_identifier = '94fdf30a-d77f-4099-98a8-3d9009d2275b'
GROUP BY sequence
HAVING COUNT(*) > 1;
```

### Events with Null/Empty Payloads
```sql
SELECT 
    event_identifier,
    sequence,
    payload_type,
    CASE 
        WHEN payload IS NULL THEN 'NULL'
        WHEN LENGTH(payload) = 0 THEN 'EMPTY'
        ELSE 'OK'
    END as payload_status
FROM events 
WHERE event_identifier = '94fdf30a-d77f-4099-98a8-3d9009d2275b'
  AND (payload IS NULL OR LENGTH(payload) = 0);
```

## Performance Indexes

### Recommended Indexes
```sql
-- Index for event_identifier lookups
CREATE INDEX IF NOT EXISTS idx_events_event_identifier 
ON events(event_identifier, sequence, created_time_utc);

-- Index for payload_type searches
CREATE INDEX IF NOT EXISTS idx_events_payload_type 
ON events(event_identifier, payload_type);

-- Index for date range queries
CREATE INDEX IF NOT EXISTS idx_events_created_time 
ON events(created_time_utc);

-- Composite index for the main query
CREATE INDEX IF NOT EXISTS idx_events_fetch_query 
ON events(event_identifier, payload_type, created_time_utc, sequence);
```

### Check Index Usage
```sql
SELECT 
    schemaname,
    tablename,
    indexname,
    idx_scan as index_scans,
    idx_tup_read as tuples_read,
    idx_tup_fetch as tuples_fetched
FROM pg_stat_user_indexes
WHERE tablename = 'events'
ORDER BY idx_scan DESC;
```

## Statistics

### Events Per Day
```sql
SELECT 
    DATE(created_time_utc) as event_date,
    COUNT(*) as total_events,
    COUNT(DISTINCT event_identifier) as unique_games,
    COUNT(DISTINCT payload_type) as unique_event_types
FROM events 
WHERE created_time_utc >= CURRENT_DATE - INTERVAL '7 days'
GROUP BY DATE(created_time_utc)
ORDER BY event_date DESC;
```

### Most Common Event Types
```sql
SELECT 
    payload_type,
    COUNT(*) as count,
    COUNT(DISTINCT event_identifier) as games,
    ROUND(AVG(LENGTH(payload))) as avg_payload_size
FROM events 
WHERE created_time_utc >= CURRENT_DATE - INTERVAL '30 days'
GROUP BY payload_type
ORDER BY count DESC
LIMIT 20;
```

### Database Size
```sql
SELECT 
    pg_size_pretty(pg_total_relation_size('events')) as total_size,
    pg_size_pretty(pg_relation_size('events')) as table_size,
    pg_size_pretty(pg_total_relation_size('events') - pg_relation_size('events')) as indexes_size;
```

## Cleanup Queries (Use with Caution!)

### Find Old Events (for archival)
```sql
SELECT 
    DATE(created_time_utc) as event_date,
    COUNT(*) as events,
    pg_size_pretty(SUM(LENGTH(payload))) as total_payload_size
FROM events 
WHERE created_time_utc < CURRENT_DATE - INTERVAL '90 days'
GROUP BY DATE(created_time_utc)
ORDER BY event_date;
```

### Count Events Older Than X Days
```sql
SELECT COUNT(*) as old_events
FROM events 
WHERE created_time_utc < CURRENT_DATE - INTERVAL '90 days';
```

## Troubleshooting

### Find Events with Unusual Characteristics
```sql
-- Very large payloads
SELECT 
    event_identifier,
    sequence,
    payload_type,
    LENGTH(payload) as size_bytes,
    pg_size_pretty(LENGTH(payload)::bigint) as size_human
FROM events 
WHERE LENGTH(payload) > 1048576  -- 1MB
ORDER BY LENGTH(payload) DESC
LIMIT 20;

-- Events far in the future or past
SELECT 
    event_identifier,
    sequence,
    payload_type,
    created_time_utc,
    created_time_utc - CURRENT_TIMESTAMP as time_diff
FROM events 
WHERE created_time_utc > CURRENT_TIMESTAMP + INTERVAL '1 day'
   OR created_time_utc < CURRENT_TIMESTAMP - INTERVAL '365 days'
ORDER BY created_time_utc;
```

### Connection Test Query
```sql
-- Simple test to verify connection works
SELECT 
    current_database() as database,
    current_user as user,
    version() as postgres_version,
    NOW() as current_time;
```

## Export Data for Testing

### Export as CSV
```sql
COPY (
    SELECT 
        event_identifier,
        sequence,
        payload_type,
        created_time_utc
    FROM events 
    WHERE event_identifier = '94fdf30a-d77f-4099-98a8-3d9009d2275b'
    ORDER BY sequence
) TO '/tmp/game_events.csv' WITH CSV HEADER;
```

### Export as JSON
```sql
SELECT json_agg(row_to_json(t))
FROM (
    SELECT 
        event_identifier,
        sequence,
        payload_type,
        payload,
        created_time_utc
    FROM events 
    WHERE event_identifier = '94fdf30a-d77f-4099-98a8-3d9009d2275b'
    ORDER BY sequence
) t;
```
