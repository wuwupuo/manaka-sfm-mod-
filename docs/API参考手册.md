# SFM Online Ext API 完整参考（314 个函数）

> 调用方式：`SfmExt.CallFunction("函数名", new SfmExtParams().Set("参数", 值))`

> 返回值：`SfmExtValue`（`.ToFloat()` 数字 / `.ToString()` 文本 / `.ToBool()` 布尔 / `["key"]` 列表）

> 另有 C# 直调等价类（`SfmExtScore.Get`、`SfmExtHud.CreateText` 等），见各模块说明。


## 系统与日志（10 个）

| 函数 | 参数 |
|---|---|
| `misc.every_seconds` | seconds, event, data |
| `misc.get_type` | value |
| `misc.is_null` | value |
| `misc.wait_seconds` | seconds, event, data |
| `system.bridge_ready` |  |
| `system.error` | text |
| `system.ext_version` |  |
| `system.function_exists` | name |
| `system.log` | text |
| `system.warning` | text |


## 字符串（12 个）

| 函数 | 参数 |
|---|---|
| `string.contains` | value, find |
| `string.find` | value, find |
| `string.format` | format |
| `string.join` | sep |
| `string.length` | value |
| `string.lower` | value |
| `string.replace` | value, from, to |
| `string.split` | value, sep |
| `string.substr` | value, start, length |
| `string.tonumber` | value |
| `string.tostring` | value |
| `string.upper` | value |


## 数学（38 个）

| 函数 | 参数 |
|---|---|
| `math.abs` | value |
| `math.acos` | value |
| `math.asin` | value |
| `math.atan` | value |
| `math.ceil` | value |
| `math.clamp` | min, max, value |
| `math.cos` | value |
| `math.deg2rad` | value |
| `math.floor` | value |
| `math.log` | value |
| `math.log10` | value |
| `math.log2` | value |
| `math.max` | a, b |
| `math.min` | a, b |
| `math.pidiv` | value |
| `math.pow` | base, exp |
| `math.quaternion` | x, y, z |
| `math.rad2deg` | value, x, y, z |
| `math.random` | min, max |
| `math.randomint` | min, max |
| `math.round` | value |
| `math.sign` | value |
| `math.sin` | value |
| `math.sqrt` | value |
| `math.tan` | value |
| `math.trunc` | value |
| `math.vector` |  |
| `math.vector3_add` | bx, by, bz |
| `math.vector3_cross` | bx, by, bz |
| `math.vector3_distance` | bx, by, bz |
| `math.vector3_dot` | bx, by, bz |
| `math.vector3_length` |  |
| `math.vector3_lerp` | bx, by, bz, t |
| `math.vector3_normalize` |  |
| `math.vector3_rotate` | rx, ry, rz |
| `math.vector3_scale` | s |
| `math.vector3_sqrlength` |  |
| `math.vector3_sub` | bx, by, bz |


## 变量与列表（15 个）

| 函数 | 参数 |
|---|---|
| `list.add` | name, value |
| `list.clear` | name |
| `list.copy` | list |
| `list.create` | name |
| `list.get` | name, index |
| `list.has` | name, value |
| `list.json` | value |
| `list.remove` | name, value |
| `list.set` | list, key, value |
| `list.size` | name |
| `var.add` | name, value |
| `var.create` | name, value |
| `var.exists` | name |
| `var.get` | name |
| `var.set` | name, value |


## 事件（5 个）

| 函数 | 参数 |
|---|---|
| `event.emit` | event, value |
| `event.emit_net` | event, value |
| `event.get` |  |
| `event.on` |  |
| `event.set` | event, value |


## 文件（6 个）

| 函数 | 参数 |
|---|---|
| `file.delete` | path |
| `file.exists` | path |
| `file.extension` | path |
| `file.get_files` | dir, pattern |
| `file.read` | path |
| `file.write` | path, content |


## 颜色（2 个）

| 函数 | 参数 |
|---|---|
| `color.rgb` | r, g, b |
| `color.rgba` | r, g, b, a |


## 积分表（5 个）

| 函数 | 参数 |
|---|---|
| `score.add` | name, value, sync |
| `score.create` | name, value, synced |
| `score.get` | name |
| `score.list` |  |
| `score.set` | name, value, sync |


## 存档（4 个）

| 函数 | 参数 |
|---|---|
| `save.get` | space, key |
| `save.has` | space, key |
| `save.remove` | space, key |
| `save.set` | space, key, value |


## 骨骼（3 个）

| 函数 | 参数 |
|---|---|
| `bone.find` | uid, bone |
| `bone.getrot` | bone |
| `bone.setrot` | bone, x, y, z |


## 区域与触发（6 个）

| 函数 | 参数 |
|---|---|
| `area.create` | shape, name, x, y, z, radius, height, stage |
| `area.inside` | name, x, y, z |
| `area.remove` | name |
| `trigger.create` | name, x, y, z, radius, touched |
| `trigger.fire` | name, uid |
| `trigger.remove` | name |


## 游戏状态（63 个）

| 函数 | 参数 |
|---|---|
| `camera.get_position` |  |
| `camera.get_rotation` |  |
| `camera.set_pos` |  |
| `camera.set_position` | x, y, z |
| `camera.set_rotation` | x, y, z |
| `chat.send` | text |
| `game.add_ecstasy` | value |
| `game.add_moisture` | value |
| `game.add_stamina` | value |
| `game.block_input` | block |
| `game.deactivate_sex` |  |
| `game.gameover` |  |
| `game.get_daytime` |  |
| `game.get_ecstasy` |  |
| `game.get_item_count` |  |
| `game.get_mental` |  |
| `game.get_moisture` |  |
| `game.get_position` |  |
| `game.get_stage` |  |
| `game.get_stamina` |  |
| `game.is_ingame` |  |
| `game.lock_handcuffs` | type |
| `game.set_action` | action |
| `game.set_adult_goods` | type, stage, on |
| `game.set_cosplay` | name, on |
| `game.set_crouch` | value |
| `game.set_daytime` | value |
| `game.set_ecstasy` | value |
| `game.set_item_count` | count |
| `game.set_moisture` | value |
| `game.set_piston` | stage |
| `game.set_position` | x, y, z |
| `game.set_sex_position` | position |
| `game.set_stage` | stage |
| `game.set_stamina` | value |
| `game.set_vibrator` | stage |
| `game.teleport` |  |
| `game.trigger_orgasm` |  |
| `game.unlock_handcuffs` | type |
| `play.block_input` | block |
| `play.deactivate_sex` |  |
| `play.gameover` |  |
| `play.get_ecstasy` |  |
| `play.get_moisture` |  |
| `play.get_stamina` |  |
| `play.set_action` | action |
| `play.set_crouch` | value |
| `play.set_ecstasy` | value |
| `play.set_moisture` | value |
| `play.set_position` | x, y, z |
| `play.set_sex_position` | position |
| `play.set_stage` | stage |
| `play.set_stamina` | value |
| `play.teleport` |  |
| `play.trigger_orgasm` |  |
| `player.get_name` |  |
| `player.get_position` |  |
| `player.get_uid` |  |
| `player.is_ingame` |  |
| `state.get_daytime` |  |
| `state.get_players` |  |
| `state.get_stage` |  |
| `state.set_daytime` | value |


## 玩家交互（17 个）

| 函数 | 参数 |
|---|---|
| `interact.action` | uid, action |
| `interact.finger` | uid, start |
| `interact.follow` | uid |
| `interact.handcuff` | uid |
| `interact.piston` | uid, stage |
| `interact.summon` | uid |
| `interact.teleport_to` | uid |
| `interact.toy_accept` | uid |
| `interact.toy_all_piston` | stage |
| `interact.toy_all_vibrate` | stage |
| `interact.toy_controller` |  |
| `interact.toy_invite` | uid |
| `interact.toy_linked` |  |
| `interact.toy_reject` | uid |
| `interact.toy_revoke` | uid |
| `interact.undress` | uid |
| `interact.vibrate` | uid, stage |


## 远程玩法控制（27 个）

| 函数 | 参数 |
|---|---|
| `remote.action` | uid, act |
| `remote.action_set` | uid, act |
| `remote.bareta` | uid, on |
| `remote.climax` | uid, on |
| `remote.collar` | on, uid |
| `remote.crawl` | uid |
| `remote.crouch` | uid, on |
| `remote.dress` | uid |
| `remote.fx` | uid, mode, kind |
| `remote.goods` | uid, type |
| `remote.goods_off` | uid, type |
| `remote.handcuff` | uid, mode, duration |
| `remote.handcuff_back` | uid, mode, duration |
| `remote.orgasm` | uid, mode |
| `remote.pee` | uid, mode |
| `remote.pee_stop` | uid |
| `remote.pleasure` | uid |
| `remote.reset` | uid |
| `remote.sit` | uid |
| `remote.stand` | uid |
| `remote.teleport` | uid, x, y, z |
| `remote.thrust` | uid |
| `remote.thrust_set` | uid, stage |
| `remote.undress` | uid, stage |
| `remote.undress_cycle` | uid |
| `remote.unlock` | uid |
| `remote.vibrate` | uid, stage |


## 联机消息（34 个）

| 函数 | 参数 |
|---|---|
| `net.bone_sync` | bone, x, y, z |
| `net.broadcast_event` | event, data |
| `net.find_uid` | name |
| `net.get_local_position` |  |
| `net.get_name` |  |
| `net.get_player_count` |  |
| `net.get_player_distance` | uid |
| `net.get_player_name` | uid |
| `net.get_player_position` | uid |
| `net.get_players` |  |
| `net.get_players_info` |  |
| `net.get_uid` |  |
| `net.is_connected` |  |
| `net.is_server_mod` | name |
| `net.plugin_call` | from, to, op, data |
| `net.room_broadcast` |  |
| `net.room_create` | name, max=8, password |
| `net.room_get_player_name` | uid |
| `net.room_get_players` |  |
| `net.room_get_rooms` |  |
| `net.room_is_host` |  |
| `net.room_join` | room_id, password |
| `net.room_kick` | uid |
| `net.room_leave` |  |
| `net.score_broadcast` | name |
| `net.score_sync` | name, value |
| `net.send_chat` | text, uid |
| `net.send_custom` | type, type, ns, op, data |
| `net.send_to_player` | uid |
| `net.send_to_plugin` | ns, op, data |
| `net.send_to_room` |  |
| `net.send_to_server` |  |
| `net.server_mods` |  |
| `net.trigger` | name |


## 玩法扩展（16 个）

| 函数 | 参数 |
|---|---|
| `achievement.has` | uid, name |
| `achievement.unlock` | uid, name |
| `gameplay.announce` | text, title |
| `gameplay.countdown` | name, seconds |
| `gameplay.countdown_remaining` | name |
| `gameplay.gift` | uid, item, count=1 |
| `gameplay.player_count` |  |
| `gameplay.spectate` | uid |
| `gameplay.spectate_stop` |  |
| `rank.get` | board, key |
| `rank.set` | board, key, score |
| `rank.top` | board, count=10 |
| `team.add` | name |
| `team.get` | uid |
| `team.members` | team |
| `team.set` | uid, team |


## 任务系统（5 个）

| 函数 | 参数 |
|---|---|
| `task.add_checkpoint` | name, checkpoint, x, y, z, radius |
| `task.complete` | name |
| `task.create` | name, title, desc, rp, synced |
| `task.get_progress` | name |
| `task.start` | name |


## NPC 系统（6 个）

| 函数 | 参数 |
|---|---|
| `npc.interact` | name, uid |
| `npc.list_scene` |  |
| `npc.remove` | name |
| `npc.spawn` | name, x, y, z, dialogue |
| `npc.spawn_from_scene` | name, npc_id, x, y, z, dialogue |
| `npc.spawn_synced` | name, x, y, z, dialogue, model=capsule |


## HUD（20 个）

| 函数 | 参数 |
|---|---|
| `hud.bar` | name, x=0.5, y=0.2, value=0, label |
| `hud.bar_set` | name, value, label |
| `hud.bind_condition` | expr, name |
| `hud.bind_event` | name, show, hide |
| `hud.bind_key` | key, name |
| `hud.hide` | name |
| `hud.image` | name, x, y, w=200, h=100, path |
| `hud.input` | name, x, y, w=220, h=28, placeholder=输入..., password |
| `hud.input_focus` | name |
| `hud.input_get` | name |
| `hud.input_set` | name, value |
| `hud.marker` | name, x, y, z, text |
| `hud.show` | name |
| `hud.text` | name, text, x, y, size |
| `hud.text_set` | name, text |
| `hud.toast` | text, seconds=4 |
| `hud.toggle` | name |
| `hud.window` | name, title, show |
| `hud.window_add_button` | name, text |
| `hud.window_show` | name, show |


## 随机与投票（9 个）

| 函数 | 参数 |
|---|---|
| `random.coin` |  |
| `random.dice` | sides=6 |
| `random.pick_player` |  |
| `random.pick_players` | count, exclude |
| `random.pool_add` | pool, name, weight=1, times=-1 |
| `random.pool_draw` | pool |
| `vote.cast` | id, uid |
| `vote.start` | id, title, duration=30, max=1 |
| `vote.stop` | id |


## 手机（3 个）

| 函数 | 参数 |
|---|---|
| `phone.chat` | name |
| `phone.send` | user, text |
| `phone.v2_present` |  |


## 同步字段（4 个）

| 函数 | 参数 |
|---|---|
| `sync.get` | name |
| `sync.register` | kind, name, value, hz=5 |
| `sync.set` | name, value |
| `sync.unregister` | name |


---
合计：310 个函数。