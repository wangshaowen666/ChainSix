# WGame

## 介绍
WGame 是一个基于 Unity 引擎开发的游戏项目，结合了 XLua 热更新框架，实现了游戏逻辑的热更新功能。该项目适用于移动端游戏开发，支持 Lua 脚本调用 C# 代码和 C# 代码调用 Lua 脚本，具备良好的扩展性和维护性。

## 软件架构
项目主要采用 Unity + XLua 的架构模式：
- Unity 作为游戏引擎，负责游戏画面渲染、物理系统、资源管理等核心功能
- XLua 作为热更新框架，实现了 Lua 对 C# 的调用和 C# 对 Lua 的调用
- 项目采用了组件化设计，包含游戏逻辑组件、UI 系统、资源加载系统等模块

## 安装教程
1. 安装 Unity 引擎（建议 2020.3 或更高版本）
2. 下载并导入 XLua 插件
3. 克隆本仓库到本地
4. 打开 Unity 项目，等待资源导入完成
5. 连接真机或设置模拟器
6. 构建并运行项目

## 使用说明
1. 在 Unity 编辑器中，可以通过 LuaBehaviour 组件挂载 Lua 脚本
2. 使用 LuaEnv 类加载和执行 Lua 脚本
3. 通过 XLua 提供的特性（如 [CSharpCallLua]、[LuaCallCSharp]）实现 C# 与 Lua 的交互
4. 利用热更新功能，可以在不重新发布应用的情况下更新游戏逻辑

## 特色功能
1. **热更新**：通过 XLua 实现游戏逻辑的热更新功能
2. **Lua 与 C# 交互**：支持 Lua 调用 C# 代码和 C# 调用 Lua 脚本
3. **性能优化**：包含避免 GC 的优化示例代码
4. **异步加载**：提供了异步加载 Lua 脚本的实现
5. **签名加载器**：支持加载经过签名的 Lua 脚本，确保脚本安全性

## 开发规范
1. 使用 [CSharpCallLua] 特性标记需要被 Lua 调用的 C# 类型
2. 使用 [LuaCallCSharp] 特性标记需要调用 Lua 的 C# 类型
3. 热更新类使用 [Hotfix] 特性标记
4. 使用 XLua 提供的 API 进行 Lua 与 C# 的交互
5. 复杂数据类型需要注册到 Lua 环境中

## 项目结构
- **Assets/Plugins/XLua**：XLua 热更新框架核心代码和插件
- **Assets/Scripts**：C# 核心游戏逻辑代码
- **Assets/Res/Lua**：存放 Lua 脚本资源
- **Assets/Examples**：各种功能示例场景和代码

## 注意事项
1. 在真机运行前，请确保已经生成必要的 Wrap 文件
2. 热更新功能需要在 Editor 模式下先进行注入
3. 不同平台的 XLua 插件已经包含在项目中，无需额外配置
4. 使用热更新时需要注意平台相关的兼容性问题
5. 热更新代码需要进行充分测试，避免运行时错误

## 参与贡献
1. Fork 本仓库
2. 新建 Feat_xxx 分支
3. 提交代码
4. 新建 Pull Request

## 相关文档
- [XLua 官方文档](https://github.com/Tencent/xLua)
- [Unity 官方文档](https://docs.unity3d.com)
- [Lua 语言参考手册](https://www.lua.org/manual/5.3/)

## 特技
1. 使用 Readme_XXX.md 来支持不同的语言
2. 通过 Gitee 平台进行代码管理和协作开发
3. 利用 Gitee 提供的丰富开源项目资源
4. 参与 GVP（Gitee 最有价值开源项目）计划
5. 关注 Gitee 封面人物栏目，展示优秀开源项目
6. 使用 Gitee 提供的手册进行开发和问题排查
