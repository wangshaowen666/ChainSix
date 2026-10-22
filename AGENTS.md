# 项目规则（AGENTS.md）

> 来源：`.trae/rules/`（Trae 侧规则）。本文件是 DSH 每次会话自动注入的副本，规则内容与优先级以 `.trae/rules/*.md` 为准。
> 改完规则后执行 `bash .trae/rules/sync-to-agents-md.sh` 重新生成本文件。
> 自 2026-09-28 起，本项目在 DeepSeek Harness（DSH）中继续开发；`.trae/rules/` 保留为规则源，DSH 每次会话按本文件注入。
> memos MCP 已接入 DSH（`~/.dsh/profiles/web/cordis.patch.yml` 的 `mcp-memos` 条目，profile 热加载即时生效）：对应工具名为 `mcp__memos__search_memory`、`mcp__memos__add_message`、`mcp__memos__get_memory`、`mcp__memos__delete_memory`，下方 MemOS 章节的其余参数与规则不变。

---

<!-- 来源: .trae/rules/项目规范.md -->

# 项目规范

1. 日志输出统一使用框架的 `Log` 类（`Log.Info`、`Log.Error`、`Log.Warning` 等），禁止使用 `UnityEngine.Debug.Log`、`Debug.LogError`、`Debug.LogWarning` 等 Unity 自带日志方法。
2. 开发任务的文档任务完成时补充内容尽量简洁、不需要写什么时候完成、如何完成的详细内容。
3. AI 对同一文件的多处修改必须**串行**执行：一次只发一个编辑操作，等其完成落盘后再做下一处；禁止在同一批次并行提交对同一文件的多个编辑——并行编辑各自基于旧快照整体写回，会互相覆盖，导致改动丢失或文件内容损坏。不同文件之间的编辑可以并行。


<!-- 来源: .trae/rules/配置表规范.md -->

# 配置表规范

1. 读表禁止安装任何临时工具/库：表内容直接读 `Assets/Res/LubanData/Json/*.json`，字段结构直接读 `Assets/Scripts/Game/DataTable/Gen/*.cs`。
2. 编辑 xlsx 使用本机已永久安装的 python3 + openpyxl（`/usr/bin/python3`，勿重复 pip install）。
3. 改完 xlsx 后执行 `bash DataTables/gen.sh` 导表；新增表需在 Unity ToolBox 点「导表」以自动补全 DataTableMgr 属性。
4. 完整流程见 `文档/开发记录/配置表Luban读写流程.md`。


<!-- 来源: .trae/rules/memos_memory.md -->

# MemOS 记忆系统 - 使用规则 (Trae)

本规则让 Trae 中的 AI 通过 memos-api-mcp 自动存取长期记忆。**核心纪律：每轮对话 = 回答前先查记忆 → 结合记忆作答 → 回答后必写记忆**。

## 项目标识（所有调用必须带上）

- `project_id`: "chainsix"   // 顶层项目隔离字段
- `info.app_id`: "chainsix"  // 写入时放 info 里；后端会把 info 的键平铺到 memory 顶层
- `session_id`: 单次会话保持稳定；用 `md5(user_id + 会话第一条用户消息)` 生成

## 每轮对话的强制工作流

### 1. 回答前，必须调用 search_memory

必填入参：
- `query`：当前用户问题（可做精简摘要）
- `session_id`：本次会话的稳定 ID
- `top_k`: 3
- `search_memory_type`: "LongTermMemory"   // 只检索长期记忆
- `dedup`: "no"                             // 关 MMR 去重，避免本地搜索慢到 40s+
- `include_preference`: false
- `search_tool_memory`: false
- `include_skill_memory`: false
- `filter`: 必须限定到本项目，用 `and`/`or` 结构；info 字段写入后平铺到顶层，过滤时直接用字段名，不要写 `info.xxx`

推荐调用示例：
```json
{
  "query": "<当前用户问题摘要>",
  "session_id": "chainsix_<stableConvId>",
  "top_k": 3,
  "search_memory_type": "LongTermMemory",
  "dedup": "no",
  "include_preference": false,
  "search_tool_memory": false,
  "include_skill_memory": false,
  "filter": { "and": [ { "project_id": { "or": [ "chainsix", "unity-dev" ] } } ] }
}
```

规则：仅使用与当前问题真正相关的记忆；无关或噪声忽略，不要硬塞进答案。

### 2. 结合检索到的相关记忆回答用户

### 3. 回答完成后，必须调用 add_message 记录本轮对话

无论用户说什么都要调用，否则记忆库不会积累。必填/建议入参：
- `session_id`：与第 1 步同一个 ID
- `messages`：本轮的 user + assistant 两条消息（OpenAI Chat 格式；assistant 为最终回答）
- `project_id`: "chainsix"
- `async_mode`: "async"   // 异步写入，不阻塞对话
- `mode`: "fast"          // 自动记忆用 fast 模式
- `info`（自定义元数据，键会被平铺到顶层，供后续过滤）：
```json
{
  "app_id": "chainsix",
  "scope_key": "chainsix:project",
  "module": "<FrontEnd / Backend / DevOps>",
  "business_type": "<unity_client / web_app / script / build_tool>",
  "biz_id": "<类名或模块名>",
  "topic": "<一句话主题>",
  "keywords": ["<稳定关键词1>", "<稳定关键词2>"],
  "scene": "<coding / debug / daily_chat / qa>",
  "lang": "zh"
}
```

推荐调用示例：
```json
{
  "session_id": "chainsix_<stableConvId>",
  "project_id": "chainsix",
  "async_mode": "async",
  "mode": "fast",
  "messages": [
    { "role": "user", "content": "<用户本轮问题>" },
    { "role": "assistant", "content": "<助手最终回答>" }
  ],
  "info": {
    "app_id": "chainsix",
    "scope_key": "chainsix:project",
    "module": "<FrontEnd / Backend / DevOps>",
    "business_type": "unity_client",
    "biz_id": "<类名或模块名>",
    "topic": "<一句话主题>",
    "keywords": ["<稳定关键词1>", "<稳定关键词2>"],
    "scene": "coding",
    "lang": "zh"
  }
}
```

## 用户偏好记忆要点

- 用户是 **Unity 游戏开发者**，主要用 **C#**，在写微信小游戏项目
- 用户**不喜欢回答里有一堆 emoji**，回复保持简洁
- 用户希望记忆系统**随用随开关、不长期占内存**（用前调 start，不用 stop）

## 关键规则（每次调用前自查）

- 按上面的 search/add 示例调用；`session_id` 单次会话保持稳定
- 搜索默认只查 `LongTermMemory`，用 `dedup: "no"`，关闭 Preference/Tool/Skill 记忆分支
- `filter` 必须用 `and`/`or` 结构；不要传裸单字段，不要用 `info.<key>`
- 不传 `custom_tags`；稳定标签写 `info.keywords`，模块归类写 `info.module` / `info.biz_id`
- 回答前必须 search，回答后必须 add——这两步是记忆系统运转的关键，不可省略


