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
