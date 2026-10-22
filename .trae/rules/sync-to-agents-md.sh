#!/usr/bin/env bash
# 由 .trae/rules/*.md 生成项目根 AGENTS.md。
# DSH（DeepSeek Harness）每次会话会把项目根的 AGENTS.md 自动注入为工作区指令，
# 但它不会读 .trae/rules/，所以改完规则后需要重新执行本脚本同步。
# 用法：bash .trae/rules/sync-to-agents-md.sh
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../.." && pwd)"
out="$root/AGENTS.md"

{
  printf '# 项目规则（AGENTS.md）\n\n'
  printf '> 来源：`.trae/rules/`（Trae 侧规则）。本文件是 DSH 每次会话自动注入的副本，规则内容与优先级以 `.trae/rules/*.md` 为准。\n'
  printf '> 改完规则后执行 `bash .trae/rules/sync-to-agents-md.sh` 重新生成本文件。\n'
  printf '> 自 2026-09-28 起，本项目在 DeepSeek Harness（DSH）中继续开发；`.trae/rules/` 保留为规则源，DSH 每次会话按本文件注入。\n'
  printf '> memos MCP 已接入 DSH（`~/.dsh/profiles/web/cordis.patch.yml` 的 `mcp-memos` 条目，profile 热加载即时生效）：对应工具名为 `mcp__memos__search_memory`、`mcp__memos__add_message`、`mcp__memos__get_memory`、`mcp__memos__delete_memory`，下方 MemOS 章节的其余参数与规则不变。\n\n'
  printf -- '---\n\n'
  for f in "$here/项目规范.md" "$here/配置表规范.md" "$here/memos_memory.md"; do
    printf '<!-- 来源: .trae/rules/%s -->\n\n' "$(basename "$f")"
    cat "$f"
    printf '\n\n'
  done
} > "$out"

echo "wrote $out"
