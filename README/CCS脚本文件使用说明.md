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

生成器统一将固定输出路径生成为预处理宏，例如 `#define DL_CSV_PATH "C:\\data\\log.csv"`，末尾不加分号。该写法直接向文件输出函数传入字符串常量，绕过全局字符串变量声明。用户在 CCS 5.5 环境中已验证，此方式可以解决原 `string DL_CSV_PATH = "...";` 行的导入报错；没有原始错误信息时，不能据此断言所有 CCS 5.5 均不支持全局字符串。修改路径后需重新加载 GEL。

生成器中的“目标 CCS 版本”是全局配置。GEL 主体在各版本间尽量保持共同语法；输入桥接则必须区分环境：CCS 5.5/8.3/12.7.1 使用 Rhino/Java 的 Legacy DSS，CCS 20.x 使用 Node.js 的新 CCS Scripting，两类 JavaScript 不能互换。

## VOFA+ 写入目标变量

变量方向设为 `input` 或 `inout` 后，生成 GEL 时会同时生成 `_ccs_input_bridge.js`。先启动 GelGenerator 的 VOFA+ 桥接，再在对应 CCS 的 Scripting Console 加载此脚本。

- CCS 5.5/8.3/12.7.1：在 Debug 视图选中正确 CPU，确认 Scripting Console 的 `activeDS` 可读取目标表达式，然后加载脚本。脚本通过 `activeDS.expression.evaluate()` 写入，并用 `evaluateToString()` 回读。
- CCS 20.x：适配器依次复用`session`、`globalThis.__ggSession`或`activeDS`；都不存在时使用`ds`/`initScripting({inProcess:true})`列出当前核，并按照生成器中的CPU核匹配自动打开会话。通常不需要再次指定`.ccxml`。
- VOFA+波形和SET命令共用一个1347全双工TCP连接；CCS输入适配器独立使用本机1349。GelGenerator不会把ACK/ERR文本发回FireWater连接，写入结果在生成器状态区显示。
- 必须在 GelGenerator 桥接页手动打开“允许反向写入”；默认关闭。
- 命令为 `SET,配置名称,数值`，如 `SET,yaw_ref,12.5`。不得发送 CCS 表达式，只有生成时固化的白名单可写。
- 整数、bool和enum允许十进制或`0x`十六进制输入；float32/float64只接受十进制。变量表三种方向均显示“数值格式”；`input`的格式用于记录VOFA命令进制要求但不进入CSV，`output/inout`决定CSV格式，FireWater始终发送十进制数值。

如在生成器勾选“生成 VOFA+ 命令配置文件”，会同时生成 `_vofa.cmds.json`，其中只包含 `input/inout` 白名单变量。配置使用 Str/ASCII 模式及 `%d/%u/%f`；十六进制变量不会生成 `0x%X`，因为 VOFA+ 官方未确认该动态格式，且 Str 参数属于 Float 模式。固定十六进制文本如 `SET,workMode,0xC0` 可由桥接器正确解析。`uint32` 大于 `16777216` 时不要依赖 VOFA+ 动态 Float 参数保持逐整数精度。

## 从 CCS Scripts 直接写入

`input/inout` 变量还会生成原生 GEL `dialog` 函数，不依赖 VOFA+ 或 JavaScript适配器。加载 GEL 后打开 `Scripts → Target Variable Input`，选择 `TargetInput_Set_序号_名称`，在弹窗中输入目标值。写入后 CCS Console 会显示实际回读值。`output` 变量不会生成写入函数。

Scripts 写入与 VOFA+ 写入最终都通过 JTAG表达式访问目标变量；前者适合人工调参，后者适合由VOFA+控件发送低速指令。两者均不能作为实时闭环控制通道。

CCS 20.x适配器支持重复加载和每1秒断线重连。写入优先执行`变量=数值`，失败后才尝试带类型转换。`NO_DEBUG_SESSION`表示两个接口均不可用，`ASSIGN_FAILED`表示两种赋值表达式均失败，`READBACK_FAILED`表示赋值后无法回读；真实异常会同时打印在Scripting Console。

桥接页面分别显示“CCS写入适配器”TCP状态和“CCS目标会话”状态。CPU核使用固定下拉项`C28xx_CPU1/C28xx_CPU2/CPU1/CPU2`；目标会话应显示唯一核名并标记“已就绪”。

Legacy DSS 与 CCS 20 Scripting 的表达式求值接口均为 CCS 原生调试服务器能力。参考：<https://software-dl.ti.com/ccs/esd/documents/scripting_api/ccs_12.8.0/dss/docs/DS_API/com/ti/debug/engine/scripting/Expression.html>、<https://software-dl.ti.com/ccs/esd/documents/users_guide_ccs_20.5.1/ccs_debug-scripting.html>。

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
- VOFA+ 反向写入也通过 JTAG 表达式求值完成。它不是实时控制链路，不能替代目标端控制协议；禁止用于高频闭环指令。
- VOFA+中应选择 TCP Client 和 FireWater，连接生成器显示的监听地址与端口。
- FireWater不携带通道名称；使用生成器自动复制的列表，在 VOFA+中按同一顺序命名。
