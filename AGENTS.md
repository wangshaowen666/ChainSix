# 项目规范

1. 日志输出统一使用框架的 `Log` 类（`Log.Info`、`Log.Error`、`Log.Warning` 等），禁止使用 `UnityEngine.Debug.Log`、`Debug.LogError`、`Debug.LogWarning` 等 Unity 自带日志方法。
2. 开发任务的文档任务完成时补充内容尽量简洁、不需要写什么时候完成、如何完成的详细内容。
3. AI 对同一文件的多处修改必须**串行**执行：一次只发一个编辑操作，等其完成落盘后再做下一处；禁止在同一批次并行提交对同一文件的多个编辑——并行编辑各自基于旧快照整体写回，会互相覆盖，导致改动丢失或文件内容损坏。不同文件之间的编辑可以并行。

# 配置表规范

1. 读表禁止安装任何临时工具/库：表内容直接读 `Assets/Res/LubanData/Json/*.json`，字段结构直接读 `Assets/Scripts/Game/DataTable/Gen/*.cs`。
2. 编辑 xlsx 使用本机已永久安装的 python3 + openpyxl（`/usr/bin/python3`，勿重复 pip install）。
3. 改完 xlsx 后执行 `bash DataTables/gen.sh` 导表；新增表需在 Unity ToolBox 点「导表」以自动补全 DataTableMgr 属性。
4. 完整流程见 `文档/开发记录/配置表Luban读写流程.md`。
