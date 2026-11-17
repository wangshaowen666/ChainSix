# 六连珠逻辑回归

在项目根目录运行（需要 .NET 8 与已还原的 Unity Luban 包）：

```sh
dotnet run --project Tests/SixLine/SixLine.Tests.csproj --configuration Release
```

覆盖规则表二进制读取、不同尺寸棋盘、普通/特殊消除攻击、连锁加成、垃圾等待与同帧互投、越线/溢出/平局、长局共享出子、逐帧哈希重放、双人输入及追帧/失焦/结束处理，同时验证连续接触、旋转重心、支撑圆弧、逐轮消除轨迹、稳定棋子 ID、结算输入窗口，并检查 Tick 和出子路径的托管分配。

`DriverEnvironment.cs` 只为独立测试提供可控时间与输入，不参与 Unity 编译。真机触控、性能和 2~4 分钟调参仍需在 Unity/设备中验收。
