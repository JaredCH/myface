# MyFace Test Environment Setup

This document describes the complete test environment setup for developing and testing MyFace changes safely, isolated from the production site.

## Environment Overview

| Component | Production (Live VPS) | Test (This Server) |
|-----------|----------------------|-------------------|
| **Database** | `myface` | `myface_test` |
| **Web Port** | 5000 | 5001 |
| **Tor SOCKS** | 9052 | 9053 |
| **Tor Control** | - | 9054 |
| **Hidden Service Dir** | `tor/hidden_service/` | `tor/hidden_service_test/` |
| **Tor Config** | `tor/torrc` | `tor/torrc.test` |
| **Web Log** | `web.out` | `test-web.out` |
| **PID File** | `web.pid` | `test-web.pid` |
| **Publish Dir** | `publish/` | `publish-dev/` |
| **ASP.NET Environment** | Production | Test |
| **.onion Address** | [Production onion] | `sigtea3fdb7g3o7zdfqkrh4kpechqy4mipkhzpoglcbeg22qzzqgjsad.onion` |

## Quick Start Guide

### Initial Setup (One-Time)

```bash
cd /home/server/myface

# 1. Generate test vanity .onion address (already done)
# The test address is: sigtea3fdb7g3o7zdfqkrh4kpechqy4mipkhzpoglcbeg22qzzqgjsad.onion

# 2. Setup test Tor configuration (already done)
# ./scripts/test/setup-tor-test.sh
```

### Regular Development Workflow

```bash
cd /home/server/myface

# 1. Fetch latest production data (safe - read-only)
./scripts/test/fetch-live-db.sh

# 2. Build and deploy test version
./scripts/test/deploy-test.sh

# 3. Start test environment
./scripts/test/start-test.sh

# 4. Access test site:
#    - Local: http://localhost:5001
#    - Tor: http://sigtea3fdb7g3o7zdfqkrh4kpechqy4mipkhzpoglcbeg22qzzqgjsad.onion

# 5. Make code changes, then rebuild:
./scripts/test/stop-test.sh
./scripts/test/deploy-test.sh
./scripts/test/start-test.sh

# 6. When done testing:
./scripts/test/stop-test.sh
```

## Script Reference

### Test Environment Scripts (`scripts/test/`)

- **`generate-vanity.sh <prefix> [threads]`** - Generate vanity .onion address
- **`setup-tor-test.sh`** - Create test Tor configuration
- **`fetch-live-db.sh`** - Download and import production database to `myface_test`
- **`deploy-test.sh`** - Build and publish MyFace for testing
- **`start-test.sh`** - Start Tor and web server for test environment
- **`stop-test.sh`** - Stop test environment
- **`README.md`** - Detailed test environment documentation

### Live Environment Scripts (`scripts/sigil/`)

- **`backup-live-assets.sh`** - Production backup script (runs on live VPS)
- **`deploy-latest.sh`** - Production deployment (runs on live VPS)
- **`fetch-live-db.sh`** - Download production data (runs on dev machine)
- **`stand-down.sh`** - Stop production services (runs on live VPS)
- **`INSTRUCTIONS.txt`** - Production operations documentation

## Configuration Files

### Test Configuration
- `MyFace.Web/appsettings.Test.json` - Test database connection (`myface_test`)
- `tor/torrc.test` - Test Tor hidden service config
- `tor/hidden_service_test/` - Test onion keys and hostname

### Production Configuration  
- `MyFace.Web/appsettings.json` - Production database connection
- `tor/torrc` - Production Tor hidden service config
- `tor/hidden_service/` - Production onion keys and hostname

## Safety Features

✅ **Complete isolation** - Test and production never interfere with each other
✅ **Separate databases** - `myface_test` vs `myface`
✅ **Different ports** - No port conflicts (5001 vs 5000, 9053 vs 9052)
✅ **Read-only live access** - Test scripts only download from production, never write
✅ **Explicit naming** - All test files clearly marked as "test"
✅ **Separate .onion addresses** - Test has its own vanity address starting with "sigte"

## Common Tasks

### Refresh Test Data from Production

```bash
cd /home/server/myface
./scripts/test/fetch-live-db.sh
```

This will:
1. SSH to production VPS and find latest backup
2. Download the database dump
3. Drop and recreate `myface_test` database
4. Import fresh production data

### Check Test Server Status

```bash
# Check if running
ps aux | grep -E "(MyFace|tor.*torrc.test)" | grep -v grep

# View recent logs
tail -f test-web.out

# View Tor logs
tail -f tor/tor-test.log
```

### Access Test Site

**Via Tor Browser:**
1. Open Tor Browser
2. Navigate to: `http://sigtea3fdb7g3o7zdfqkrh4kpechqy4mipkhzpoglcbeg22qzzqgjsad.onion`

**Via Local Browser (without Tor):**
1. Navigate to: `http://localhost:5001`
2. Note: Some features requiring .onion address may not work

### Debugging

```bash
# Check Tor test service
cat tor/hidden_service_test/hostname

# Test database connection
psql -h localhost -U postgres -d myface_test -c "SELECT COUNT(*) FROM \"Users\";"

# View full web server log
less test-web.out

# Check for port conflicts
lsof -i :5001
lsof -i :9053
```

## Workflow Comparison

### Production Deployment (Live VPS)
```bash
# SSH to live VPS
ssh nessy@173.212.225.125

# Deploy latest code
cd ~/myface/myface
FORCE_UPDATE=true ./scripts/sigil/deploy-latest.sh

# Or use redeploy.sh directly
./redeploy.sh
```

### Test Deployment (This Server)
```bash
# On dev/test server
cd /home/server/myface

# Fetch fresh data
./scripts/test/fetch-live-db.sh

# Deploy and test
./scripts/test/deploy-test.sh
./scripts/test/start-test.sh
```

## Directory Structure

```
myface/
├── scripts/
│   ├── test/              # Test environment scripts
│   │   ├── generate-vanity.sh
│   │   ├── setup-tor-test.sh
│   │   ├── fetch-live-db.sh
│   │   ├── deploy-test.sh
│   │   ├── start-test.sh
│   │   ├── stop-test.sh
│   │   └── README.md
│   ├── sigil/             # Production/live scripts
│   │   ├── backup-live-assets.sh
│   │   ├── deploy-latest.sh
│   │   ├── fetch-live-db.sh
│   │   ├── stand-down.sh
│   │   └── INSTRUCTIONS.txt
│   └── live/              # (Reserved for future use)
├── tor/
│   ├── torrc              # Production Tor config
│   ├── torrc.test         # Test Tor config
│   ├── hidden_service/    # Production keys
│   ├── hidden_service_test/ # Test keys
│   ├── data/              # Production Tor data
│   ├── data_test/         # Test Tor data
│   └── vanity/            # Vanity address generator
├── MyFace.Web/
│   ├── appsettings.json        # Production config
│   ├── appsettings.Test.json   # Test config
│   └── appsettings.Development.json
├── publish/               # Production build output
├── publish-dev/           # Test build output
├── web.out                # Production web log
├── test-web.out           # Test web log
├── web.pid                # Production process ID
└── test-web.pid           # Test process ID
```

## Notes

- Both test and production can run simultaneously on this server for testing purposes
- The production .onion address is different and runs on the live VPS
- Test database is completely isolated - experiments are safe
- To deploy to production, you must SSH to the live VPS
- Test environment uses ASPNETCORE_ENVIRONMENT=Test which loads appsettings.Test.json

## Troubleshooting

### Test server won't start
```bash
# Check if port is in use
lsof -i :5001

# Kill any hanging processes
./scripts/test/stop-test.sh

# Check logs
tail -50 test-web.out
```

### Database connection issues
```bash
# Verify test database exists
psql -h localhost -U postgres -l | grep myface_test

# Recreate if needed
./scripts/test/fetch-live-db.sh
```

### Tor issues
```bash
# Check Tor is running
ps aux | grep "torrc.test"

# View Tor logs
tail -50 tor/tor-test.log

# Restart Tor
./scripts/test/stop-test.sh
./scripts/test/start-test.sh
```
