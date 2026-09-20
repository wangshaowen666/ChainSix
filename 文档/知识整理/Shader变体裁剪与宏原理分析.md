# Shader变体裁剪与multi_compile、shader_feature宏原理分析

针对你在Unity URP项目（团结引擎1.8.1）中遇到的"带宏的Shader热更后被裁切""理论变体数量爆炸""Instancing Stripping 设置"等问题，整理一份关于 Shader 变体（Variant）机制、宏声明、变体裁剪（Stripping）以及 AssetBundle 热更链路的完整说明。

---

## 一、什么是 Shader 变体（Variant）

一个 Shader 源文件在编译后，根据**关键字（Keyword）的组合**会产生多份"编译产物"，每一份叫一个**变体（Variant）**。

```
#pragma multi_compile A B
#pragma multi_compile_instancing
```

上面的声明会让编译器生成：

| 组合 | 说明 |
| --- | --- |
| （空，无关键字） | 基础变体 |
| A | 启用A |
| B | 启用B |
| INSTANCING_ON | 启用实例化 |
| A + INSTANCING_ON | 两者同时启用 |
| ... | ... |

N 个关键字理论上有 2^N 种组合，这就是"理论变体数"（我们统计工具里那 32 万亿的来源）。但**理论数 ≠ 实际编译数**，实际编译多少由**裁剪纸规则**决定。

运行时通过 `material.EnableKeyword("A")` / `Shader.EnableKeyword` 切换生效的关键字，对应的变体必须存在于已编译的集合中，否则表现异常或丢失。

---

## 二、multi_compile 与 shader_feature 的核心区别

| 维度 | `#pragma multi_compile` | `#pragma shader_feature` |
| --- | --- | --- |
| 变体保留规则 | **无条件编译全部组合** | 只保留**打包内容中有材质实际启用该关键字**的组合，其余全裁 |
| 用途 | 固定开关、多平台差异、instancing、全局开关等（运行时切换） | 材质属性开关（烘焙时/打包前确定的静态状态） |
| 包体/编译开销 | 大（所有组合都进包） | 小（用多少编多少） |
| 热更/动态创建的坑 | 只要 Stripping 设置放行就安全 | **运行时动态 `EnableKeyword` 或动态创建材质时极易被裁** |
| 常用变体 | `multi_compile_instancing`、`multi_compile_fog`、`multi_compile _ LIGHTMAP_ON` | `shader_feature _ FLASH`、`shader_feature _MAIN_LIGHT_SHADOWS` |

### 关键点：什么决定 shader_feature 变体"被裁"？

打包时 Unity 遍历**本次构建包含的场景、材质资产、Prefab 引用的材质**，收集每个材质实际启用的关键字。只有被材质用到的 `shader_feature` 组合才会编译进包。

因此下面两类情况会让 `shader_feature` 变体被裁掉：

1. **材质不在本次构建中**（例如 Shader 单独打 AB，材质在另一个 AB 或另一个包版本里）
2. **没有材质资产**——材质是运行时 `new Material(shader)` + `EnableKeyword` 动态创建的（打包时根本不存在，无法被分析到）

> 注意区分：`[Toggle] _Loop` 只是材质面板的复选框，写入 float 属性 0/1。**只有搭配了 `#pragma shader_feature` 才产生变体**；若 Shader 里用 `if (_Loop > 0.5)` 普通分支判断，则不产生任何变体，也不会被裁。

---

## 三、Graphics Settings 中的变体裁剪设置

`Project Settings > Graphics` 中：

### 1. Shader Stripping（全局）
- 默认会剔除未使用的 **lightmap / fog / HDR 等引擎内置变体**
- 具体项可在 Graphics Settings 面板中逐项控制

### 2. Instancing Stripping
这是"实例化变体"（`INSTANCING_ON` / `PROCEDURAL_INSTANCING_ON`）的裁剪规则，属于 `multi_compile` 性质，但**受此设置约束**：

| 设置 | 行为 |
| --- | --- |
| Strip Unused | 工程中没有任何"启用了 instancing 且引用该 Shader 的材质"时，裁掉 instancing 变体 |
| Strip Unused Variants | 按实际使用情况精简（更激进） |
| Keep All | **保留全部 instancing 变体，不裁剪**（你当前采用的方案） |

- 设置保存在 `ProjectSettings.asset`（GraphicsSettings 部分），**在打 AB 的那台机器/分支上生效**
- 只影响 instancing 类 `multi_compile` 变体，**对 `shader_feature` 变体无效**

### 3. Shader Loading 页签（Currently tracked）
- 显示本次编辑器会话中**实际被编译并加载**的变体数
- 是"实际使用情况"的观测窗口，不是打包数量，但能反映变体压力

---

## 四、与 AssetBundle 热更链路的关系（本项目场景）

变体裁剪发生在**打 AssetBundle 的那一刻**，读的是当时工程的 GraphicsSettings。真机运行时只认 AB 里已编译的变体。

### 本项目 `Game/FrameAnimGpuInst` 的实际情况

该 Shader 只有：

```hlsl
#pragma multi_compile_instancing   // 唯一产生变体的声明
```

- 没有 `shader_feature` 变体，因此不存在"shader_feature 被 AB 裁剪"的问题
- instancing 变体属于 `multi_compile`，唯一可能裁它的就是 **Instancing Stripping = Strip Unused**
- 你已改为 **Keep All**，理论上是"对症"的，打包后 `INSTANCING_ON` 变体会保留

### 动态创建材质为什么"目前安全，但要警惕"

- 该 Shader 变体是 `multi_compile`，不依赖材质存在，KeepAll 下必全进包 → **安全**
- 但若哪天 Stripping 改回 StripUnused，或换机器打包没提交 `ProjectSettings.asset`：工程里找不到任何 `enableInstancing = true` 的材质资产，`INSTANCING_ON` 会被判"没人用"直接裁掉，真机动态创建材质后 `RenderMeshInstanced` 表现错误 → **翻车**
- 以后若给 Shader 加 `shader_feature` 宏、运行时 `EnableKeyword` 切换：动态材质在打包时无法被分析到，这些变体**必被裁**

### 热更生效链路确认

```
改 GraphicsSettings(KeepAll)
  → 提交 ProjectSettings.asset（版本库）
  → 重新打热更 AB（包含该 Shader 的 bundle hash 必须变化）
  → 真机下载新 AB
  → INSTANCING_ON 变体存在
  → RenderMeshInstanced 正常渲染
```

---

## 五、解决方案与最佳实践

按推荐度排列：

### 方案1：ShaderVariantCollection（标准做法）
1. 在编辑器里把 Shader 的各个状态跑全（或手动创建 SVC，逐个 `Add` 变体）
2. `Graphics Settings > Shader Loading` 点 **Save to asset** 生成 SVC
3. 把 **SVC 与 Shader 打进同一个 AB**
4. AB 构建时 SVC 声明的变体全部保留，运行时还可通过 Preloaded Shaders 预热避免卡顿

> 对"运行时动态创建材质 + `EnableKeyword`"的项目，这是唯一可靠的兜底方案。

### 方案2：将必要的 `shader_feature` 改为 `multi_compile`
- 一劳永逸不会被裁，代价是包体增大、编译变慢
- 只适合关键字数量少的 Shader（避免变体爆炸）

### 方案3：材质与 Shader 同批构建
- 把启用该宏的材质与 Shader 放同一 bundle（至少同一次 `BuildAssetBundles`）
- 变体会保留，但后续单独热更 Shader 时容易翻车，稳定性不如方案1

### 保险措施（可选）
在工程里留一个隐藏目录（如 `Assets/Res/EditorOnly`），放一个 `enableInstancing = true` 且引用目标 Shader 的材质并随包构建。这样即使误改回 StripUnused，instancing 变体也不会被裁。

---

## 六、验证方法

1. **构建日志**：打 AB 时查 Editor.log 中该 Shader 的 `Compiling shader ... variants` 行，确认目标变体（如 `INSTANCING_ON`）是否被编译
2. **真机表现**：加载后用 `material.shaderKeywords` 检查宏是否生效，或直接观察 `Graphics.RenderMeshInstanced` 是否正常
3. **变体统计工具**：工具箱 > 统计工具 > 导出Shader变体统计数据，输出各 Shader 名称与理论变体数量（用于评估变体爆炸风险）

---

## 七、总结一句话

> `multi_compile` 是"全都要"（打包数量由 Stripping 设置决定，与材质无关）；`shader_feature` 是"用到的才要"（打包时按材质使用情况裁剪）。热更/动态材质场景下，`shader_feature` 变体极易被裁，用 ShaderVariantCollection 陪绑进同一 AB 兜底最稳妥；Instancing 变体受 Graphics Settings 的 Instancing Stripping 控制，KeepAll 可保平安。
