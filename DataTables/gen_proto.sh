#!/bin/bash
# ============================================================
# proto 代码生成脚本
# 作用：将公共 Proto/*.proto 生成 C# 代码到客户端热更层 Game/Net/Proto
# 服务器端代码由 GameServer.csproj 的 Grpc.Tools 在 build 时自动生成
#
# 用法：
#   ./scripts/gen_proto.sh           # 生成客户端代码
#   修改 Proto/*.proto 后运行一次即可
# ============================================================
set -e

# 项目根目录（脚本所在目录的上一级）
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

PROTO_DIR="$ROOT/Server/wgame-server/Proto"
CLIENT_OUT="$ROOT/Assets/Scripts/Game/Net/Proto"

# ---- 定位 protoc ----
# 优先用环境变量 PROTOC，否则在 NuGet 包里查找
if [ -n "$PROTOC" ]; then
    PROTOC_BIN="$PROTOC"
else
    # 自动探测当前平台的 protoc（Grpc.Tools 2.83.0）
    case "$(uname -s)" in
        Darwin)
            PROTOC_BIN="$HOME/.nuget/packages/grpc.tools/2.83.0/tools/macosx_x64/protoc"
            ;;
        Linux)
            PROTOC_BIN="$HOME/.nuget/packages/grpc.tools/2.83.0/tools/linux_x64/protoc"
            ;;
        *)
            echo "错误：不支持的系统，请手动设置 PROTOC 环境变量指向 protoc 可执行文件"
            exit 1
            ;;
    esac
fi

if [ ! -x "$PROTOC_BIN" ]; then
    echo "错误：找不到 protoc: $PROTOC_BIN"
    echo "请确认服务器已还原 Grpc.Tools 包，或用 PROTOC 环境变量指定路径"
    exit 1
fi

echo "使用 protoc: $PROTOC_BIN"

# ---- 生成客户端代码 ----
# 一次性编译所有 .proto（文件间有 import 依赖，必须一起编译）
mkdir -p "$CLIENT_OUT"
PROTOS=("$PROTO_DIR"/*.proto)
if [ "${#PROTOS[@]}" -eq 1 ] && [ ! -f "${PROTOS[0]}" ]; then
    echo "错误：$PROTO_DIR 下没有 .proto 文件"
    exit 1
fi

echo "待生成 proto 文件："
for proto in "${PROTOS[@]}"; do
    echo "  - $(basename "$proto")"
done

"$PROTOC_BIN" -I "$PROTO_DIR" --csharp_out="$CLIENT_OUT" "${PROTOS[@]}"

# ---- 同步共享常量（手写文件，非 protoc 生成）----
if [ -f "$PROTO_DIR/NetApi.cs" ]; then
    cp "$PROTO_DIR/NetApi.cs" "$CLIENT_OUT/NetApi.cs"
    echo "已同步共享常量: NetApi.cs"
fi

echo "完成！客户端代码已生成到 $CLIENT_OUT"
echo "注意：服务器端代码由 GameServer build 时自动生成，无需手动处理"

# 成功标记：ToolBox.ImportProto 通过它判断导 proto 成功（ShellUtil 不返回 exit code）
echo "[PROTO_OK]"
