#!/bin/bash
# Jarvis Daemon v3 — launchd wrapper.
# Sets up environment before launching the Node.js daemon.
export PATH="/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/Library/Frameworks/Python.framework/Versions/3.14/bin"
export HOME="/Users/jamestunick"

cd /Users/jamestunick/Applications/jAIrvisXR/Daemon

# Ensure tmp dir exists
mkdir -p /tmp/jarvis-daemon

# Run preflight (exit on critical failure)
node preflight.mjs
if [ $? -ne 0 ]; then
    echo "Preflight failed, exiting" >&2
    exit 1
fi

# Start daemon
exec node jarvis-daemon.mjs
