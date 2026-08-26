# SFM Online - 联机客户端（开源版）

SecretFlasherManaka 的联机模组：BepInEx 6 (IL2CPP) 插件，包含联机核心（OnlineCore）与模组开发前置框架（SFMOnline.Ext，314+ 个 API）。

## 特性

- **联机核心**：直连（局域网/房主）与中继服务器（relay）双模式
- **前置框架 Ext**：314 个函数——积分/骨骼/区域/事件/任务/NPC/HUD/远程玩法控制/同步字段等
- **远程玩法控制**：`remote.*` 27 个函数，可对指定玩家/全员执行动作、振动、脱衣、高潮、传送等
- **玩家查询**：按名字找 uid、全员信息列表
- **模组开发**：写一个 DLL 放到 `BepInEx/plugins/` 即可扩展玩法
- **服务器插件**：Python 插件（`plugins/*.py`），客户端模组 ↔ 服务器插件可联动（本仓库不含服务器实现）

## 目录

```
SFMOnline.Client/   客户端插件源码（可编译）
docs/               开发文档（模组制作 + API 参考 + 插件开发 + 玩法设计）
examples/           示例模组工程（可直接编译安装）
server-plugin/      服务器插件开发模板与文档
```

## 构建

需要：.NET SDK 8 + 游戏目录（含 BepInEx interop DLL）。

```
$env:SFM_GAME_DIR = "D:\你的游戏目录"
.\SFMOnline.Client\build.ps1
# 产物: bin\SFMOnline.dll → 复制到 BepInEx\plugins\
```

## 安装

1. 安装 BepInEx 6（IL2CPP）
2. 把 `SFMOnline.dll` 复制到 `BepInEx/plugins/`
3. 启动游戏，F10 打开联机菜单

## 文档

- [客户端模组开发指南](docs/客户端模组开发指南.md)
- [API 完整参考](docs/API参考手册.md)
- [服务器插件开发指南](docs/服务器插件开发指南.md)
- [玩法设计模式](docs/玩法设计模式.md)
- [示例模组详解](docs/示例模组详解.md)

## 示例模组

- [ExampleQuiz 答题游戏](examples/ExampleQuiz/)（客户端 + 服务器插件完整联动）
- [ExampleHideSeek 捉迷藏](examples/ExampleHideSeek/)
- [ExampleTask 任务剧情](examples/ExampleTask/)

## License

MIT
