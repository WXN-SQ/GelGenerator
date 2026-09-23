# GEL 生成器使用说明

## 软件功能

项目顶层 `GelGenerator.exe` 可生成纯 CCS GEL/JTAG 低速采集脚本、生成并运行 VOFA+ FireWater 实时桥接，以及批量清理 CCS CSV 前缀并转换为 `.xlsx`。面向 Windows 10/11 x64，使用系统 .NET Framework；独立桥接脚本使用 Windows 自带 PowerShell，无需 Python、MATLAB 或 Excel。

## 变量配置

每行填写方向、CSV/VOFA 名称、CCS/GEL 变量表达式、目标数据类型和输出格式。方向含义：`output` 只采集，`input` 只允许写入且输出格式固定为“无”，`inout` 同时采集和写入并可选择十进制或十六进制。支持数组元素、多层结构体成员和有效指针成员：

```text
gTelemetry.attitude.yaw
gControllers[0].positionRef
gDevicePtr->controller.feedback
```

暂不支持整数组或整个结构体自动展开。目标变量必须是带固定地址、未被优化删除的全局符号。

### 表格编辑与复制

- 单击单元格后直接输入，或按 `F2` 进入编辑状态；输入字符时不会自动退出。
- 按 `Enter` 或 `Tab`提交，按 `Esc`取消，或点击其他位置结束当前编辑。
- 选中单个单元格后按 `Ctrl+C`，只复制该单元格内容。
- 框选多个单元格时，按所选行列区域复制，不会自动扩展为整行。
- 点击表格最左侧的行头可选中整行；`Ctrl+点击行头`选择多个不连续行，`Shift+点击行头`选择连续多行。
- “删除选中”“上移”“下移”针对通过行头选中的整行。

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

“生成 GEL 文件”旁必须选择目标 CCS 版本：CCS 5.5、8.3、12.7.1 或 20.x。该全局选项同时决定 GEL 文件标识和输入桥接适配器类型；旧 JSON 配置默认迁移为 CCS 12.7.1，旧变量默认迁移为 `output`。

## VOFA+ 实时桥接

在 GEL生成页勾选“启用 VOFA+桥接并生成独立脚本”后，`output/inout` 行的“发送到 VOFA+”才可编辑；`input` 行不会进入 CSV。生成 GEL 时会在相同目录生成：

```text
原GEL文件名_vofa_bridge.ps1
原GEL文件名_ccs_input_bridge.js（存在 input/inout 时）
```

同时会把 `sample` 和已选变量名称按通道顺序复制到剪贴板。FireWater帧只传输数字，通道名称仍需在 VOFA+中按复制顺序设置。

“VOFA+实时桥接”页默认配置：

```text
桥接程序：TCP Server
监听地址：127.0.0.1
端口：1347
CCS 输入适配器端口：1349（只允许本机连接）
CPU核匹配：C28xx_CPU1
VOFA+：TCP Client
协议引擎：FireWater
```

使用步骤：

1. 在 GEL生成页启用桥接并选择要发送的变量。
2. 在 VOFA+中新建 TCP Client，连接 `127.0.0.1:1347`，选择 FireWater。
3. 在生成器的桥接页选择“仅跟随新增数据”或“回放已有数据后继续跟随”。
4. 点击“启动桥接”，再启动或继续 GEL采集。
5. 点击“停止桥接”关闭监听和当前连接。

### 通过 CCS Scripts 写入

每个 `input/inout` 变量都会生成一个原生 GEL `dialog` 函数。加载 GEL 后进入 `Scripts → Target Variable Input`，选择对应的 `TargetInput_Set_序号_名称`，在弹窗中输入数值。函数写入后会立即回读，并在 CCS Console 输出 `[TargetInput] 名称=实际值`。此方式不需要启动 VOFA+、GelGenerator 桥接或 JavaScript 适配器。

生成参数类型映射：int8/int16/enum16→`int`，uint8/uint16/bool→`unsigned int`，int32/enum32→`long`，uint32→`unsigned long`，float32→`float`，float64→`long double`。GEL 对话框自身不提供生成器级边界校验，输入者必须保证数值符合目标类型。

### VOFA+ 反向写入

1. 启动 GelGenerator 内置桥接。
2. 在 CCS 中加载生成的 `_ccs_input_bridge.js`。CCS 5.5/8.3/12.7.1 使用 Legacy DSS Scripting Console，并先在 Debug 视图选中目标 CPU，使 `activeDS` 正确；CCS 20.x适配器会自动检测已有会话。若不存在，会调用`initScripting({inProcess:true})`、读取`listCores()`并按桥接页的“CPU核匹配”自动`openSession()`。
3. 看到“CCS 写入适配器：已连接”后，手动勾选“允许 VOFA+ 反向写入目标变量”。该开关每次启动默认关闭。
4. VOFA+只建立一个TCP Client连接波形端口1347并选择FireWater；命令控件通过同一个当前数据接口发送`SET,yaw_ref,12.5\n`。名称必须是`input/inout`白名单名称。
5. 为避免ACK/ERR文本污染FireWater，写入结果不回传到VOFA+，而是在GelGenerator的最近命令、成功/失败计数和错误状态中显示。

写入支持 int8/uint8/int16/uint16/int32/uint32、float32/float64、bool、enum16/enum32。整数、布尔和枚举可用十进制或带 `0x` 前缀的十六进制，例如 `SET,flags,0xFF`、`SET,count,0xFFFFFFFF`；浮点只接受十进制。生成器会拒绝整数小数、溢出值、无符号负数、NaN、Infinity，以及 bool 的非 0/1/0x0/0x1 值。ACK统一返回十进制回读值。未设置最小值/最大值；请在控制算法中自行保证指令范围。

`inout`选择十六进制时，CSV按十六进制保存；VOFA+ FireWater波形仍转换为十进制数值，避免协议解析歧义。

CCS 20.x适配器可安全重复加载：新脚本会停止旧Socket再建立连接，不会出现`Identifier 'socket' has already been declared`。GelGenerator尚未启动或重启时，适配器每1秒自动重连。写入先尝试无强制转换表达式，失败后才尝试带类型转换；错误区分为`NO_DEBUG_SESSION`、`ASSIGN_FAILED`和`READBACK_FAILED`，详细异常打印在CCS Scripting Console。

CPU核匹配为固定下拉选项：`C28xx_CPU1`、`C28xx_CPU2`、`CPU1`、`CPU2`，默认`C28xx_CPU1`。适配器优先匹配路径最后一段的完整名称，并排除CLA核。匹配为0个或多个时拒绝写入并报告候选核。首次VOFA连接附带的`plot0`等无换行前缀会在找到完整`SET,`后安全丢弃，后续仍执行白名单与类型检查。

波形和SET命令共用1347全双工TCP连接：GelGenerator只向该连接发送FireWater数字帧，VOFA+通过同一连接反向发送命令。`inout`变量可在后续采样中看到回读值；`input`变量的回读结果只显示在GelGenerator。独立PowerShell脚本保留原有CSV→VOFA+单向输出能力。

“仅跟随新增数据”会跳过启动前的历史记录；“回放已有数据”会按 GEL采集周期发送历史完整行，再继续监听新数据。VOFA+断开期间不缓存数据，重新连接后只发送后续新行。

桥接会处理 CSV暂时不存在、CCS共享占用、未完成半行、文件清空/覆盖/重建、重复表头和 CPU/GEL前缀。列数变化时暂停发送，避免通道错位；单行非数字或格式错误会跳过并累计错误数。

十六进制 GEL列在发送 FireWater前会转换为十进制数值。所有桥接通道必须是整型或浮点型，不支持字符串。

关闭 GelGenerator时若应用内桥接仍在运行，程序会要求确认并停止桥接。生成的独立 `.ps1` 可以在关闭生成器后单独运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "D:\Logger\data_logger_vofa_bridge.ps1"
```

桥接开关、变量选择、地址、端口和回放模式会随 JSON配置保存。旧配置默认不启用桥接。

旧 JSON 配置中的 `float/double/int/unsigned int/hex` 及上一版带括号的明确类型会自动迁移。旧科学计数法迁移为十进制；旧 `int64/uint64/string` 和宽度不明确的旧 `enum` 会被拒绝。

## CSV 转 XLSX

在“CSV 转 XLSX”页批量选择文件、选择输出目录后转换。程序会清理 `C28xx_CPU1:`、`GEL Output:` 等前缀，并按表头列数重组 CCS 拆行输出。

十进制整数超过 Excel 15 位有效数字时写为文本单元格，完整保留数字；15 位以内整数和浮点值写为数值，十六进制保留为文本。

## 核实依据与验证状态

TI 官方 GEL 文档确认参数可接收目标符号/表达式；格式仅支持 `%d/%u/%x/%f/%e/%s`，且 `%s` 只能是主机端常量字符串：

- <https://software-dl.ti.com/ccs/esd/documents/users_guide_12.7.0/gel/format.html>
- <https://software-dl.ti.com/ccs/esd/documents/users_guide/gel/param.html>

软件自动测试覆盖类型/格式校验、旧配置迁移、结构体/数组表达式、输入数值边界、uint32 最大值不截断、双 TCP 通道命令转发与回读、各代适配器模板、CSV 前缀清理与拆行重组、Excel 15 位整数边界。

自动测试不能代替 CCS 实机验证。首次使用 `Uint32/unsigned long`、enum32、float64 或特定 typedef 时，应在具体 C28x、ABI、CCS 和仿真器组合上用边界值比较 Expressions 与 CSV。GEL `%u/%x` 没有长度修饰符，因此 32 位格式化结果必须以实机结果为准。

## 性能限制

- 仅适合几 Hz～几十 Hz；严禁用于 kHz 级高速控制环。
- CPU 必须保持 Run，脚本不会暂停或恢复 CPU。
- 多变量顺序读取，不是原子快照。
- 实际周期受 JTAG、变量数量、目标存储器及 PC 调度影响。
