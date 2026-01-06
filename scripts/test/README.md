# Test Environment Scripts

These scripts manage the **test** deployment of MyFace on this server. The test environment runs completely separately from production with its own:
- Database: `myface_test` (vs production `myface`)
- Tor hidden service: Different .onion address on port 9053
- Web server: Runs on `http://localhost:5001` (vs production `5000`)
- Configuration: Uses `ASPNETCORE_ENVIRONMENT=Test`

## Quick Start

### 1. Generate Test Vanity Address (One-time)
```bash
./generate-vanity.sh sigte 4
```
Generates a `.onion` address starting with "sigte" using 4 CPU threads. Keys are automatically installed to `tor/hidden_service_test/`.

### 2. Setup Test Tor Config (One-time)
```bash
./setup-tor-test.sh
```
Creates `tor/torrc.test` with test-specific settings.

### 3. Fetch Live Database to Test
```bash
./fetch-live-db.sh
```
Downloads the latest production backup from the live VPS and imports it into the `myface_test` database. **Safe** - does not affect production.

### 4. Build and Deploy Test
```bash
./deploy-test.sh
```
Builds MyFace and publishes to `publish-dev/`.

### 5. Start Test Environment
```bash
./start-test.sh
```
Starts Tor and the web server. Access via:
- Local: http://localhost:5001
- Onion: http://[generated-address].onion (shown in output)

### 6. Stop Test Environment
```bash
./stop-test.sh
```
Cleanly shuts down test Tor and web server.

## Full Workflow

```bash
# One-time setup
cd /home/server/myface/scripts/test
chmod +x *.sh
./generate-vanity.sh sigte 4
./setup-tor-test.sh

# Regular test cycle
./fetch-live-db.sh          # Get fresh production data
./deploy-test.sh            # Build latest code
./start-test.sh             # Start test site
# ... test changes ...
./stop-test.sh              # Stop when done
```

## Environment Isolation

| Component | Production | Test |
|-----------|-----------|------|
| Database | `myface` | `myface_test` |
| Web Port | 5000 | 5001 |
| Tor SOCKS | 9052 | 9053 |
| Tor Control | - | 9054 |
| Hidden Service | `tor/hidden_service/` | `tor/hidden_service_test/` |
| Tor Config | `tor/torrc` | `tor/torrc.test` |
| Web Log | `web.out` | `test-web.out` |
| PID File | `web.pid` | `test-web.pid` |
| Publish Dir | `publish/` | `publish-dev/` |
| ASP.NET Env | Production | Test |

## Safety Features

- **All paths are test-specific** - no risk of overwriting production files
- **Separate database** - `myface_test` is completely isolated
- **Different ports** - no conflicts with running production services
- **Read-only from live** - fetch scripts only download, never write to production
- **Explicit naming** - all logs and output files clearly marked as "test"

## Notes

- Test environment runs on the **same machine** as this dev environment, not on the live VPS
- The vanity generation for "sigte" may take 2-15 minutes depending on your CPU
- Test database is **dropped and recreated** on each fetch-live-db.sh run for consistency
- Both production and test can run simultaneously without interference
