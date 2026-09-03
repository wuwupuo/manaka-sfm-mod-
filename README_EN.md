Please note that due to numerous unnecessary attacks on my server from various countries, I will be restricting IP addresses from most countries. My server currently supports the following countries:
Mainland China, Taiwan, Macau, Hong Kong, the United States, Japan, Russia, and the United KingdomSouth Korea
If your country is not among the countries listed above, please submit lssues. If you still cannot access the site using your IP address, please send your IP address to the email address provided, and I will add your IP address to the whitelist.



# SFM Online (SFM Online Connectivity)

> 🌐 [English](README_EN.md) | Chinese

A multiplayer mod developed for the commercial Unity game Secret Flasher: Manaka, based on BepInEx + IL2CPP.

This project focuses solely on the engineering layer: connecting single-player games to a network (rooms, chat, player status/action synchronization, gameplay controls, security and maintenance). It **does not contain, create, or generate any game content.** The decompiled Assembly-CSharp is for internal technical reference only and will not be distributed externally.

---

## ✨ Functions

- **Three connection methods:** LAN / Netcom tunneling / Online server (total server list)
- **Room System**: Room creation (graphical verification code), real-time room list refresh, room owner kicking out players, room settings (skill/attribute bonus on/off).
- **Player Synchronization**: Coordinates, orientation, action status, hip center point on the ground, clothing/undress level, exposed state, NPC actions and positions synchronized.
- **Controls and Gameplay**: Vibrator/Extendable Rod (4 settings), Toy Wearing (Clitoris/Nipple/Anal Plug/Blindfold), Handcuffs (Front/Back/Timed), Sitting/Standing Switch, Undressing Cycle, Urination/Forced Orgasm (with Water Flow Effects), Remote Action List (All Actions Triggered)
- **Force Restore**: HUD button / `Alt+F3`, clear all control/toy/action states with one click.
- **Language:** Switch between Chinese and English interfaces
- **Security**: AES/HMAC session encryption, login graphical CAPTCHA (forced upon failure), email CAPTCHA pre-verification, chat keyword blocking/rate limiting, server-side submission review.
- **Admin Panel**: The online server comes with a built-in web-based admin panel (Chinese and English languages), supporting overview/rooms/social/announcements/accounts/mods/logs/settings.

---

## 🚀 Client Usage Tutorial

1. Install the BepInEx environment (game itself) required by the integration package.
2. The client is **open source**: source code at [SFMOnline.Client/](SFMOnline.Client/) (encryption removed, directly compilable). If you prefer prebuilt, download the compiled DLL from this repository [client/SFMOnline_1.0.8.dll](client/SFMOnline_1.0.8.dll), rename it to `SFMOnline.dll`, and place it in `BepInEx/plugins/`.
3. Start the game: `F10` online menu, `F12` normal menu, `F11` chat.
4. After logging in: Press `F10` → Select a server from the main server list → Create/join a room on the room page; or create a room via LAN/internal network penetration.
5. Press `Alt+F3` in-game to force a complete restoration of all control states at any time.

### 🧩 Mod Development (New)

The client ships with the **SFMOnline.Ext framework (314+ APIs)**. Anyone can build multiplayer gameplay mods:

- Full documentation: [docs/](docs/) (client mod guide, complete API reference, server plugin guide, design patterns, gameplay idea library, example walkthrough)
- Compilable examples: [examples/](examples/) (quiz game [client + server plugin] / hide & seek / quest story)
- Server plugin template: [server-plugin/](server-plugin/)
- **The relay server itself stays closed-source**, but the plugin interface (`plugins/*.py`) is open for community development and works together with client mods.

For detailed instructions, please refer to [Client Usage Guide](client/Client-Usage-EN.md) / [Client Usage Guide](client/Client-Usage-EN.md).

---

## 📦 Download (Release)

- [SFMOnline_Relay_Windows_v1.0.7.zip](release/SFMOnline_Relay_Windows_v1.0.7.zip): Windows One-Click Version (Double-click to start the online server after extraction.bat)
- [SFMOnline_Relay_Linux_v1.0.7.zip](release/SFMOnline_Relay_Linux_v1.0.7.zip): One-click Linux version (after decompression, run ./start.sh)
- [SFMOnline_Client_v1.0.10.zip](release/SFMOnline_Client_v1.0.10.zip): Client integration package (after decompression, put the contents of BepInEx into the game directory, or directly download client/SFMOnline_1.0.10.dll and put it into BepInEx/plugins/).

> **Client v1.0.10 Update**: TCP+UDP co-linking (in-room high-frequency sync via UDP 8000, menus/controls stay on TCP, bypasses port-7000 blocking); **automatic room mod sync** (host mod manifest → auto-compare on join → auto-download missing files → hot-load → auto-reload game); **per-map NPC authority sync** (first player in a map is authority, full-map NPC sync, authority transfers on leave); **drop item sync** (see others' dropped items with player-name labels, pick them up, permission control, F1 recall all); **multi-channel email captcha** (Resend + QQ auto-fallback); **plugin admin page extension**.
>
> **Client v1.0.9 Update**: Fixed room chat "unknown message type" spam; fixed server lag at 5+ players (pre-serialized broadcasts + lower sync rates); fixed player getting stuck in place (auto-unlink when controller goes offline); F10 menu now has a Friends tab with red dot badge; F11 chat has dual tabs (Room / Lobby); tampered client auto-downloads the official build to repair itself; new SFMOnlineMods folder auto-loads mods; dev docs rewritten for beginners.
>
> **Client v1.0.8 Update**: Client source code is now **open source** (string encryption removed). Added **27 new `remote.*` gameplay control functions** (action/vibrate/thrust/goods/undress/orgasm/pee/crouch/crawl/sit/handcuff/collar/blindfold/fx/teleport — target a specific player or broadcast to all). Added player queries (net.get_player_name / net.find_uid / net.get_players_info). Framework now exposes **314 functions** for mod development (see docs/).
>
> **Client v1.0.6 Update**: Fixed the issue of remote players' hair/ribbons (slim elongated renderers) disappearing. Adjusted the ghost renderer filter logic so it no longer wrongly hides slim renderers.
> 
> **Client v1.0.5 Update**: Fixed F10 not opening the online menu (key detection moved to the game main loop, no longer relying on GUI events). Added cross-navigation guide buttons at the top of the online menu and main menu ([Main Menu F12] / [Online Menu F10]).
> 
> **Client v1.0.4 Update**: Fixed the registration UI layout (password/hint text no longer covers input fields; hint text wraps automatically without overflowing the screen). The map now displays obstacles at their real size and shows wall boundaries (different obstacles/walls have different sizes, no longer uniform).
> 
> **Client v1.0.3 Update**: Client hardening (DLL obfuscation to prevent decompilation + RSA signed update packages to prevent tampering). The update package is now verified by signature before replacing the local plugin; if verification fails, the replacement is refused.

>  **Server v1.0.3 Update**: The room status in the backend is now refreshed every 5 seconds (previously 30 seconds), so changes are visible in the backend more quickly after deleting/creating a room; the admin panel is now synchronized.

>  **Client v1.0.2 Update**: Optimized multi-room synchronization performance (bone synchronization/motion synchronization with server-side frequency limiting, client-side motion/bone synchronization frequency reduction), resolving issues such as lag during multi-player room synchronization and control command delays/losses that prevented controlled characters from moving.

>  **Server v1.0.1 Fixes:** Relaxed the online server rate limit (RATE_LIMIT 600→4000). When the traffic is too high, only discarding and frequency limiting warnings are given, and the player connection will no longer be directly disconnected (solving the issue of players being kicked out about 10 seconds after entering the room).

---

## 🖥️ Online Server

Supports **one-click startup for Windows** and **Linux (systemd / scripts)**, and comes with a web-based backend.

> ⚠️ **The online server must share data with the main server (master_report) during operation.** Otherwise, the server will not be able to start normally, regardless of whether it is in the main server's server list. Without master_report configured, it will refuse to start.

```bash
# Linux
chmod +x start.sh stop.sh
./start.sh
# Admin Panel: http://127.0.0.1:7001
```

In Windows, simply double-click `Start Online Server.bat`.

For configuration details, please refer to [Online Server Configuration Tutorial](relay/Online Server Configuration Tutorial.md) / [Relay Server Config Guide](relay/Relay-Server-Config-EN.md).

---

## 📜 Agreement and Instructions

For details on data sharing agreements, user agreements, server connection deployment agreements, disclaimers, etc., please refer to [AGREEMENTS.md](AGREEMENTS.md) / [AGREEMENTS_EN.md](AGREEMENTS_EN.md).

---

## 📮 Server List Submission and Review

Want to display your server in the server list?

Please submit your articles to: 3197377739@qq.com

Please include the following information when submitting your submission: server name, server address (domain name/IP:port), administrator contact information, server description, and server duration.

### Review Mechanism (Please be sure to read)

We will **review** your server at any time and determine whether it is allowed to appear in the server list; servers that fail the review or violate the rules midway will be removed.
- **Advertising of any kind is prohibited on the server (including but not limited to advertising, traffic generation, and promotion of other groups/sites/servers).**
- **No payment is allowed to enter**. Players are prohibited from being asked to pay, purchase items, or engage in monetary transactions in any form.
- **It is prohibited to exchange in-game items/permissions/services under the guise of sponsorship.** Unpaid sponsorship (voluntary and without exchanging any in-game benefits) is not prohibited.
- Even after approval, the above rules must still be followed; servers that violate the rules will be **removed from the list and banned (domain/IP) depending on the severity of the offense**.

---

## 📞 Contact Us

- QQ Group for discussion: **1095532943**
- Telegram：[https://t.me/SFMMM11](https://t.me/SFMMM11)
- GitHub:[https://github.com/discussion/free-sfm-mod-](https://github.com/discussion/free-sfm-mod-)
- email：**3197377739@qq.com**

---

## 🚀Sponsorship
https://zanzhu.wuwupuo.cc.cd


## ⚖️ Disclaimer

This project is for technical learning and exchange purposes only and is not related to the game copyright holder.
Please support the official game; the user shall bear all consequences arising from the use of this module.
- This project is prohibited from being used for commercial purposes.
This repository contains open-source code for the online server. The client is closed-source, and the repository provides pre-compiled DLLs for download (containing no game resources/decompiled content).

## 📄 Open Source License

MIT License, see [LICENSE](LICENSE) for details.

## Our other project
sfmmm Creative Workshop: https://github.com/b9348/sfmmm
---

## ✨ Features

- **Three ways to play together**: LAN / tunnel (NAT traversal) / relay server (master server list)
- **Rooms**: create (with image captcha), live-updating room list, host kick, room settings (skill/stat bonuses toggle)
- **Player sync**: position, rotation, action state, hip center-point ground alignment, clothes/undress level, exposure state, NPC motion & position sync
- **Control features**: vibrator / piston 4 stages, toy wear (clit / nipple / anal plug / blindfold), handcuffs (front / behind / timed), sit-stand toggle, undress cycle, pee / forced climax (with water effects), remote full action list
- **Force Reset**: HUD button / `Alt+F3` clears all control/toy/action states
- **Languages**: Chinese / English UI switch
- **Security**: AES/HMAC session encryption, login image captcha (mandatory after a failed password login), email code requires captcha first, chat filtering/rate limits, server submission review
- **Admin panel**: bilingual web dashboard (overview / rooms / social / announcement / accounts / mods / logs / settings)

---

## 🚀 Client Usage

1. Install the required BepInEx environment (the game itself).
2. Download the compiled client from [client/SFMOnline_1.0.6.dll](client/SFMOnline_1.0.6.dll) (the client is **closed-source**), rename it to `SFMOnline.dll` and put it into `BepInEx/plugins/`.
3. Launch the game: `F10` online menu, `F12` general menu, `F11` chat.
4. After login: `F10` → Server List → pick a relay; then create/join a room; or use LAN/tunnel rooms.
5. Press `Alt+F3` anytime to force-reset all control states.

See [Client Usage Guide](client/Client-Usage-EN.md) (Chinese) for details.

---

## 📦 Downloads (Release)

- [SFMOnline_Relay_Windows_v1.0.3.zip](release/SFMOnline_Relay_Windows_v1.0.3.zip): Windows one-click (unzip, run 启动联机服.bat)
- [SFMOnline_Relay_Linux_v1.0.3.zip](release/SFMOnline_Relay_Linux_v1.0.3.zip): Linux one-click (unzip, run ./start.sh)
- Client: just download SFMOnline_1.0.2.dll into BepInEx/plugins/ (closed-source, no compilation needed).

---

## 🖥️ Relay Server (Open Source)

Works on **Windows (one-click)** and **Linux (script / systemd)**, with a built-in web admin panel.

> ⚠️ **A relay server MUST share data with the master server (master_report) while running**, otherwise it cannot operate normally, whether or not it is on the master server list; missing master_report will refuse to start.

```bash
# Linux
chmod +x start.sh stop.sh
./start.sh
# Admin: http://127.0.0.1:7001
```

On Windows just double-click `启动联机服.bat`.

See [Relay Server Config Guide](relay/Relay-Server-Config-EN.md) (Chinese) for details.

---

## 📜 Agreements

Data-sharing, user, deployment and disclaimer agreements: [AGREEMENTS.md](AGREEMENTS.md) / [AGREEMENTS_EN.md](AGREEMENTS_EN.md).

---

## 📮 Server List Submission & Review

Want your server to appear in the server list?

**Submit to: 3197377739@qq.com**

Please include: server name, server address (domain/IP:port), admin contact, server description, and how long it has been running.

### Review Rules (please read carefully)

- We **review servers at any time** and decide whether they may stay on the server list; servers that fail review or violate rules will be removed.
- **No advertising of any kind** inside the server (including promoting other groups/sites/servers).
- **No pay-to-enter**, no requirement for players to pay, buy items, or conduct any money transactions.
- **No sponsorships in exchange for in-game items/permissions/services**; free-will donations (with no in-game benefits) are allowed.
- Approved servers must keep following these rules; violators will be **removed from the list and may be banned (domain/IP)**.

---

## 📞 Contact

- QQ Group: **1095532943 129733687**
- Telegram: [https://t.me/SFMMM11](https://t.me/SFMMM11)
- Donation site: [https://zanzhu.wuwupuo.ccwu.cc/](https://zanzhu.wuwupuo.ccwu.cc/)
- GitHub: [https://github.com/wuwupuo/manaka-sfm-mod-](https://github.com/wuwupuo/manaka-sfm-mod-)
- email：**3197377739@qq.com**

---
## 🎁 Sponsorship

https://zanzhu.wuwupuo.cc.cd

## ⚖️ Disclaimer

- This project is for technical learning and communication only; it is not affiliated with the game's rights holders.
- Please support the official game. Use of this mod is at your own risk.
- Commercial use is prohibited.
- This repository open-sources the **relay server source code**. The client is **closed-source**; a compiled DLL is provided for download (no game assets / decompiled content included).

## 📄 License

MIT License. See [LICENSE](LICENSE).

## Our other project
sfmmm Creative Workshop
https://github.com/b9348/sfmmm
