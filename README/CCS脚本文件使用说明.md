# CCS 脚本文件使用说明

## 环境前提

生成脚本只通过 JTAG 在 CPU Run 状态读取目标全局符号，不暂停 CPU，不使用目标端串口、Python 或 MATLAB。

- `.ccxml`、仿真器、器件和 CPU 核匹配，已连接并加载含调试符号的 `.out`。
- 变量是有固定地址的全局符号。建议关键量声明 `volatile`，并通过 retain/链接配置避免被优化删除。
- CSV 目录存在且 CCS 有写权限；多核设备须选中保存变量的 CPU 核。

## 先验证表达式

在 Expressions/表达式视图逐个添加完整表达式，如：

```text
gTelemetry.attitude.yaw
gControllers[0].positionRef
```

确认类型、值和变化正确。Expressions 无法读取时，GEL 也无法可靠读取。支持数组元素、多层结构体和有效指针成员；不自动展开整数组或整个结构体。

## 各 CCS 版本加载

- CCS 20.5.1：选中 CPU，打开 `View → GEL Files`，Load GEL；从 Scripts/Script Commands 的 `Data Logger` 调用。参考：<https://software-dl.ti.com/ccs/esd/documents/users_guide_ccs_20.5.1/index_debug.html>
- CCS 12.7.1：`Tools → GEL Files → Load GEL...`，再用 `Scripts → Data Logger`。Expressions 位于 `View → Expressions` 或 `View → Other... → Debug → Expressions`。
- CCS 8.3：Eclipse Debug 透视图中选中 CPU，`Tools → GEL Files` 加载，再从 Scripts 调用。不同补丁应先单变量、慢周期验证。
- CCS 5.5：操作与旧 Eclipse 版相同，但必须现场验证定时器、连接/暂停检测、文件输出及运行态读取；API 未定义时不要删除安全检查强行运行。

## 控制函数

- `DataLogger_Start`：覆盖旧 CSV、写表头并启动。
- `DataLogger_Stop`：取消定时器并停止。
- `DataLogger_Status`：打印运行状态、样本数、错误数。
- `DataLogger_ForceCleanup`：异常后关闭定时器和文件重定向。

优先从 Scripts 菜单调用，不要把 `DataLogger_Start()` 放入自动刷新的 Expressions。

## 类型与格式边界

生成器提供 `int8/uint8/int16/uint16/int32/uint32/float32/float64/bool/enum16/enum32`。浮点只允许十进制；整数、布尔值和枚举允许十进制或十六进制。`int32/enum32` 使用 GEL `long`，`uint32` 使用 `unsigned long`，避免赋值到 C28x 16 位 `int/unsigned int` 时截断。

TI 官方 GEL 只支持 `%d/%u/%x`（GEL `int`）、`%f/%e`（`double`）和 `%s`（主机常量字符串），没有长度或精度修饰符。因此界面已删除 int64、uint64、科学计数法、浮点精度列和“目标字符串”。C28x 的最小可寻址单元通常为 16 位，`int8/uint8` 是否存在及其实际布局必须以目标工程 typedef 和 Expressions 为准。

C28x `int/unsigned int` 通常为 16 位，`long/unsigned long` 为 32 位；`double` 在 COFF/EABI 下宽度不同。依据：

- <https://software-dl.ti.com/ccs/esd/documents/users_guide_12.7.0/gel/format.html>
- <https://software-dl.ti.com/ccs/esd/documents/users_guide/gel/param.html>
- <https://software-dl.ti.com/ccs/esd/documents/C2000_c28x_migration_from_coff_to_eabi.html>
- <https://software-dl.ti.com/C2000/docs/cla_software_dev_guide/faq.html>

## uint32 实机验证

令 `Uint32/uint32_t/unsigned long` 测试量依次取 `0`、`65535`、`65536`、`2147483647`、`2147483648`、`4294967295`，比较 Expressions 与 CSV 的十进制和十六进制结果。再验证 int32 的 `-2147483648/-32769/32768/2147483647`，以及采样计数跨过 32767。全部一致后，才能确认具体 CCS/目标组合不截断。软件测试只能检查生成模板，不能替代 JTAG 实机验证。

## 故障排查

- `identifier not found`：符号未加载、表达式错误、变量被优化、局部变量离开作用域或 CPU 核错误。
- CSV 只有表头：CPU Halt、连接中断、API 不兼容或运行态内存不可访问。
- 数据全零：先看 Expressions 是否非零；新版输出应类似 `GEL_TextOut("%f,",,,,,dl_v0);`。
- 类型/格式被拒绝：浮点不能选择十六进制；旧 64 位整数或目标字符串配置会被拒绝。
- CSV 含 `C28xx_CPU1:`：使用生成器的 CSV 转 XLSX 功能清理。

## 限制与注意事项

- 仅适合几 Hz～几十 Hz；严禁用于 kHz 级高速控制环。
- CPU 必须持续 Run，脚本不会暂停 CPU。
- 多变量是 JTAG 顺序读取，不是原子快照。
- GEL 没有通用 `try/catch/finally`，不能捕获所有非法地址、USB 或驱动故障。
- 指针必须非空并指向有效内存；修改 GEL 后须在 GEL Files 中 Reload。
- VOFA+桥接是 PC端文件监听和 TCP转发，不改变 GEL的纯 JTAG采集方式，也不会暂停目标 CPU。
- VOFA+中应选择 TCP Client 和 FireWater，连接生成器显示的监听地址与端口。
- FireWater不携带通道名称；使用生成器自动复制的列表，在 VOFA+中按同一顺序命名。
