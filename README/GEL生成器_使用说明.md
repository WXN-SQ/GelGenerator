# GEL 生成器使用说明

## 软件功能

项目顶层 `GelGenerator.exe` 可生成纯 CCS GEL/JTAG 低速采集脚本，并可批量清理 CCS CSV 前缀、转换为 Excel 可打开的 `.xlsx`。面向 Windows 10/11 x64，使用系统 .NET Framework，无需 Python、MATLAB 或 Excel。

## 变量配置

每行分别填写 CSV 列名称、CCS/GEL 变量表达式、目标数据类型、输出格式。支持数组元素、多层结构体成员和有效指针成员：

```text
gTelemetry.attitude.yaw
gControllers[0].positionRef
gDevicePtr->controller.feedback
```

暂不支持整数组或整个结构体自动展开。目标变量必须是带固定地址、未被优化删除的全局符号。

| 类型 | 常用类型/别名 | 可选格式 | GEL 格式 |
|---|---|---|---|
| int8 | `int8_t/I8/Int8`（须由目标工程实际定义） | 十进制、十六进制 | `%d/%x` |
| uint8 | `uint8_t/U8/Uint8`（须由目标工程实际定义） | 十进制、十六进制 | `%u/%x` |
| int16 | `int16_t/I16/Int16/int` | 十进制、十六进制 | `%d/%x` |
| uint16 | `uint16_t/U16/Uint16/unsigned int` | 十进制、十六进制 | `%u/%x` |
| int32 | `int32_t/I32/Int32/long` | 十进制、十六进制 | `%d/%x` |
| uint32 | `uint32_t/U32/Uint32/unsigned long` | 十进制、十六进制 | `%u/%x` |
| float32 | `float/float32_t` | 十进制 | `%f` |
| float64 | `double/float64_t` | 十进制 | `%f` |
| bool | 布尔量，按 0/1 | 十进制、十六进制 | `%u/%x` |
| enum16 | 16 位有符号底层枚举 | 十进制、十六进制 | `%d/%x` |
| enum32 | 32 位有符号底层枚举 | 十进制、十六进制 | `%d/%x` |

生成模板对 `int32/enum32` 使用 `long`，对 `uint32` 使用 `unsigned long`，避免 C28x 16 位 `int` 截断；float64 使用 `long double`。C28x `double` 在 COFF ABI 中是 32 位、EABI 中是 64 位。

## 已取消的类型与格式

- `int64`、`uint64` 已从界面删除，因为 GEL 没有 `%lld/%llu`，无法证明 64 位无损输出。
- 科学计数法和浮点精度设置已删除；浮点统一使用 GEL `%f` 十进制输出。
- 目标字符串已取消。GEL `%s` 仅接受主机端常量字符串，不等同于目标端 `char[]` 或指针。

不兼容组合会被生成器阻止。

## 使用步骤

1. 打开项目顶层 `GelGenerator.exe`。
2. 在“GEL 脚本生成”页设置 CSV 路径、周期和 GEL 输出路径。
3. 添加变量，建议先在 CCS Expressions 验证表达式和实际数值。
4. 单击“生成 GEL 文件”。

旧 JSON 配置中的 `float/double/int/unsigned int/hex` 及上一版带括号的明确类型会自动迁移。旧科学计数法迁移为十进制；旧 `int64/uint64/string` 和宽度不明确的旧 `enum` 会被拒绝。

## CSV 转 XLSX

在“CSV 转 XLSX”页批量选择文件、选择输出目录后转换。程序会清理 `C28xx_CPU1:`、`GEL Output:` 等前缀，并按表头列数重组 CCS 拆行输出。

十进制整数超过 Excel 15 位有效数字时写为文本单元格，完整保留数字；15 位以内整数和浮点值写为数值，十六进制保留为文本。

## 核实依据与验证状态

TI 官方 GEL 文档确认参数可接收目标符号/表达式；格式仅支持 `%d/%u/%x/%f/%e/%s`，且 `%s` 只能是主机端常量字符串：

- <https://software-dl.ti.com/ccs/esd/documents/users_guide_12.7.0/gel/format.html>
- <https://software-dl.ti.com/ccs/esd/documents/users_guide/gel/param.html>

软件自动测试覆盖类型/格式校验、旧配置迁移、结构体/数组表达式、64 位与精度禁用、CSV 前缀清理与拆行重组、Excel 15 位整数边界。

自动测试不能代替 CCS 实机验证。首次使用 `Uint32/unsigned long`、enum32、float64 或特定 typedef 时，应在具体 C28x、ABI、CCS 和仿真器组合上用边界值比较 Expressions 与 CSV。GEL `%u/%x` 没有长度修饰符，因此 32 位格式化结果必须以实机结果为准。

## 性能限制

- 仅适合几 Hz～几十 Hz；严禁用于 kHz 级高速控制环。
- CPU 必须保持 Run，脚本不会暂停或恢复 CPU。
- 多变量顺序读取，不是原子快照。
- 实际周期受 JTAG、变量数量、目标存储器及 PC 调度影响。
