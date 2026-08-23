Please note that due to too many unnecessary countries attacking my servers, I will be restricting access from most country IPs. The countries my server currently supports are: 

mainland China, Taiwan, Macau, Hong Kong, USA, Japan, Russia, UK.

If your country isn't listed above, please submit a ticket. If you still find that your IP can't access the server, please send your IP address via email and I'll add your IP to the whitelist


# SFM Online

> 🌐 English | [中文](README.md)

# SFM Online

A **multiplayer mod** for the commercial Unity game *Secret Flasher: Manaka*, built on BepInEx + IL2CPP.

This project is purely engineering: it adds online play (rooms, chat, state/animation sync, control features, security & ops) to a single-player game. It **does not include, create, or generate any game content**. Decompiled Assembly-CSharp is used only as internal technical reference and is never distributed.

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
2. Download the compiled client from [client/SFMOnline_1.0.0.dll](client/SFMOnline_1.0.0.dll) (the client is **closed-source**), rename it to `SFMOnline.dll` and put it into `BepInEx/plugins/`.
3. Launch the game: `F10` online menu, `F12` general menu, `F11` chat.
4. After login: `F10` → Server List → pick a relay; then create/join a room; or use LAN/tunnel rooms.
5. Press `Alt+F3` anytime to force-reset all control states.

See [Client Usage Guide](client/Client-Usage-EN.md) (Chinese) for details.

---

## 📦 Downloads (Release)

- [SFMOnline_Relay_Windows_v1.0.0.zip](release/SFMOnline_Relay_Windows_v1.0.0.zip): Windows one-click (unzip, run 启动联机服.bat)
- [SFMOnline_Relay_Linux_v1.0.0.zip](release/SFMOnline_Relay_Linux_v1.0.0.zip): Linux one-click (unzip, run ./start.sh)
- Client: just download SFMOnline_1.0.0.dll into BepInEx/plugins/ (closed-source, no compilation needed).

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

- QQ Group: **1095532943**
- Telegram: [https://t.me/SFMMM11](https://t.me/SFMMM11)
- Donation site: [https://zanzhu.wuwupuo.ccwu.cc/](https://zanzhu.wuwupuo.ccwu.cc/)
- GitHub: [https://github.com/wuwupuo/manaka-sfm-mod-](https://github.com/wuwupuo/manaka-sfm-mod-)

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

## 我们的另一个项目
sfmmm创意工坊https://github.com/b9348/sfmmm
