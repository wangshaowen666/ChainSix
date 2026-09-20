# 团队C#/Unity编码命名规范

---

## 文档信息

|项|说明|
|---|---|
|版本|1.0|
|适用范围|本团队所有 C#/.NET、Unity 项目|
|生效日期|2026-04-03|
|修订记录|初始版本|


---

## 核心原则

1. **语义优先**：用完整的英文单词描述变量 / 方法的作用，避免生僻缩写（通用缩写除外，如 HP、UI、ID、DTO）

2. **区分作用域**：通过前缀、大小写严格区分不同作用域的元素，从命名就能直接看出它的类型和访问权限

3. **避免旧规范**：彻底淘汰匈牙利命名法（如`iHp`、`strName`这类带类型前缀的旧规范）

4. **IDE 友好**：符合主流 IDE 的默认规则，保证代码分析、重构、自动补全的兼容性

---

## 类型命名

|元素|命名规则|示例|
|---|---|---|
|类 / 结构体|PascalCase（大驼峰：首字母大写，后续单词首字母大写）|`PlayerController`、`DamageData`|
|接口|`I`前缀 + PascalCase|`IUpdateable`、`IDamageable`|
|枚举类型|PascalCase|`PlayerState`、`AttackType`|
|泛型参数|`T`前缀，或完整语义名|`TEntity`、`TPlayer`|
---

## 方法、属性与事件

|元素|命名规则|示例|
|---|---|---|
|自定义方法|PascalCase|`TakeDamage`、`MoveToPosition`|
|属性|PascalCase|`Hp`、`MaxHp`、`IsAlive`|
|事件|PascalCase，用`On`前缀表示触发时机|`OnHpChanged`、`OnDeath`|
|Unity 生命周期方法|遵循官方命名|`Awake`、`Start`、`Update`|
|扩展方法|PascalCase|`player.GetDistanceTo(target)`|
---

## 字段命名

这是区分度最高的部分，彻底解决私有字段、参数、属性的命名冲突：

|字段类型|命名规则|示例|说明|
|---|---|---|---|
|私有实例字段|`_`前缀 + camelCase（小驼峰：首字母小写，后续单词首字母大写）|`_hp`、`_maxHp`、`_moveSpeed`|核心区分规则：下划线前缀用来和参数、局部变量、属性做区分，从命名就能直接看出这是私有实例字段|
|私有静态字段|`s_`前缀 + camelCase|`s_instance`、`s_globalConfig`|`s`代表 static，用来区分实例私有字段，常用于单例、全局静态变量|
|私有线程静态字段|`t_`前缀 + camelCase|`t_localData`|极少用，`t`代表 thread，用于线程静态变量|
|公有常量 / 只读字段|PascalCase|`DefaultMaxHp`、`MoveSpeed`|常量和只读字段属于全局不变量，和属性规则一致|
|Unity 序列化私有字段|`_`前缀 + camelCase + `[SerializeField]`|`[SerializeField] private int _hp;`|完美解决 Inspector 编辑的冲突：不需要把字段设为公有，就能在 Inspector 面板编辑，同时私有字段的`_`前缀和属性的 PascalCase 完全不冲突|
> 注意：**尽量避免使用公有可变字段**，除非是非常简单的 DTO 数据类，正常的业务类都应该用「私有字段 + 公有属性」的组合，既保证封装性，又避免命名冲突。
> 
> 

---

## 参数与局部变量

|元素|命名规则|示例|说明|
|---|---|---|---|
|方法参数|camelCase|`damage`、`targetPosition`|小驼峰，和私有字段的`_`前缀区分，一眼就能看出这是入参|
|局部变量|camelCase|`tempHp`、`moveDelta`|小驼峰，和私有字段的`_`前缀区分，不会混淆|
---

## 枚举命名

|元素|命名规则|示例|说明|
|---|---|---|---|
|枚举成员|PascalCase|`Idle`、`Move`、`Attack`|不要加类型前缀（如旧规范的`PS_Idle`），C# 中已经不需要，通过枚举类型就能区分，比如`PlayerState.Idle`|
---

## 命名空间

|元素|命名规则|示例|说明|
|---|---|---|---|
|命名空间|PascalCase|`Game.Player`、`Game.UI`|按项目模块划分，和类名规则一致|
---

## 管理类、工具类

|元素|命名规则|示例|说明|
|---|---|---|---|
|有状态、需清理的管理类|XXXMgr|`UIMgr`、`BattleMgr`|普通类、统一在GameManager中实例管理|
|无状态的管理类|XXXCtr|`ProcedureCtr`|静态类、专注提供功能接口，切场景、重启等不需要清理操作|
|独立的功能|按功能命名|Fsm|普通类、提供静态的Create方法供其他模块使用|
|工具类|XXXUtil|FileUtil|普通类、提供静态的Create方法供其他模块使用|

## 完整代码示例

```csharp

// 接口：I前缀+PascalCase
public interface IUpdateable
{
    void Update(float deltaTime);
}

// 枚举：PascalCase
public enum PlayerState
{
    Idle,
    Move,
    Attack,
    Dead
}

// 类：PascalCase
public class Player : MonoBehaviour, IUpdateable
{
    // 常量：PascalCase
    public const int DefaultMaxHp = 100;
    
    // 私有静态字段：s_前缀（单例）
    private static Player s_instance;
    
    // 公有只读字段：PascalCase
    public readonly int MoveSpeed = 5;
    
    // 私有序列化字段：_前缀，可在Inspector编辑
    [SerializeField] private int _maxHp;
    [SerializeField] private float _attackRange;
    
    // 私有实例字段：_前缀
    private int _hp;
    private PlayerState _currentState;
    private Vector3 _moveVelocity;
    
    // 属性：PascalCase，和私有字段完全不重名
    public int Hp => _hp;
    public int MaxHp => _maxHp;
    public PlayerState CurrentState => _currentState;
    public bool IsAlive => _hp > 0;
    
    // 事件：PascalCase
    public event Action OnDeath;
    
    // 单例属性
    public static Player Instance => s_instance;

    // Unity生命周期方法
    private void Awake()
    {
        s_instance = this;
        _hp = _maxHp;
    }
    
    // 接口方法：PascalCase
    public void Update(float deltaTime)
    {
        if (!IsAlive) return;
        
        // 局部变量：camelCase，和私有字段的_前缀区分
        var moveInput = Input.GetAxisRaw("Horizontal");
        HandleMove(moveInput, deltaTime);
    }
    
    // 自定义方法：PascalCase
    // 参数：camelCase，和私有字段的_前缀区分
    public void TakeDamage(int damage)
    {
        // 命名清晰无混淆：_hp是私有字段，damage是入参
        _hp = Mathf.Max(0, _hp - damage);
        
        if (_hp == 0)
        {
            _currentState = PlayerState.Dead;
            OnDeath?.Invoke();
        }
    }
    
    private void HandleMove(float input, float deltaTime)
    {
        if (Mathf.Abs(input) > 0.1f)
        {
            _currentState = PlayerState.Move;
            _moveVelocity = new Vector3(input * MoveSpeed, 0, 0);
            transform.Translate(_moveVelocity * deltaTime);
        }
        else
        {
            _currentState = PlayerState.Idle;
        }
    }
}
```

---

## 为什么选择这个规范？

1. **行业通用**：这是微软.NET Core 源码、Unity 官方包、绝大多数 Unity/C# 开源项目都在使用的规则，团队协作时不需要额外的学习成本。

2. **彻底解决冲突**：通过`_`前缀区分私有字段，PascalCase 区分公有成员，从根本上避免了公有字段与属性的命名冲突，同时代码可读性极高。

3. **IDE 原生支持**：这是 Rider、Visual Studio 等主流 IDE 的默认命名规则，代码分析、重构、自动补全都能完美支持，不会有命名警告。

4. **兼容 Unity 工作流**：通过`[SerializeField]`特性，既保留了 Inspector 面板的编辑能力，又不需要破坏封装性，完美适配 Unity 的开发流程。
> （注：文档部分内容可能由 AI 生成）