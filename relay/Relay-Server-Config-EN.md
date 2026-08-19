# Relay Server Configuration Guide (Windows / Linux)

## Files

- `relay.py`: relay server main program
- `admin.py`: web admin panel (default port 7001)
- `gamemode.py`: game-mode extension framework (write your own modes)
- `config.json`: all configuration
- `requirements.txt`: Python dependencies

## Windows

1. Install Python 3.10+ (tick "Add to PATH").
2. Double-click `启动联机服.bat`: it installs dependencies and starts the relay (7000) and the admin panel (7001).
3. Client connect address: `your-IP:7000`; admin: `http://127.0.0.1:7001`.

## Linux

```bash
chmod +x start.sh stop.sh
./start.sh
```

systemd:

```bash
sudo cp sfm-relay.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now sfm-relay
```

## Configuration (config.json)

| Field | Description |
|---|---|
| `host` / `port` | Bind address and port (default 0.0.0.0:7000) |
| `domain` | Public domain (for reporting) |
| `master_report` | Master server reporting endpoint; leave empty for LAN-only / not listed |
| `secret` | Client connection secret; change to a long random string |
| `max_online` / `max_rooms` / `room_max_players` | Online / room / per-room limits |
| `room_timeout` | Empty-room cleanup seconds |
| `lan_only` | true = only private-network IPs allowed |
| `admin.user` / `admin.password` | Admin panel login |

Restart the service after changing the config.

## Submit to the Server List

See the "Server List Submission & Review" section in the repository root README (English: README_EN.md).
