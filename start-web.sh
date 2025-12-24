#!/bin/bash
# Start the web application on port 5000 to match Tor config
# Use Production environment to ensure security settings (Cookie policy) are applied correctly for Tor
export ASPNETCORE_URLS="http://localhost:5000"
export ASPNETCORE_ENVIRONMENT="Production"

echo "Starting MyFace.Web on http://localhost:5000 (Production)..."
cd MyFace.Web
# --no-launch-profile ensures we don't pick up Development settings from launchSettings.json
dotnet run --no-launch-profile
