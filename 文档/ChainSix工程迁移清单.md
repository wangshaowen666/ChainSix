# ChainSix 工程迁移清单

> 目标：从 `wgame`（团结 1.8.1）迁移框架与基建到新工程 **ChainSix**（团结 1.10.3，git 仓库 `chainsix`），确立微信/抖音/TapTap 三平台通用的小游戏网络方案。
> 姊妹文档：`开发记录/六连珠对战玩法开发任务清单.md`（玩法任务，本清单完成后的开发按其推进）、`开发记录/客户端项目架构梳理.md`（框架现状）。

## 一、工程初始化

- [ ] 团结 1.10.3 新建工程 `ChainSix`；git 仓库 `chainsix`，补 `.gitignore`（Library/Temp/Logs/UserSettings/Build/obj）
- [ ] HybridCLR 安装并选 **1.10.3** 生成 il2cpp 工具链（**版本必须匹配**，唯一硬兼容点）
- [ ] 插件：Addressables、Input System 1.14+、Google.Protobuf（IL2CPP AOT 兼容 + link.xml）、DOTween、UnityWebSocket（或同类的 WS 桥接插件）；**LiteNetLib 不装**（见第五节）
- [ ] asmdef 三程序集结构照搬：Framework（AOT）/ Game（热更）/ Launch（AOT）

## 二、整体迁移（Framework / Launch，AOT 主包）

- [ ] `Assets/Scripts/Framework` 全量：Base/Module/Util/Profiler/Editor，asmdef 原样
- [ ] `Assets/Scripts/Launch` 全量：LaunchEntry、LaunchConfig、热更加载流程（ProcedureLoadDll/ResCheckAA/VersionCheck）、AOT 补充元数据流程
- [ ] `Assets/Res/Dll/` AOT 元数据补充 dll

## 三、Game 层选择性迁移（热更程序集）

| 模块 | 迁移 | 说明 |
| --- | --- | --- |
| Game/Base | GameMgr、GameLaunch、GameConfig、GameEnum、PlayerDataMgr、FrameAnim | AccountMgr 迁移但登录体系改为平台抽象（见五、七） |
| Game/Base | 不迁：TTTT、LoginTest | 测试代码淘汰，生产入口归位 |
| Game/Battle | FixMath（Fix/XRng）、`View/`（EntityPool/ViewSync/ViewMats）、LocalDriver | ChainSix 地基直接用 |
| Game/Battle | BattleView/BattleMgr 骨架 | `BattleMode` 只留 ChainSix；**TD/、VampireSurvivor/ 全目录不迁**（留旧仓库当范式参考） |
| Game/Net | FrameSyncMgr、RoomMgr、NetMsgHandler、HttpMsgHandler、ApiRegistry | 协议层全复用；传输层改造见五 |
| Game/Net/Proto | 不迁旧 proto，从零定义 | 新项目协议干净开始，只留 proto 工具链 |
| Game/UI | UIPanelBase/UIMgr/UIGroup + MainPanel/LoadingPanel/SettingPanel 骨架 | BattlePanel 只迁 HUD 占位形态；Joystick Pack/GameJoystick 不迁（点列操作无摇杆） |
| Game/Procedure | Preload/Main/ChangeScene/Battle 全量 | ProcedureBattle 清掉 TD/VS 分支 |
| Game/DataTable | DataTableMgr + Gen 框架 | 表数据重建 |
| Game/Editor | ToolBox 全量（导表/导 Proto/构建） | 工具链原样 |

## 四、资产与配置

- [ ] `Assets/link.xml`（preserve PhysicsModule 等，按 1.10.3 重新核对）
- [ ] Addressables 分组结构照搬：Remote_Scene/Remote_BattlePanel/Remote_Entity/Remote_DataTable/Remote_GameDll/Remote_MetaDll/Remote_Common
- [ ] 场景：Launch.unity（唯一 Build Settings 场景）+ Main/Battle 模板重建；GameConfig 场景常量 + `GetSceneType` 登记收口为 ChainSix 最小集
- [ ] GameEvent 枚举清掉 TD/VS 专属事件

## 五、必须同步做的改造（迁移 ≠ 原样照搬）

### 5-1 NetMgr 传输抽象（最高优先，先于一切联机开发）

**背景**：微信/抖音/TapTap 三平台小游戏均为 WebGL/WASM + JS 沙箱，`System.Net.Sockets` 不存在，LiteNetLib（基于原生 UDP）**在任何小游戏平台都不可用**。抖音官方兼容性文档原文：UDP 不支持。

**改造**：

- [ ] Framework 的 NetMgr 抽出 `ITransport` 接口（Connect/Send/OnData/OnClose），NetMgr 下沉为纯字节泵
- [ ] 实现 `WebSocketTransport`（wss，底层 UnityWebSocket 类插件桥接平台 WebSocket API）
- [ ] 可选 `LoopbackTransport`（编辑器单机调试）
- [ ] FrameSyncMgr/RoomMgr/protobuf 协议层 **零改动**（操作上行本就是 LiteNetLib ReliableOrdered ≈ TCP 语义，换 WS 行为等价；BufferFrames=2 防抖照用；应用层补 ping/pong 替代 LiteNetLib 内建心跳）
- [ ] 服务器：GameServer 从 LiteNetLib 换 ASP.NET Core Kestrel + WebSocket 中间件（LoginServer 技术栈现成）；NetPeer 概念换 WS Session；消息分发/房间/50ms 攒批广播/GameOver 流程原样保留；一个 wss 端点服务三平台

### 5-2 平台抽象层 IPlatformAdapter（登录/广告/分享/存储）

三平台差异收口：

| 能力 | 微信 | 抖音 | TapTap |
| --- | --- | --- | --- |
| 登录 | wx.login code2session | tt.login | TapTap auth |
| WebSocket | wx.connectSocket | tt.connectSocket | 平台同类 API |
| 广告 | wx.createRewardedVideoAd | TT SDK 激励视频 | TapTap 广告 |
| 桥接插件 | WX SDK | TT SDK | TapTap 转换 SDK |

- [ ] 定义 `IPlatformAdapter`（登录/广告/分享/存储/WS 桥），按平台各实现一份，运行时分派
- [ ] 上线前在对应开发者后台配置 **socket 合法域名（wss，需已备案域名 + 证书）**；开发期 `ws://localhost` + 关闭域名校验

## 六、DataTables 工具链

- [ ] `DataTables/` 迁 `luban.conf`、`__tables__.xlsx` 框架、`gen.sh`、`gen_proto.sh`（client/server/all 三 target 配置不动）
- [ ] 不迁任何旧表内容（#UIPanel 只迁 Main/Loading/Setting 三行起步，#Entity/#Effect 空表起步）；新建 `#ChainSix*.xlsx` 调参表
- [ ] 服务器 `Server/wgame-server` 整仓复制：删 TD 结算公式/坐标校验，proto 重写，JWT/房间/帧同步攒批骨架直接用

## 七、风险与待核实项

| # | 风险 | 处理 |
| --- | --- | --- |
| 1 | **抖音热更政策**：抖音 WebGL 兼容文档（2023）写明"小游戏环境不允许热更方案（xlua/ilruntime/puerts 等），运营策略不允许动态变更代码"，HybridCLR 是否放行未确认 | **立项前找抖音运营/对接群核实现行政策**；不放行则抖音版需评估整包更新或方案调整 |
| 2 | 抖音包体要求：压缩后 <30M（优秀 6M），wasm 需分包 | 团结 1.8+ 支持抖音 Metal 小游戏打包；用 wasm 分包工具 + Addressables 按需加载 |
| 3 | HybridCLR 与 1.10.3 兼容 | installer 选对应团结版本，初始化即验 |
| 4 | WebSocket 插件在 1.10.3 微信真机的兼容性 | 阶段 4-1 前置验证：转换一个最小 WS demo 真机连通即消掉最大平台风险 |

## 八、迁移顺序与验收

1. 空工程 + git init + HybridCLR（一）→ **验收：编辑器打开无报错**
2. Framework/Launch 迁移（二）→ **验收：能起空场景**
3. Game 白名单迁移 + 清理（三、四、五-2）→ 临时入口 → **验收：编辑器能进主界面**
4. DataTables 工具链跑通（六）→ **验收：导表 → ToolBox 补全 → Bin 产物链路正常**
5. ITransport 抽象 + 服务器 WS 端点 + proto 重写（五-1、六）→ **验收：编辑器双开 WS 帧同步假人对局**
6. 真机首次出包 → **验收：微信转换管线在 1.10.3 下跑通一次**
7. 完成后按 `开发记录/六连珠对战玩法开发任务清单.md` 阶段 1 开工

> 备注：六连珠任务清单当前平台写的是微信；三平台发布时按本清单 5-2 的 IPlatformAdapter 落地，任务清单阶段 4 可同步扩展，由后续拍板。
