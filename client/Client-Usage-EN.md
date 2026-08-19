# Client Usage Guide

## Install

1. Make sure you have the game *Secret Flasher: Manaka* integrated package with BepInEx installed.
2. The client is **closed-source**. Download the compiled `SFMOnline_1.0.0.dll` from the `client/` folder, rename it to `SFMOnline.dll`, and put it into `BepInEx/plugins/`.
3. Launch the game.

## Controls

| Key | Function |
|---|---|
| F10 | Online menu (after login: server list / create-join room / LAN) |
| F11 | Chat |
| F12 | General menu (profile / settings / about) |
| Alt+F3 | Force-reset all control states (HUD button top-right too) |
| Shift+F9 | Export scene component list to `BepInEx/SFMOnline_export.txt` |

## Online Flow

1. Log in (after one failed password login, an image captcha is required; email verification codes also require the image captcha first).
2. F10 → Server List → choose a relay server.
3. Rooms page: create a room (enter the image captcha) or join one; LAN/tunnel rooms live under the "LAN/Tunnel" section.
4. Inside a room you can request control of other players (after they accept) and use all advanced control features.

## FAQ

- White screen / freeze on launch: first load initializes with a delay on low-end machines; wait a few seconds.
- Room list empty: the list refreshes automatically; you can also press "Refresh Room List".
- Control has no effect: make sure the other player accepted control; if it still fails, press Alt+F3 to force reset.
