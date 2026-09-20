# 配置表（Luban）读写流程

## 目录结构

| 路径 | 说明 |
|---|---|
| `DataTables/Datas/#*.xlsx` | 数据源表（直接编辑） |
| `DataTables/Datas/__tables__.xlsx` | 表注册（表名 ↔ 数据文件、分组） |
| `DataTables/Datas/__beans__.xlsx` | 结构体（bean）定义 |
| `DataTables/Datas/__enums__.xlsx`、`__pnlenums__.xlsx` | 枚举定义 |
| `DataTables/Defines/builtin.xml` | 内置类型 vector2/3/4 |
| `DataTables/luban.conf` | Luban 主配置 |
| `DataTables/gen.sh`、`gen.bat` | 导表脚本 |
| `Assets/Scripts/Game/DataTable/Gen/` | 自动生成 C#（禁止手改） |
| `Assets/Res/LubanData/Bin/*.bytes` | 运行时二进制数据 |
| `Assets/Res/LubanData/Json/*.json` | 文本导出（供人/AI 阅读） |

## AI 读表：无需安装任何工具

1. 查表内容：直接读 `Assets/Res/LubanData/Json/tb<表名小写>.json`（如 `tbvsweapon.json`）
2. 查字段类型/注释：直接读 `Gen/D<表名>.cs`（readonly 字段 + XML 注释）
3. 查表 API：读 `Gen/Tb<表名>.cs`（`DataMap`、`DataList`、`Get(id)`、`GetOrDefault(id)`、索引器）
4. 查全部表清单：读 `Gen/Tables.cs`
5. 枚举取值对照：读 `Gen/` 下对应枚举类（如 `VSWeaponBehavior.cs`）
6. 禁止为读表临时安装任何工具或库（pip/npm 等）

注意：

- JSON 字段名为小驼峰；枚举列 Excel 源里写枚举名（如 `Projectile`），JSON 里是数字
- gen.sh 以 `client` 目标导出，JSON 仅含 client 组字段
- Json/Bin 是导表产物；若刚改过 xlsx 还未导表，以 xlsx 为准

## xlsx 格式（编辑时遵守）

每个 sheet 一张表，固定 4 类行，数据从第 4 行起：

```
第1行  ##var  id    name   behavior          ...
第2行  ##type int   string VSWeaponBehavior  ...
第3行  ##     武器id 名字   行为范式          ...
第4行+        1     魔杖   Projectile        ...
```

- 首列 `##var` / `##type` / `##` 是元数据标记行，写入新列时三行都要补齐
- 新增数据行从第 4 行起，前三个标记列留空

## AI 配表流程

1. 改数据/加行：编辑对应 `DataTables/Datas/#XXX.xlsx`
2. 加字段：在数据表 xlsx 追加一列（`##var`/`##type`/`##` 三行补齐）；若该类型是 bean，同步改 `__beans__.xlsx`
3. 加新表：`__tables__.xlsx` 注册 → 新建 `#XXX.xlsx`（补齐三行元数据）
4. 加枚举：改 `__enums__.xlsx`
5. 编辑 xlsx 用本机已永久安装的 python3 + openpyxl，勿重复安装
6. 重新导表：
   - 命令行：`bash DataTables/gen.sh`（Windows 用 gen.bat）
   - Unity：ToolBox → 「导表」按钮（同一脚本，且唯一会**自动补全 DataTableMgr 表属性**的途径）
7. 新增表后必须用 Unity 导表按钮跑一次（或手动给 DataTableMgr 加属性）
8. 校验：脚本无报错，`Gen/`、`Bin/`、`Json/` 三处同步更新，Unity 编译通过

## 运行时加载

- 单例访问：`GameMgr.DataTable`（`DataTableMgr`），启动时 `LoadTable()` 经 `CoreMgr.Res.LoadSync<TextAsset>("Bin/tb<xxx>.bytes")` 读取
- 用法示例：`GameMgr.DataTable.TbVSWeapon.Get(id)`
- `ReloadTable()` 支持热重载

## 当前表清单（11 张）

Effect / Entity / Plane / UIPanel / VSCharacter / VSEnemy / VSPassive / VSPickup / VSStage / VSWeapon / VSWeaponLevel

各表字段含义见 `Gen/D<表名>.cs` 内注释。
