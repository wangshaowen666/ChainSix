# C# 委托与闭包的内存分配分析

## 背景

在 Unity（HybridCLR + 热更）项目里，回调 API（如 HTTP 请求的 `onResp`）高频出现在业务层。不同写法（lambda / 方法组 / 字段缓存）的内存分配差异是性能优化的关键关注点。本文汇总 lambda、方法组、字段缓存三种写法的分配行为，并附带泛型推断规则的说明。

---

## 一、三种写法的内存分配对比

### 前提：什么是"分配"

回调被调用时是否产生 GC 压力，取决于**委托/闭包对象是否被重复创建**。分配对象 = 有 GC 成本；零分配 = 复用已创建对象。

### 1. 无捕获 lambda —— 全局零分配（最优）

```csharp
GameMgr.HttpMsg.Post(req, resp => { /* 不引用任何外部变量 */ });
```

lambda **没有捕获外部变量**时，编译器把它缓存为**静态委托字段**，每次调用复用同一个对象。

- 分配：**整个 AppDomain 全局 1 次**（所有实例共享）
- 每次调用：**零分配**
- 结论：完全不需要外部变量时，直接写 lambda，编译器自动优化，零成本。

### 2. 捕获变量的 lambda（闭包）—— 每次分配

```csharp
public void Login(string u, string p, Action<LoginResp> onDone)
{
    GameMgr.HttpMsg.Post(req, resp => { ... onDone?.Invoke(resp); }); // 捕获 onDone
}
```

lambda **捕获外部变量**（如方法参数 `onDone`）时，编译器生成一个**闭包类**（display class），其实例字段保存捕获的变量。**每次调用外层方法都 new 一个闭包对象**。

- 分配：**每次调用 1 个闭包对象**（委托通常被编译器缓存复用）
- 结论：**lambda 本身不是分配来源，闭包（捕获变量）才是**。

### 3. 外部实例方法（方法组）—— 每次分配

```csharp
public void Login(...)
{
    GameMgr.HttpMsg.Post(req, HandleLogin);  // 方法组
}
private void HandleLogin(LoginResp resp) { ... }
```

方法组转委托时，若目标是**实例方法**，委托绑定 `this`，编译器通常**每次调用都创建新委托**（无法跨调用缓存）。

- 分配：**每次调用 1 个委托**（若是静态方法 → 编译器缓存静态委托，零分配）
- **限制**：外部方法只能访问 `this` 的字段，**捕获不了外层方法的局部变量/参数**！

```csharp
public void Login(...) { GameMgr.HttpMsg.Post(req, HandleLogin); }
private void HandleLogin(LoginResp resp)
{
    // ❌ 无法访问 onDone（它是 Login 的局部参数，不是字段）
}
```

### 4. 字段缓存 —— 零分配（需要访问外部变量时的最优解）

```csharp
private Action<LoginResp> _handler;   // 委托只建一次

public void Login(string u, string p, Action<LoginResp> onDone)
{
    _onDone = onDone;                 // 外部变量转存字段（赋值零分配）
    GameMgr.HttpMsg.Post(req, _handler); // 复用委托，零分配
}
```

- 分配：委托**对象构造时 1 次**（每个实例 1 次）
- 每次调用：**零分配**（只是字段赋值 + 传引用）
- 结论：高频复用同一回调时最优，但代价是"同一时刻只能有一个不同的外部变量"（并发多回调会被覆盖）。

### 汇总表

| 写法 | 能否捕获局部变量/参数 | 分配时机 | 每次调用分配 |
|---|---|---|---|
| 无捕获 lambda | ❌ | 全局 1 次（静态缓存）| **零分配** ✅ |
| 捕获变量 lambda（闭包）| ✅ | 每次调用 | 1 个闭包对象 ⚠️ |
| 外部实例方法（方法组）| ❌（只能 this 字段）| 每次调用 | 1 个委托 ⚠️ |
| 字段缓存 | ✅（变量转存字段）| 构造时 1 次 | **零分配** ✅ |

### 一个易混淆的细节："一次分配"的作用域

- **无捕获 lambda** → 静态字段 → **AppDomain 全局 1 次**（所有实例共享）
- **字段缓存** → 实例字段 → **每个对象实例 1 次**（N 个实例 = N 次）

单例场景下两者等价；多实例场景下 lambda 更省（全局共享）。

---

## 二、泛型推断：为什么方法组推断不出类型

`HttpMsgHandler.Post` 的签名是 `Post<TResp>(IMessage req, Action<TResp> onResp)` -- 泛型 `TResp` 只出现在回调里。

```csharp
GameMgr.HttpMsg.Post(req, A);          // ❌ 方法组 → 报错"必须指定泛型"
GameMgr.HttpMsg.Post(req, onDone);     // ✅ 委托变量 → 自动推断
GameMgr.HttpMsg.Post<LoginResp>(req, resp => {});  // ✅ 显式指定
```

**原因**：C# 泛型类型推断只看传入参数。`req` 是 `IMessage` 不提供 `TResp` 信息；而**方法组（method group）不参与泛型推断**，编译器无法从方法组推导 `TResp`。委托变量（`Action<LoginResp>`）类型已知，能反推出 `TResp`。

| 传的东西 | 类型信息 | 能否推断 |
|---|---|---|
| 方法组 `A` | 编译器不给方法组做推断 | ❌ 报错 |
| 委托变量 `onDone`（`Action<LoginResp>`）| 类型已知 | ✅ |
| Lambda `resp => {}` | 由 `Action<TResp>` 参数约束 | ✅ |
| 显式 `<LoginResp>` | 明确指定 | ✅ |

**解决**：显式写泛型、用 lambda、或把方法组强转成委托。

```csharp
GameMgr.HttpMsg.Post<NetMsg.LoginResp>(req, A);                    // 1. 显式泛型
GameMgr.HttpMsg.Post(req, resp => A(resp));                        // 2. lambda 包装
GameMgr.HttpMsg.Post(req, (Action<NetMsg.LoginResp>)A);            // 3. 强转委托
```

---

## 三、实用建议

- **回调不需要外部变量** → 无捕获 lambda，编译器自动静态缓存，不用管
- **低频请求 + 每次回调变量不同**（如 HTTP 登录/注册）→ **lambda 最方便**，分配可忽略
- **高频复用同一回调**（帧同步、每帧执行）→ **字段缓存**，牺牲灵活性换零分配
- **方法组**是中间态：既不能捕获局部变量，又每次分配，除非为了代码复用否则不优先

**核心一句话**：lambda 的分配来自"闭包（捕获变量）"，方法组的分配来自"委托（绑定 this）"，字段缓存两者都省。真正决定分配的不是"lambda vs 方法"，而是"是否捕获外部变量 / 编译器能否缓存"。

---

## 附：与 Lua 的差异（一句话备忘）

Lua 没有 C# 的"无捕获 lambda 自动静态缓存"机制；函数定义在嵌套/循环里就每次分配，必须手动缓存到模块级变量或字段。手动缓存技巧两边通用。
