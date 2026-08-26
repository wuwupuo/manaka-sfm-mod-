#!/usr/bin/env bash
cd "$(dirname "$0")"
pkill -f "python3 relay.py" 2>/dev/null || true
pkill -f "python3 admin.py" 2>/dev/null || true
echo "stopped"
