#!/usr/bin/env bash
set -e
cd "$(dirname "$0")"
export SFM_CFG="$PWD/config.json"
export SFM_STATE="$PWD/state.json"
export SFM_CMD="$PWD/commands.json"
export SFM_LOG="$PWD/relay.log"
export SFM_ADMIN_PORT=7001
if ! command -v python3 >/dev/null 2>&1; then
  echo "[ERROR] python3 not found. Install Python 3.10+."
  exit 1
fi
python3 -m pip install -r requirements.txt -q
nohup python3 relay.py >>relay.log 2>&1 &
echo "relay started (pid $!) on port 7000"
sleep 1
nohup python3 admin.py >>admin.log 2>&1 &
echo "admin started (pid $!) at http://127.0.0.1:7001"
echo "To stop: ./stop.sh"
