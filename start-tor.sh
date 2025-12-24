#!/bin/bash
# Ensure correct permissions for Tor hidden service directory
chmod 700 /home/server/myface/tor/hidden_service

# Start Tor with the local configuration
echo "Starting Tor with config /home/server/myface/tor/torrc..."
tor -f /home/server/myface/tor/torrc
