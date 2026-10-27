# VOFA+ 1.3.10 自定义控件开发工作区

本目录用于开发可直接复制到 VOFA+ `plugins/widgets` 目录的 QML 自定义控件。

## 目录

- `widgets/`：实际控件，一控件一文件夹。
- `templates/basic_widget/`：新控件模板；复制后同时修改文件夹名、入口 QML 文件名及 `path`。
- `scripts/validate-widgets.ps1`：静态检查目录、入口文件、`path`、关键接口和 QML 导入版本。
- `scripts/deploy-widget.ps1`：将指定控件复制到 VOFA+ 的 `plugins/widgets` 目录。
- `docs/DEVELOPMENT.md`：开发规范、API 摘要和调试流程。

## 快速开始

在 PowerShell 中进入本目录：

```powershell
./scripts/validate-widgets.ps1
./scripts/deploy-widget.ps1 -WidgetName hello_widget
```

默认 VOFA+ 目录为：

```text
C:\Program Files\Gutega\VOFA+\x64\plugins\widgets
```

向 `Program Files` 写入通常需要管理员权限。也可以明确指定其他测试目录：

```powershell
./scripts/deploy-widget.ps1 -WidgetName hello_widget -DestinationRoot C:\path\to\plugins\widgets
```

部署后重启 VOFA+；若左侧缩略图没有更新，点击控件栏的刷新按钮。
