# VOFA+ 1.3.10 控件开发说明

## 环境基线

- 目标软件：VOFA+ 1.3.10（Windows x64）。
- 运行时：Qt 5.14.2；本机 `Qt5Core.dll` 已验证为 5.14.2。
- 控件语言：QML/JavaScript，不需要单独编译。
- 推荐编辑器：Qt Creator 配套 Qt 5.14.2（可选）；普通代码编辑器也可完成开发。
- 调试：VOFA+ 启动时加载 QML；可用 `console.log()` 输出信息，并用 DebugView 查看。

不要依赖 Qt 5.14.2 以后才增加的 QML 类型或属性。为贴近官方示例，基础导入优先使用 `QtQuick 2.12` 与 `MyModules 1.0`。

## 控件打包约定

每个控件必须能够作为完整文件夹复制：

```text
widgets/
└── my_widget/
    ├── my_widget.qml       # 必需入口
    └── ...                 # 本控件自带的图片、QML 子组件等
```

以下三者必须完全一致且区分大小写：

1. 文件夹名 `my_widget`；
2. 入口文件名 `my_widget.qml`；
3. 根对象属性 `property string path: "my_widget"`。

为了保持控件独立，不要引用工作区中其他控件目录。通用资源应复制到控件自己的文件夹内。VOFA+ 自带的 `MyModules 1.0` 可以直接使用。

## 必备生命周期接口

根对象通常使用 `ResizableRectangle`，并实现：

```qml
function get_widget_ctx() {
    return {
        "path": path,
        "ctx": {
            ".": { "ctx": get_ctx() }
        }
    }
}

function set_widget_ctx(ctx) {
    __set_ctx__(root, ctx.ctx, ref)
}
```

`get_widget_ctx()` 保存状态，`set_widget_ctx()` 恢复状态。若控件包含 `ChMenu`、`CmdMenu` 等可持久化对象，应在 `ctx` 中保存其 `get_ctx()`，并通过懒加载 `ref_<id>` 引用让 `__set_ctx__` 恢复。

## 常用 VOFA+ 接口

- `ChMenu.bind_obj`：已绑定通道；常用属性有 `name`、`color`、`scale`、`yoffset`、`xoffset`、`decimal`、`value`。
- `CmdMenu.bind_obj`：已绑定命令；常用属性有 `name`、`hex_on`、`loop_on`、`loop_ms`、`cmd_hex`。
- `sys_manager.send_string(data, arg)`：发送 UTF-8 字符串。
- `sys_manager.send_hex(data, arg)`：发送十六进制字符串。
- `sys_manager.send_command(name, cmd, arg, arg_hex_on)`：按 VOFA+ 命令模型发送。
- `sys_manager.connected` / `sending` / `image_count`：连接、发送及图片通道状态。
- `g_settings.applyHScale/VScale/FontScale()`：屏幕尺寸适配。
- `appTheme`：只读主题颜色。不要修改 `g_settings` 或 `appTheme` 的全局属性。

## 开发与验证流程

1. 复制 `templates/basic_widget`，放入 `widgets/<新名称>`。
2. 将入口 QML 重命名为 `<新名称>.qml`，并修改 `path`。
3. 运行 `scripts/validate-widgets.ps1`。
4. 用 `scripts/deploy-widget.ps1 -WidgetName <新名称>` 部署。
5. 重启 VOFA+，在控件栏刷新并拖入画布。
6. 测试缩放、右键删除、状态保存与恢复，以及对应的数据/命令功能。
7. 修改后必须重启 VOFA+ 才能重新加载。

## 已知边界

`MyModules 1.0`、`ResizableRectangle`、`MyMenu` 等是 VOFA+ 私有运行时类型，独立的 `qmlscene`/Qt Creator 设计器无法完整运行该控件。最终验证必须在 VOFA+ 中进行。

## 已开发控件

### spot_deviation_target（光斑脱靶量）

将 `widgets/spot_deviation_target` 整个文件夹复制到 VOFA+ 的 `plugins/widgets`。控件通过右键菜单绑定跟踪点 X/Y 与光斑 X/Y，按 `(光斑 - 跟踪点)` 绘制红色历史点；X 向右、Y 向上。边界圆直径和历史点上限可在设置菜单修改，右上角按钮或菜单均可清空历史点。

默认边界圆直径为 100 个通道数据单位，默认历史上限为 1000 点。达到上限后，下一个有效样本会清空旧点并作为新一轮第一个点。历史点不随工程保存，通道绑定及参数会保存。

圆内十字轴延伸至边界圆，并带有 10 等分主刻度和 20 等分次刻度；圆内刻度不显示数字。方形绘图区左侧和下侧显示覆盖完整绘图区的坐标刻度及数值，数值会根据边界圆直径自动换算。底部状态卡显示最新 ΔX、ΔY、径向脱靶量 R 和历史点数。界面采用深色分层面板、青蓝色靶标、细网格和红色光斑光晕。

标题栏右侧按钮和右键菜单可切换控件自身的深色/浅色主题。主题会同步调整背景、绘图区、网格、坐标、靶标、状态卡和光斑对比度，不修改 VOFA+ 全局主题；主题选择随工程保存。

### pid_parameter_tuner（PID 参数调节）

将 `widgets/pid_parameter_tuner` 整个文件夹复制到 VOFA+ 的 `plugins/widgets`。Kp、Ki、Kd 是控件内部保存的本地参数，首次创建时均为 0；控件提供三个独立浮点输出命令，并允许只绑定其中任意 1～3 个。

每个参数均提供减号、数值输入、加号和提交按钮。加减按钮会按独立步进立即提交；数值输入通过 Enter 或提交按钮发送。默认范围为 −50～50，默认步进为 0.01，范围和步进可在右键设置菜单中独立修改，但不占用主界面空间。未绑定输出命令的参数仍可编辑，但不会产生默认命令。

底部提供一键全部发送及 10 个固定参数槽位。保存操作记录当前 Kp、Ki、Kd；恢复操作载入选中槽位并按 Kp、Ki、Kd 顺序发送所有已绑定命令。输出绑定、范围、步进、本地值和参数槽位均随 VOFA+ 工程保存。

双击标题可进入编辑，按 Enter 或让输入框失去焦点即可保存。标题栏右侧按钮和右键菜单都可切换控件自身的深色/浅色主题，不会修改 VOFA+ 全局主题；自定义标题及主题选择均随工程保存。

### waveform_monitor（波形监视器）

将 `widgets/waveform_monitor` 整个文件夹复制到 VOFA+ 的 `plugins/widgets`。控件支持最多四条独立波形和一个可选横轴通道。未绑定横轴通道时，横轴使用从首次有效采样开始计算的本机相对时间（秒）；绑定后直接以通道值作为“序号”横坐标。

数据刷新时，只有至少一条已绑定曲线的数值发生变化才会同步记录一次全部通道。缓存默认1000点，可设置为100～10000点，超限后滚动移除最旧记录。每条曲线均可独立绑定、显示、命名和配色。

标题栏提供暂停、清空、跟随、自动量程和深浅主题切换。绘图区支持滚轮以光标为中心缩放、左键拖动平移、双击恢复自动跟随与自动量程，并可显示网格、数据点和悬停数据。右键菜单可配置缓存长度、线宽、手动Y轴范围及曲线属性。历史数据不随工程保存，其他设置和五个通道绑定均会保存。

## 资料来源

- https://www.vofa.plus/docs/learning/widgets/introduce/
- https://www.vofa.plus/docs/learning/widgets/development/
- https://gitee.com/gutega/Vodka/tree/master/widgets/
