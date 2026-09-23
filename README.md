# GelGenerator

Windows 桌面 GEL 数据采集脚本生成器，使用 C# WinForms 和 .NET Framework 开发。配置变量清单、周期和保存路径后，生成由 TI CCS 执行的 `.gel` 脚本，通过 JTAG 读取运行中的目标变量。另提供 CCS CSV 清理及 XLSX 转换。

## 功能与需求

- 配置 `input/output/inout`、CSV/VOFA 名称、全局变量表达式、目标类型及输出格式。
- 支持数组元素和多层结构体成员，不自动展开整数组/结构体。
- 支持 int8/uint8/int16/uint16/int32/uint32、float32/float64、bool、enum16/enum32；浮点仅使用十进制。
- 可配置 ms/s 周期，提供启动、停止、状态和异常后清理函数。
- 保存/读取 JSON 配置；不兼容类型会报错。
- 清理 CCS CPU/GEL 前缀，重组拆行 CSV，导出带筛选和冻结表头的 XLSX。
- 超过 15 位有效数字的十进制整数字面量在 XLSX 中保存为文本。
- 支持 CCS 5.5、8.3、12.7.1 和 20.x 版本化环境；可从 CCS Scripts 原生 GEL 对话框写入，也可通过 CCS Legacy DSS/新 Scripting 将 VOFA+ `SET` 命令写入白名单变量并回读。VOFA+波形与命令共用一个全双工连接，CCS适配器使用独立本机端口。

## 环境与快速使用

当前交付为 Windows x64 可执行文件，面向 Windows 10/11，依赖系统 .NET Framework；并非所有 Windows 版本均已验证。生成器不需要安装 Python、MATLAB 或 Excel。

1. 运行根目录 `GelGenerator.exe`，配置路径、采样周期和变量。
2. 生成 GEL。在 CCS 中连接目标、加载含调试符号的程序，并确认 CPU 为 Run。
3. 在 GEL Files 视图加载脚本，通过 Scripts 菜单的 Data Logger 启停。
4. 停止采集后，使用“CSV 转 XLSX”页转换文件。

详细文档：

- [生成器使用说明](README/GEL生成器_使用说明.md)
- [CCS 脚本文件使用说明](README/CCS脚本文件使用说明.md)：包含 CCS 12.7.1、20.5.1、8.3 和 5.5 操作参考；不代表全部版本已经实机验证。

## 构建与测试

在 Windows 项目根目录运行：

```bat
build_gel_generator.cmd
```

脚本使用系统 .NET Framework `csc.exe`，直接输出根目录 `GelGenerator.exe`。当前工程无需 `.sln` 或 `.csproj`。

PowerShell 回归测试命令：

```powershell
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /out:tests\TestGenerator.exe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll GelGenerator\Program.cs tests\TestGenerator.cs /main:TestGenerator
if ($LASTEXITCODE -eq 0) { & .\tests\TestGenerator.exe }
```

测试覆盖模板生成、配置迁移、格式拒绝、输入边界值、uint32 最大值、双向 TCP 转发、CSV 清理/拆行和大整数 XLSX 文本保存。生成模板测试不等于 GEL/DSS 解释器或目标板实机验证。

## 当前限制与待验证项

- 仅适合几 Hz～几十 Hz 低速监测，严禁用于 kHz 控制环采集；设定周期不是实时保证。
- 纯 JTAG Run 模式采集，目标必须支持运行态访问。变量必须是未被优化删除的全局符号。
- 读取是顺序操作，不构成原子快照；GEL 不能捕获所有驱动或非法地址故障。
- 32 位临时量已使用 long/unsigned long，但仍直接使用 GEL `%d/%u/%x` 输出。格式化器的完整 32 位支持尚待实机验证，逐位输出方案尚未实现。
- 周期和计数器已使用 long；状态输出仍需要验证超过 32767 的行为，计数器也不是无限范围。
- float64 临时量使用 long double，仍通过 `%f` 输出；尚不能保证完整双精度往返精度。
- enum16/enum32 目前按有符号类型解释。C28x 的 8 位类型和 typedef 需核对实际存储定义。
- 十六进制输出目前没有统一 `0x` 前缀，仅含数字的十六进制值在 XLSX 转换时可能被当作十进制数；此项待修复。

## 目录结构

```text
GelGenerator.exe             可执行文件
GelGenerator/Program.cs       界面、GEL 模板、CSV/XLSX 转换
GelGenerator/assets/          图标资源
GelGenerator/app.manifest     Windows 清单
build_gel_generator.cmd       构建入口
tests/                       软件回归测试与合成 CSV 样例
tools/                       图标处理辅助代码
README/                      详细使用说明
```

个人配置 `json/` 随项目提交，包含本地路径与变量清单，换机使用时需要调整路径。采集文件、临时测试产物和 dist 不提交。根目录 EXE 随源码一起管理，修改代码后应重新构建后提交。

## 版本更迭

项目此前未使用 Git 标签，以下按开发记录整理，不虚构语义版本号。

| 阶段 | 变更 |
|---|---|
| 初始开发 | WinForms 配置界面、GEL 模板、JSON 配置和图标 |
| CSV/XLSX 扩展 | CCS 前缀清理、拆行重组、XLSX 导出；EXE 放置于根目录 |
| 类型与格式分离 | 明确类型选择、输出格式限制、大整数文本保存 |
| 2026-09-10 界面调整 | 增加 int8/uint8，简化类型名称；移除 int64/uint64、科学计数法及精度列 |
| 2026-09-10 位宽调整 | int32/uint32 改用 long/unsigned long；枚举拆分；周期与计数器加宽 |
| Git 管理基线 | 添加忽略规则、保留所有权利声明、根 README 和已知限制 |
| 2026-09-22 双向桥接 | 增加 input/output/inout、CCS 版本选择、VOFA+ SET 写入、DSS/Scripting 适配器及写后回读 |
| 2026-09-22 CCS20适配器修复 | 自动兼容session/activeDS、无转换优先、错误分类、安全重复加载和断线重连 |
| 2026-09-22 会话自动初始化 | CCS20自动listCores/openSession、CPU核匹配、目标会话状态和VOFA首次命令流同步 |
| 2026-09-23 单连接双向桥接 | VOFA+波形/命令合并为一个TCP连接、取消文本ACK回传、CPU核改为固定下拉选择 |

后续建议每次发布记录变更及验证环境，并使用 Git 标签标记实际测试过的版本。

## 权利声明

Copyright © 2026 Ethan Wang. All rights reserved. 本项目未授予开源使用许可，详见 [LICENSE](LICENSE)。仓库可见性由 GitHub 设置决定。
