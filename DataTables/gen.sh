#!/bin/bash

#这里不设置，unity内通过工具执行shell会报错 dotnet找不到
export PATH="$PATH:/usr/local/share/dotnet"

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
WORKSPACE=$(dirname "$SCRIPT_DIR")

CONF_ROOT="$WORKSPACE/DataTables"

LUBAN_DLL="$WORKSPACE/Tools/Luban/Luban.dll"

#echo "脚本目录: $SCRIPT_DIR"
#echo "工作空间: $WORKSPACE"
#echo "配置根目录: $CONF_ROOT"

dotnet $LUBAN_DLL \
    -t client \
    -c cs-bin \
    -d bin \
    -d json \
    --conf $CONF_ROOT/luban.conf \
    -x bin.outputDataDir=$WORKSPACE/Assets/Res/LubanData/Bin \
    -x json.outputDataDir=$WORKSPACE/Assets/Res/LubanData/Json \
    -x outputCodeDir=$WORKSPACE/Assets/Scripts/Game/DataTable/Gen \