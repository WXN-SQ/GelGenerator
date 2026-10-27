using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace GelGenerator
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public sealed class VariableItem
    {
        public string Direction { get; set; }
        public string Name { get; set; }
        public string Expression { get; set; }
        public string Type { get; set; }
        public string OutputFormat { get; set; }
        public bool SendToVofa { get; set; }
    }

    public sealed class GeneratorConfig
    {
        public string CsvPath { get; set; }
        public decimal PeriodValue { get; set; }
        public string PeriodUnit { get; set; }
        public string GelPath { get; set; }
        public List<VariableItem> Variables { get; set; }
        public bool VofaEnabled { get; set; }
        public string VofaAddress { get; set; }
        public int VofaPort { get; set; }
        public bool VofaReplayExisting { get; set; }
        public string CcsVersion { get; set; }
        public bool VofaWriteEnabled { get; set; }
        public string CcsCorePattern { get; set; }
        public bool GenerateVofaCommandConfig { get; set; }
    }

    public sealed class MainForm : Form
    {
        private readonly TextBox txtCsvPath = new TextBox();
        private readonly NumericUpDown numPeriod = new NumericUpDown();
        private readonly ComboBox cmbPeriodUnit = new ComboBox();
        private readonly TextBox txtGelPath = new TextBox();
        private readonly DataGridView grid = new DataGridView();
        private readonly ToolStripStatusLabel statusLabel = new ToolStripStatusLabel();
        private readonly ListBox csvFileList = new ListBox();
        private readonly CheckBox chkSameDirectory = new CheckBox();
        private readonly CheckBox chkOverwriteXlsx = new CheckBox();
        private readonly TextBox txtXlsxDirectory = new TextBox();
        private readonly Label lblConvertStatus = new Label();
        private readonly CheckBox chkVofaEnabled = new CheckBox();
        private readonly CheckBox chkGenerateVofaCommands = new CheckBox();
        private readonly ComboBox cmbCcsVersion = new ComboBox();
        private readonly CheckBox chkVofaWriteEnabled = new CheckBox();
        private readonly TextBox txtBridgeCsvPath = new TextBox();
        private readonly TextBox txtBridgeScriptPath = new TextBox();
        private readonly TextBox txtVofaAddress = new TextBox();
        private readonly NumericUpDown numVofaPort = new NumericUpDown();
        private readonly TextBox txtCcsAdapterPort = new TextBox();
        private readonly ComboBox cmbCcsCorePattern = new ComboBox();
        private readonly RadioButton radFollowNew = new RadioButton();
        private readonly RadioButton radReplay = new RadioButton();
        private readonly Button btnBridgeStart = new Button();
        private readonly Button btnBridgeStop = new Button();
        private readonly Label lblBridgeStatus = new Label();
        private readonly Label lblBridgeConnection = new Label();
        private readonly Label lblAdapterConnection = new Label();
        private readonly Label lblBridgeCommand = new Label();
        private readonly Label lblTargetSession = new Label();
        private readonly Label lblBridgeCounters = new Label();
        private readonly Label lblBridgeError = new Label();
        private readonly TextBox txtBridgeChannels = new TextBox();
        private VofaBridgeService bridgeService;

        internal static readonly string[] GelTypes =
        {
            "int8", "uint8", "int16", "uint16", "int32", "uint32",
            "float32", "float64", "bool", "enum16", "enum32"
        };

        internal static readonly string[] OutputFormats =
        {
            "十进制", "十六进制"
        };

        internal static readonly string[] Directions = { "output", "input", "inout" };
        internal static readonly string[] CcsVersions = { "CCS 5.5", "CCS 8.3", "CCS 12.7.1", "CCS 20.x" };

        public MainForm()
        {
            Text = "CCS GEL 数据采集脚本生成器";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 600);
            Size = new Size(1050, 720);
            Font = new Font("Microsoft YaHei UI", 9F);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            var generatorPage = new TabPage("GEL 脚本生成");
            var bridgePage = new TabPage("VOFA+ 实时桥接");
            var converterPage = new TabPage("CSV 转 XLSX");
            tabs.TabPages.Add(generatorPage);
            tabs.TabPages.Add(bridgePage);
            tabs.TabPages.Add(converterPage);
            Controls.Add(tabs);
            BuildGeneratorUi(generatorPage);
            BuildBridgeUi(bridgePage);
            BuildConverterUi(converterPage);
            txtCsvPath.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "data_logger.csv");
            numPeriod.Value = 1000;
            cmbPeriodUnit.SelectedIndex = 0;
            cmbCcsVersion.SelectedItem = "CCS 12.7.1";
            txtGelPath.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "data_logger.gel");
            txtVofaAddress.Text = "127.0.0.1";
            numVofaPort.Value = 1347;
            cmbCcsCorePattern.Items.AddRange(new object[] { "C28xx_CPU1", "C28xx_CPU2", "CPU1", "CPU2" });
            cmbCcsCorePattern.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbCcsCorePattern.SelectedItem = "C28xx_CPU1";
            numVofaPort.ValueChanged += delegate { UpdateAdapterPort(); };
            radFollowNew.Checked = true;
            txtCsvPath.TextChanged += delegate { SyncBridgePaths(); };
            txtGelPath.TextChanged += delegate { SyncBridgePaths(); };
            chkVofaEnabled.CheckedChanged += delegate { ApplyVofaEnabledState(true); };
            chkVofaWriteEnabled.CheckedChanged += delegate { if (bridgeService != null) bridgeService.SetWriteEnabled(chkVofaWriteEnabled.Checked); };
            SyncBridgePaths();
            ApplyVofaEnabledState(false);
            UpdateAdapterPort();
        }

        private void BuildGeneratorUi(Control parent)
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 1,
                RowCount = 5
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            parent.Controls.Add(root);

            var settings = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 4,
                Padding = new Padding(0, 0, 0, 8)
            };
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            settings.Controls.Add(LabelFor("CSV 保存文件："), 0, 0);
            txtCsvPath.Dock = DockStyle.Fill;
            settings.Controls.Add(txtCsvPath, 1, 0);
            var btnCsv = ButtonFor("浏览…", delegate { BrowseCsv(); });
            settings.Controls.Add(btnCsv, 2, 0);
            settings.SetColumnSpan(btnCsv, 2);

            settings.Controls.Add(LabelFor("采集周期："), 0, 1);
            numPeriod.Minimum = 1;
            numPeriod.Maximum = 86400000;
            numPeriod.DecimalPlaces = 0;
            numPeriod.Width = 180;
            settings.Controls.Add(numPeriod, 1, 1);
            cmbPeriodUnit.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbPeriodUnit.Items.AddRange(new object[] { "ms", "s" });
            cmbPeriodUnit.Width = 80;
            settings.Controls.Add(cmbPeriodUnit, 2, 1);

            settings.Controls.Add(LabelFor("GEL 输出文件："), 0, 2);
            txtGelPath.Dock = DockStyle.Fill;
            settings.Controls.Add(txtGelPath, 1, 2);
            var btnGel = ButtonFor("浏览…", delegate { BrowseGel(); });
            settings.Controls.Add(btnGel, 2, 2);
            settings.SetColumnSpan(btnGel, 2);
            chkVofaEnabled.Text = "启用 VOFA+ 桥接并生成独立脚本";
            chkVofaEnabled.AutoSize = true;
            settings.Controls.Add(chkVofaEnabled, 1, 3);
            settings.SetColumnSpan(chkVofaEnabled, 3);
            chkGenerateVofaCommands.Text = "生成 VOFA+ 命令配置文件";
            chkGenerateVofaCommands.AutoSize = true;
            settings.Controls.Add(chkGenerateVofaCommands, 1, 4);
            settings.SetColumnSpan(chkGenerateVofaCommands, 3);
            root.Controls.Add(settings, 0, 0);

            var note = new Label
            {
                AutoSize = true,
                ForeColor = Color.DarkRed,
                Text = "提示：JTAG 采集仅适合几 Hz～几十 Hz；严禁用于 kHz 级高速控制环。纯 GEL 输出可能包含 CCS CPU 上下文前缀。",
                Padding = new Padding(0, 0, 0, 8)
            };
            root.Controls.Add(note, 0, 1);

            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = true;
            grid.AllowUserToDeleteRows = true;
            grid.AutoGenerateColumns = false;
            /* 普通点击只选中单元格；点击最左侧行头时才选择整行。 */
            grid.SelectionMode = DataGridViewSelectionMode.RowHeaderSelect;
            grid.MultiSelect = true;
            grid.RowHeadersWidth = 55;
            grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "NameColumn", HeaderText = "CSV 列名称", DataPropertyName = "Name",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 30
            });
            var directionColumn = new DataGridViewComboBoxColumn
            {
                Name = "DirectionColumn", HeaderText = "方向", DataPropertyName = "Direction",
                Width = 82, FlatStyle = FlatStyle.Flat
            };
            directionColumn.Items.AddRange(Directions);
            grid.Columns.Insert(0, directionColumn);
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ExpressionColumn", HeaderText = "CCS/GEL 变量表达式", DataPropertyName = "Expression",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 55
            });
            var typeColumn = new DataGridViewComboBoxColumn
            {
                Name = "TypeColumn", HeaderText = "目标数据类型", DataPropertyName = "Type",
                Width = 120, FlatStyle = FlatStyle.Flat
            };
            typeColumn.Items.AddRange(GelTypes);
            grid.Columns.Add(typeColumn);
            var formatColumn = new DataGridViewComboBoxColumn
            {
                Name = "FormatColumn", HeaderText = "数值格式", DataPropertyName = "OutputFormat",
                Width = 105, FlatStyle = FlatStyle.Flat
            };
            formatColumn.Items.AddRange(OutputFormats);
            grid.Columns.Add(formatColumn);
            grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "VofaColumn", HeaderText = "发送到 VOFA+", DataPropertyName = "SendToVofa",
                Width = 110, ThreeState = false
            });
            grid.DefaultValuesNeeded += delegate(object sender, DataGridViewRowEventArgs e)
            {
                e.Row.Cells["TypeColumn"].Value = "float32";
                e.Row.Cells["DirectionColumn"].Value = "output";
                e.Row.Cells["FormatColumn"].Value = "十进制";
                e.Row.Cells["VofaColumn"].Value = chkVofaEnabled.Checked;
            };
            grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                e.ThrowException = false;
            };
            grid.CurrentCellDirtyStateChanged += delegate
            {
                if (grid.IsCurrentCellDirty && grid.CurrentCell != null &&
                    (grid.CurrentCell.OwningColumn.Name == "VofaColumn" || grid.CurrentCell.OwningColumn.Name == "DirectionColumn"))
                    grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            grid.CellValueChanged += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                {
                    if (grid.Columns[e.ColumnIndex].Name == "DirectionColumn") ApplyDirectionState(e.RowIndex);
                    RefreshBridgeChannels();
                }
            };
            grid.CellEndEdit += delegate { RefreshBridgeChannels(); };
            grid.RowsRemoved += delegate { RefreshBridgeChannels(); };
            root.Controls.Add(grid, 0, 2);

            var rowButtons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            rowButtons.Controls.Add(ButtonFor("添加变量", delegate { AddRow(); }));
            rowButtons.Controls.Add(ButtonFor("删除选中", delegate { DeleteSelectedRows(); }));
            rowButtons.Controls.Add(ButtonFor("上移", delegate { MoveSelected(-1); }));
            rowButtons.Controls.Add(ButtonFor("下移", delegate { MoveSelected(1); }));
            rowButtons.Controls.Add(ButtonFor("保存配置…", delegate { SaveConfig(); }));
            rowButtons.Controls.Add(ButtonFor("打开配置…", delegate { LoadConfig(); }));
            rowButtons.Controls.Add(ButtonFor("清空", delegate { ClearRows(); }));
            root.Controls.Add(rowButtons, 0, 3);

            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 4 };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var status = new StatusStrip { SizingGrip = false, Dock = DockStyle.Fill };
            statusLabel.Text = "就绪：请添加至少一个变量。";
            status.Items.Add(statusLabel);
            bottom.Controls.Add(status, 0, 0);
            var generate = ButtonFor("生成 GEL 文件", delegate { GenerateGel(); });
            generate.AutoSize = true;
            generate.Font = new Font(Font, FontStyle.Bold);
            generate.Padding = new Padding(16, 5, 16, 5);
            cmbCcsVersion.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbCcsVersion.Items.AddRange(CcsVersions);
            cmbCcsVersion.Width = 125;
            bottom.Controls.Add(LabelFor("目标 CCS："), 1, 0);
            bottom.Controls.Add(cmbCcsVersion, 2, 0);
            bottom.Controls.Add(generate, 3, 0);
            root.Controls.Add(bottom, 0, 4);
        }

        private void BuildBridgeUi(Control parent)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 5 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            parent.Controls.Add(root);

            var intro = new Label
            {
                AutoSize = true,
                Text = "单连接双向桥接：同一 VOFA+ TCP Client 接收 FireWater 波形并发送 SET 命令；CCS 适配器通过本机独立端口写入目标。",
                Padding = new Padding(0, 0, 0, 8)
            };
            root.Controls.Add(intro, 0, 0);

            var settings = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4 };
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            settings.Controls.Add(LabelFor("CSV 文件："), 0, 0);
            txtBridgeCsvPath.ReadOnly = true; txtBridgeCsvPath.Dock = DockStyle.Fill;
            settings.Controls.Add(txtBridgeCsvPath, 1, 0); settings.SetColumnSpan(txtBridgeCsvPath, 3);
            settings.Controls.Add(LabelFor("独立脚本："), 0, 1);
            txtBridgeScriptPath.ReadOnly = true; txtBridgeScriptPath.Dock = DockStyle.Fill;
            settings.Controls.Add(txtBridgeScriptPath, 1, 1); settings.SetColumnSpan(txtBridgeScriptPath, 3);
            settings.Controls.Add(LabelFor("监听地址："), 0, 2);
            txtVofaAddress.Width = 180; settings.Controls.Add(txtVofaAddress, 1, 2);
            settings.Controls.Add(LabelFor("端口："), 2, 2);
            numVofaPort.Minimum = 1; numVofaPort.Maximum = 65535; numVofaPort.Width = 100;
            settings.Controls.Add(numVofaPort, 3, 2);
            settings.Controls.Add(LabelFor("CCS适配器端口："), 0, 3);
            txtCcsAdapterPort.ReadOnly = true; txtCcsAdapterPort.Width = 100;
            settings.Controls.Add(txtCcsAdapterPort, 1, 3);
            settings.Controls.Add(LabelFor("CPU核匹配："), 0, 4);
            cmbCcsCorePattern.Width = 180;
            settings.Controls.Add(cmbCcsCorePattern, 1, 4); settings.SetColumnSpan(cmbCcsCorePattern, 3);
            radFollowNew.Text = "仅跟随新增数据"; radFollowNew.AutoSize = true;
            radReplay.Text = "回放已有数据后继续跟随"; radReplay.AutoSize = true;
            var modes = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            modes.Controls.Add(radFollowNew); modes.Controls.Add(radReplay);
            settings.Controls.Add(LabelFor("启动模式："), 0, 5);
            settings.Controls.Add(modes, 1, 5); settings.SetColumnSpan(modes, 3);
            chkVofaWriteEnabled.Text = "允许 VOFA+ 反向写入目标变量（默认关闭）";
            chkVofaWriteEnabled.AutoSize = true;
            settings.Controls.Add(chkVofaWriteEnabled, 1, 6); settings.SetColumnSpan(chkVofaWriteEnabled, 3);
            root.Controls.Add(settings, 0, 1);

            txtBridgeChannels.Dock = DockStyle.Fill;
            txtBridgeChannels.Multiline = true;
            txtBridgeChannels.ReadOnly = true;
            txtBridgeChannels.ScrollBars = ScrollBars.Vertical;
            txtBridgeChannels.Font = new Font("Consolas", 9F);
            root.Controls.Add(txtBridgeChannels, 0, 2);

            var state = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1 };
            lblBridgeStatus.Text = "状态：未启用 VOFA+ 桥接。";
            lblBridgeConnection.Text = "VOFA+ 波形连接：未监听";
            lblAdapterConnection.Text = "CCS 写入适配器：未连接";
            lblBridgeCommand.Text = "最近命令：无";
            lblTargetSession.Text = "CCS 目标会话：未就绪";
            lblBridgeCounters.Text = "已发送：0；跳过：0";
            lblBridgeError.Text = "最近错误：无";
            Label[] bridgeLabels = { lblBridgeStatus, lblBridgeConnection, lblAdapterConnection, lblTargetSession, lblBridgeCommand, lblBridgeCounters, lblBridgeError };
            foreach (Label label in bridgeLabels)
            {
                label.AutoSize = true;
                label.Dock = DockStyle.Fill;
                label.Margin = new Padding(0, 2, 0, 2);
            }
            state.Controls.Add(lblBridgeStatus); state.Controls.Add(lblBridgeConnection); state.Controls.Add(lblAdapterConnection); state.Controls.Add(lblTargetSession); state.Controls.Add(lblBridgeCommand);
            state.Controls.Add(lblBridgeCounters); state.Controls.Add(lblBridgeError);
            root.Controls.Add(state, 0, 3);

            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            btnBridgeStop.Text = "停止桥接"; btnBridgeStop.AutoSize = true; btnBridgeStop.Enabled = false;
            btnBridgeStop.Click += delegate { StopBridge(); };
            btnBridgeStart.Text = "启动桥接"; btnBridgeStart.AutoSize = true;
            btnBridgeStart.Font = new Font(Font, FontStyle.Bold);
            btnBridgeStart.Click += delegate { StartBridge(); };
            var copyStatus = ButtonFor("复制状态信息", delegate { CopyBridgeStatus(); });
            buttons.Controls.Add(copyStatus);
            buttons.Controls.Add(btnBridgeStop); buttons.Controls.Add(btnBridgeStart);
            root.Controls.Add(buttons, 0, 4);
        }

        private void BuildConverterUi(Control parent)
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 1,
                RowCount = 5
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            parent.Controls.Add(root);

            var intro = new Label
            {
                AutoSize = true,
                Text = "批量选择 CCS 输出的 CSV，清除 C28xx_CPU1、GEL Output 等调试前缀，并生成 Excel 可直接打开的 XLSX。",
                Padding = new Padding(0, 0, 0, 8)
            };
            root.Controls.Add(intro, 0, 0);

            csvFileList.Dock = DockStyle.Fill;
            csvFileList.SelectionMode = SelectionMode.MultiExtended;
            csvFileList.HorizontalScrollbar = true;
            root.Controls.Add(csvFileList, 0, 1);

            var fileButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            fileButtons.Controls.Add(ButtonFor("添加 CSV…", delegate { AddCsvFiles(); }));
            fileButtons.Controls.Add(ButtonFor("移除选中", delegate { RemoveSelectedCsvFiles(); }));
            fileButtons.Controls.Add(ButtonFor("清空列表", delegate { csvFileList.Items.Clear(); UpdateConvertStatus("列表已清空。"); }));
            root.Controls.Add(fileButtons, 0, 2);

            var options = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 4 };
            options.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            chkSameDirectory.Text = "输出到原 CSV 目录";
            chkSameDirectory.Checked = true;
            chkSameDirectory.AutoSize = true;
            chkSameDirectory.CheckedChanged += delegate
            {
                txtXlsxDirectory.Enabled = !chkSameDirectory.Checked;
            };
            options.Controls.Add(chkSameDirectory, 0, 0);
            options.SetColumnSpan(chkSameDirectory, 4);
            options.Controls.Add(LabelFor("自定义输出目录："), 0, 1);
            txtXlsxDirectory.Dock = DockStyle.Fill;
            txtXlsxDirectory.Enabled = false;
            txtXlsxDirectory.Text = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            options.Controls.Add(txtXlsxDirectory, 1, 1);
            options.Controls.Add(ButtonFor("浏览…", delegate { BrowseXlsxDirectory(); }), 2, 1);
            chkOverwriteXlsx.Text = "直接覆盖同名 XLSX";
            chkOverwriteXlsx.AutoSize = true;
            options.Controls.Add(chkOverwriteXlsx, 3, 1);
            root.Controls.Add(options, 0, 3);

            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            lblConvertStatus.AutoSize = true;
            lblConvertStatus.Anchor = AnchorStyles.Left;
            lblConvertStatus.Text = "就绪：请添加一个或多个 CSV 文件。";
            bottom.Controls.Add(lblConvertStatus, 0, 0);
            var convert = ButtonFor("批量转换为 XLSX", delegate { ConvertCsvFiles(); });
            convert.Font = new Font(Font, FontStyle.Bold);
            convert.Padding = new Padding(16, 5, 16, 5);
            bottom.Controls.Add(convert, 1, 0);
            root.Controls.Add(bottom, 0, 4);
        }

        private static Label LabelFor(string text)
        {
            return new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 8, 6) };
        }

        private static Button ButtonFor(string text, EventHandler handler)
        {
            var button = new Button { Text = text, AutoSize = true, Margin = new Padding(4) };
            button.Click += handler;
            return button;
        }

        private void BrowseCsv()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "CSV 文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
                dialog.FileName = SafeFileName(txtCsvPath.Text, "data_logger.csv");
                dialog.InitialDirectory = SafeDirectory(txtCsvPath.Text);
                if (dialog.ShowDialog(this) == DialogResult.OK) txtCsvPath.Text = dialog.FileName;
            }
        }

        private void BrowseGel()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "GEL 脚本 (*.gel)|*.gel|所有文件 (*.*)|*.*";
                dialog.FileName = SafeFileName(txtGelPath.Text, "data_logger.gel");
                dialog.InitialDirectory = SafeDirectory(txtGelPath.Text);
                if (dialog.ShowDialog(this) == DialogResult.OK) txtGelPath.Text = dialog.FileName;
            }
        }

        private static string SafeFileName(string path, string fallback)
        {
            try { return String.IsNullOrWhiteSpace(path) ? fallback : Path.GetFileName(path); }
            catch { return fallback; }
        }

        private static string SafeDirectory(string path)
        {
            try
            {
                var value = Path.GetDirectoryName(path);
                return Directory.Exists(value) ? value : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            }
            catch { return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); }
        }

        private void AddCsvFiles()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*";
                dialog.Multiselect = true;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                foreach (string path in dialog.FileNames)
                {
                    bool exists = false;
                    foreach (object item in csvFileList.Items)
                    {
                        if (String.Equals(Convert.ToString(item), path, StringComparison.OrdinalIgnoreCase))
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists) csvFileList.Items.Add(path);
                }
                UpdateConvertStatus("已选择 " + csvFileList.Items.Count + " 个 CSV 文件。");
            }
        }

        private void RemoveSelectedCsvFiles()
        {
            while (csvFileList.SelectedIndices.Count > 0)
                csvFileList.Items.RemoveAt(csvFileList.SelectedIndices[0]);
            UpdateConvertStatus("当前列表包含 " + csvFileList.Items.Count + " 个文件。");
        }

        private void BrowseXlsxDirectory()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择 XLSX 输出目录";
                dialog.SelectedPath = Directory.Exists(txtXlsxDirectory.Text)
                    ? txtXlsxDirectory.Text
                    : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (dialog.ShowDialog(this) == DialogResult.OK) txtXlsxDirectory.Text = dialog.SelectedPath;
            }
        }

        private void ConvertCsvFiles()
        {
            if (csvFileList.Items.Count == 0)
            {
                MessageBox.Show(this, "请先添加至少一个 CSV 文件。", "没有输入文件", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!chkSameDirectory.Checked && !Directory.Exists(txtXlsxDirectory.Text))
            {
                MessageBox.Show(this, "自定义 XLSX 输出目录不存在。", "输出目录无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int success = 0;
            int skipped = 0;
            var errors = new List<string>();
            foreach (object item in csvFileList.Items)
            {
                string csvPath = Convert.ToString(item);
                try
                {
                    if (!File.Exists(csvPath)) throw new FileNotFoundException("CSV 文件不存在。", csvPath);
                    string outputDir = chkSameDirectory.Checked ? Path.GetDirectoryName(csvPath) : txtXlsxDirectory.Text;
                    string xlsxPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(csvPath) + ".xlsx");
                    if (File.Exists(xlsxPath) && !chkOverwriteXlsx.Checked)
                    {
                        DialogResult answer = MessageBox.Show(
                            this,
                            "文件已存在：\n" + xlsxPath + "\n\n是否覆盖？选择“否”将跳过该文件。",
                            "确认覆盖",
                            MessageBoxButtons.YesNoCancel,
                            MessageBoxIcon.Question);
                        if (answer == DialogResult.Cancel) break;
                        if (answer == DialogResult.No) { skipped++; continue; }
                    }
                    CcsCsvConverter.ConvertFile(csvPath, xlsxPath);
                    success++;
                }
                catch (Exception ex)
                {
                    errors.Add(Path.GetFileName(csvPath) + "：" + ex.Message);
                }
            }

            string summary = "转换完成：成功 " + success + "，跳过 " + skipped + "，失败 " + errors.Count + "。";
            UpdateConvertStatus(summary);
            if (errors.Count > 0)
                MessageBox.Show(this, summary + "\n\n" + String.Join("\n", errors.ToArray()), "转换结果", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
                MessageBox.Show(this, summary, "转换结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateConvertStatus(string text)
        {
            lblConvertStatus.Text = text;
        }

        private void SyncBridgePaths()
        {
            txtBridgeCsvPath.Text = txtCsvPath.Text;
            txtBridgeScriptPath.Text = BridgeScriptPath(txtGelPath.Text);
        }

        internal static string BridgeScriptPath(string gelPath)
        {
            if (String.IsNullOrWhiteSpace(gelPath)) return String.Empty;
            try
            {
                string dir = Path.GetDirectoryName(gelPath) ?? String.Empty;
                return Path.Combine(dir, Path.GetFileNameWithoutExtension(gelPath) + "_vofa_bridge.ps1");
            }
            catch { return String.Empty; }
        }

        private void ApplyVofaEnabledState(bool userChanged)
        {
            bool enabled = chkVofaEnabled.Checked;
            if (!enabled || userChanged)
            {
                foreach (DataGridViewRow row in grid.Rows)
                    if (!row.IsNewRow) row.Cells["VofaColumn"].Value = enabled;
            }
            grid.Columns["VofaColumn"].ReadOnly = !enabled;
            foreach (DataGridViewRow row in grid.Rows) if (!row.IsNewRow) ApplyDirectionState(row.Index);
            txtVofaAddress.Enabled = enabled;
            numVofaPort.Enabled = enabled;
            cmbCcsCorePattern.Enabled = enabled;
            radFollowNew.Enabled = enabled;
            radReplay.Enabled = enabled;
            chkVofaWriteEnabled.Enabled = enabled;
            if (!enabled) chkVofaWriteEnabled.Checked = false;
            btnBridgeStart.Enabled = enabled && bridgeService == null;
            if (!enabled && bridgeService != null) StopBridge();
            lblBridgeStatus.Text = enabled ? "状态：已启用，等待启动。" : "状态：未启用 VOFA+ 桥接；请先在 GEL 脚本生成页启用。";
            RefreshBridgeChannels();
        }

        private int AdapterPort()
        {
            int wave = Decimal.ToInt32(numVofaPort.Value);
            for (int offset = 2; offset <= 65535; offset++)
            {
                int value = ((wave - 1 + offset) % 65535) + 1;
                if (value != wave) return value;
            }
            throw new InvalidOperationException("无法分配 CCS 适配器端口。");
        }

        private void UpdateAdapterPort()
        {
            try { txtCcsAdapterPort.Text = AdapterPort().ToString(CultureInfo.InvariantCulture); }
            catch { txtCcsAdapterPort.Text = String.Empty; }
        }

        private void RefreshBridgeChannels()
        {
            var names = new List<string> { "sample" };
            var inputs = new List<string>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                bool selected = false;
                try { selected = Convert.ToBoolean(row.Cells["VofaColumn"].Value ?? false, CultureInfo.InvariantCulture); }
                catch { }
                string name = CellText(row, "NameColumn");
                string direction = CellText(row, "DirectionColumn");
                if (selected && direction != "input" && !String.IsNullOrWhiteSpace(name)) names.Add(name);
                if ((direction == "input" || direction == "inout") && !String.IsNullOrWhiteSpace(name)) inputs.Add(name);
            }
            txtBridgeChannels.Text = "输出通道顺序：" + Environment.NewLine + String.Join(Environment.NewLine, names.ToArray()) +
                Environment.NewLine + Environment.NewLine + "可写入变量（SET,名称,数值）：" + Environment.NewLine +
                (inputs.Count == 0 ? "（无）" : String.Join(Environment.NewLine, inputs.ToArray()));
        }

        private void ApplyDirectionState(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= grid.Rows.Count) return;
            DataGridViewRow row = grid.Rows[rowIndex];
            string direction = CellText(row, "DirectionColumn");
            bool output = direction != "input";
            row.Cells["FormatColumn"].ReadOnly = false;
            row.Cells["VofaColumn"].ReadOnly = !output || !chkVofaEnabled.Checked;
            if (!output) row.Cells["VofaColumn"].Value = false;
            if (CellText(row, "FormatColumn") == "无" || String.IsNullOrWhiteSpace(CellText(row, "FormatColumn")))
                row.Cells["FormatColumn"].Value = "十进制";
        }

        private void CopyChannelNames(IList<VariableItem> variables)
        {
            var names = new List<string> { "sample" };
            foreach (VariableItem item in variables) if (item.SendToVofa && item.Direction != "input") names.Add(item.Name);
            try { Clipboard.SetText(String.Join(Environment.NewLine, names.ToArray())); }
            catch (Exception ex) { lblBridgeError.Text = "最近错误：通道名称复制失败：" + ex.Message; }
        }

        private BridgeOptions CurrentBridgeOptions(bool validate)
        {
            List<VariableItem> variables = ReadVariables(validate);
            var selected = new List<int>();
            var hexColumns = new bool[variables.Count + 1];
            int csvIndex = 1;
            foreach (VariableItem item in variables)
            {
                if (item.Direction == "input") continue;
                if (item.SendToVofa) selected.Add(csvIndex);
                hexColumns[csvIndex] = item.OutputFormat == "十六进制";
                csvIndex++;
            }
            VariableItem[] writable = variables.FindAll(delegate(VariableItem v) { return v.Direction == "input" || v.Direction == "inout"; }).ToArray();
            if (validate && !chkVofaEnabled.Checked) throw new InvalidOperationException("请先启用 VOFA+ 桥接。");
            if (validate && selected.Count == 0 && writable.Length == 0) throw new InvalidOperationException("请至少选择一个输出通道或配置一个 input/inout 变量。");
            IPAddress address;
            if (!IPAddress.TryParse(txtVofaAddress.Text.Trim(), out address))
                throw new InvalidOperationException("监听地址不是有效 IP 地址。");
            if (String.IsNullOrWhiteSpace(cmbCcsCorePattern.Text)) throw new InvalidOperationException("CPU核匹配不能为空。");
            int wavePort = Decimal.ToInt32(numVofaPort.Value);
            return new BridgeOptions
            {
                CsvPath = txtCsvPath.Text,
                Address = address,
                Port = wavePort,
                ReplayExisting = radReplay.Checked,
                PeriodMs = PeriodMilliseconds(),
                ExpectedColumns = csvIndex,
                SelectedColumns = selected.ToArray(),
                HexColumns = hexColumns,
                ChannelNames = ChannelNames(variables),
                ControlPort = AdapterPort(),
                WriteEnabled = chkVofaWriteEnabled.Checked,
                WritableVariables = writable
                ,CorePattern = cmbCcsCorePattern.Text.Trim()
            };
        }

        private static string[] ChannelNames(IList<VariableItem> variables)
        {
            var names = new List<string> { "sample" };
            foreach (VariableItem item in variables) if (item.SendToVofa && item.Direction != "input") names.Add(item.Name);
            return names.ToArray();
        }

        private void StartBridge()
        {
            try
            {
                BridgeOptions options = CurrentBridgeOptions(true);
                bridgeService = new VofaBridgeService(options, BridgeStatusChanged);
                bridgeService.Start();
                btnBridgeStart.Enabled = false;
                btnBridgeStop.Enabled = true;
                CopyChannelNames(ReadVariables(true));
                lblBridgeStatus.Text = "状态：桥接运行中；通道名称已复制。";
            }
            catch (Exception ex)
            {
                if (bridgeService != null) { bridgeService.Dispose(); bridgeService = null; }
                lblBridgeError.Text = "最近错误：" + ex.Message;
                MessageBox.Show(this, ex.Message, "无法启动桥接", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void StopBridge()
        {
            if (bridgeService != null) { bridgeService.Dispose(); bridgeService = null; }
            btnBridgeStop.Enabled = false;
            btnBridgeStart.Enabled = chkVofaEnabled.Checked;
            lblBridgeStatus.Text = "状态：桥接已停止。";
            lblBridgeConnection.Text = "VOFA+ 波形连接：未监听";
            lblAdapterConnection.Text = "CCS 写入适配器：未连接";
            lblTargetSession.Text = "CCS 目标会话：未就绪";
        }

        private void BridgeStatusChanged(BridgeStatus status)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action<BridgeStatus>(BridgeStatusChanged), status); return; }
            lblBridgeConnection.Text = "VOFA+ 波形连接：" + (status.ClientConnected ? "已连接" : "等待连接");
            lblAdapterConnection.Text = "CCS 写入适配器：" + (status.AdapterConnected ? "已连接" : "等待连接");
            lblTargetSession.Text = "CCS 目标会话：" + (String.IsNullOrWhiteSpace(status.TargetSessionStatus) ? "未就绪" : status.TargetSessionStatus);
            lblBridgeCounters.Text = "已发送：" + status.Sent + "；跳过：" + status.Skipped + "；写入成功：" + status.Writes + "；写入失败：" + status.WriteFailures;
            lblBridgeCommand.Text = "最近命令：" + (String.IsNullOrWhiteSpace(status.LastCommand) ? "无" : status.LastCommand + " → " + status.LastResponse);
            lblBridgeError.Text = "最近错误：" + (String.IsNullOrWhiteSpace(status.LastError) ? "无" : status.LastError);
            lblBridgeError.ForeColor = String.IsNullOrWhiteSpace(status.LastError) ? SystemColors.ControlText : Color.DarkRed;
            if (status.ColumnMismatch) lblBridgeStatus.Text = "状态：检测到列数变化，已暂停发送。";
        }

        private void CopyBridgeStatus()
        {
            string value = String.Join(Environment.NewLine, new[]
            {
                lblBridgeStatus.Text, lblBridgeConnection.Text,
                lblAdapterConnection.Text, lblTargetSession.Text, lblBridgeCommand.Text, lblBridgeCounters.Text, lblBridgeError.Text
            });
            try { Clipboard.SetText(value); statusLabel.Text = "桥接状态已复制到剪贴板。"; }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "复制状态失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (bridgeService != null && MessageBox.Show(this, "VOFA+ 桥接正在运行。关闭程序将停止桥接，是否继续？", "确认关闭", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            { e.Cancel = true; return; }
            StopBridge();
            base.OnFormClosing(e);
        }

        private void AddRow()
        {
            int index = grid.Rows.Add();
            grid.Rows[index].Cells["DirectionColumn"].Value = "output";
            grid.Rows[index].Cells["TypeColumn"].Value = "float32";
            grid.Rows[index].Cells["FormatColumn"].Value = "十进制";
            grid.Rows[index].Cells["VofaColumn"].Value = chkVofaEnabled.Checked;
            grid.CurrentCell = grid.Rows[index].Cells["NameColumn"];
            grid.BeginEdit(true);
        }

        private void DeleteSelectedRows()
        {
            var rows = new List<DataGridViewRow>();
            foreach (DataGridViewRow row in grid.SelectedRows) if (!row.IsNewRow) rows.Add(row);
            foreach (DataGridViewRow row in rows) grid.Rows.Remove(row);
        }

        private void MoveSelected(int direction)
        {
            if (grid.SelectedRows.Count != 1 || grid.SelectedRows[0].IsNewRow) return;
            var row = grid.SelectedRows[0];
            int oldIndex = row.Index;
            int newIndex = oldIndex + direction;
            if (newIndex < 0 || newIndex >= grid.Rows.Count - 1) return;
            var values = new object[grid.Columns.Count];
            for (int i = 0; i < values.Length; i++) values[i] = row.Cells[i].Value;
            grid.Rows.RemoveAt(oldIndex);
            grid.Rows.Insert(newIndex, values);
            grid.Rows[newIndex].Selected = true;
            grid.CurrentCell = grid.Rows[newIndex].Cells[0];
        }

        private void ClearRows()
        {
            if (grid.Rows.Count <= 1) return;
            if (MessageBox.Show(this, "确认清空全部变量？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                grid.Rows.Clear();
        }

        private List<VariableItem> ReadVariables(bool validate)
        {
            grid.EndEdit();
            var list = new List<VariableItem>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int logicalRow = 0;
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                logicalRow++;
                string direction = CellText(row, "DirectionColumn");
                if (String.IsNullOrWhiteSpace(direction)) direction = "output";
                string name = CellText(row, "NameColumn");
                string expression = CellText(row, "ExpressionColumn");
                string type = CellText(row, "TypeColumn");
                string outputFormat = CellText(row, "FormatColumn");
                bool sendToVofa = Convert.ToBoolean(row.Cells["VofaColumn"].Value ?? false, CultureInfo.InvariantCulture);
                if (validate)
                {
                    if (String.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("第 " + logicalRow + " 行的 CSV 列名称为空。");
                    if (String.IsNullOrWhiteSpace(expression)) throw new InvalidOperationException("第 " + logicalRow + " 行的变量表达式为空。");
                    if (name.IndexOfAny(new[] { ',', '\r', '\n', '"' }) >= 0) throw new InvalidOperationException("CSV 列名称不能包含逗号、引号或换行：" + name);
                    if (!names.Add(name)) throw new InvalidOperationException("CSV 列名称重复：" + name);
                    if (Array.IndexOf(GelTypes, type) < 0) throw new InvalidOperationException("第 " + logicalRow + " 行的变量类型无效。");
                    if (Array.IndexOf(Directions, direction) < 0) throw new InvalidOperationException("第 " + logicalRow + " 行的方向无效。");
                    GelTemplate.ValidateTypeAndFormat(type, outputFormat);
                }
                list.Add(new VariableItem { Direction = direction, Name = name, Expression = expression, Type = type, OutputFormat = outputFormat, SendToVofa = sendToVofa && direction != "input" });
            }
            if (validate && list.Count == 0) throw new InvalidOperationException("请至少添加一个变量。");
            return list;
        }

        private static string CellText(DataGridViewRow row, string name)
        {
            object value = row.Cells[name].Value;
            return value == null ? String.Empty : Convert.ToString(value, CultureInfo.InvariantCulture).Trim();
        }

        private int PeriodMilliseconds()
        {
            decimal multiplier = cmbPeriodUnit.Text == "s" ? 1000M : 1M;
            decimal result = numPeriod.Value * multiplier;
            if (result < 1 || result > Int32.MaxValue || Decimal.Truncate(result) != result)
                throw new InvalidOperationException("采集周期换算为毫秒后必须是 1 到 2147483647 之间的整数。");
            return Decimal.ToInt32(result);
        }

        private void ValidatePaths()
        {
            if (String.IsNullOrWhiteSpace(txtCsvPath.Text)) throw new InvalidOperationException("CSV 保存路径不能为空。");
            if (String.IsNullOrWhiteSpace(txtGelPath.Text)) throw new InvalidOperationException("GEL 输出路径不能为空。");
            if (!String.Equals(Path.GetExtension(txtGelPath.Text), ".gel", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("GEL 输出文件必须使用 .gel 扩展名。");
            string gelDir = Path.GetDirectoryName(Path.GetFullPath(txtGelPath.Text));
            string csvDir = Path.GetDirectoryName(Path.GetFullPath(txtCsvPath.Text));
            if (!Directory.Exists(gelDir)) throw new InvalidOperationException("GEL 输出目录不存在：" + gelDir);
            if (!Directory.Exists(csvDir)) throw new InvalidOperationException("CSV 输出目录不存在：" + csvDir);
        }

        private void GenerateGel()
        {
            try
            {
                ValidatePaths();
                List<VariableItem> variables = ReadVariables(true);
                int period = PeriodMilliseconds();
                BridgeOptions bridgeOptions = chkVofaEnabled.Checked ? CurrentBridgeOptions(true) : null;
                List<VariableItem> outputVariables = variables.FindAll(delegate(VariableItem v) { return v.Direction != "input"; });
                List<VariableItem> writableVariables = variables.FindAll(delegate(VariableItem v) { return v.Direction == "input" || v.Direction == "inout"; });
                string script = GelTemplate.Build(txtCsvPath.Text, period, outputVariables, cmbCcsVersion.Text, writableVariables);
                GelTemplate.ValidateGeneratedScript(script, outputVariables.Count);
                string commandsPath = null;
                string commandsJson = null;
                if (chkGenerateVofaCommands.Checked)
                {
                    if (writableVariables.Count == 0)
                        throw new InvalidOperationException("已选择生成 VOFA+ 命令配置文件，但变量表中没有 input 或 inout 变量。");
                    commandsPath = VofaCommandConfigTemplate.OutputPath(txtGelPath.Text);
                    commandsJson = VofaCommandConfigTemplate.Build(Path.GetFileNameWithoutExtension(txtGelPath.Text), writableVariables);
                }
                File.WriteAllText(txtGelPath.Text, script, new UTF8Encoding(false));
                string extra = String.Empty;
                if (chkGenerateVofaCommands.Checked)
                {
                    File.WriteAllText(commandsPath, commandsJson, new UTF8Encoding(false));
                    extra += "\nVOFA+ 命令配置：\n" + commandsPath;
                }
                if (chkVofaEnabled.Checked)
                {
                    string bridgePath = BridgeScriptPath(txtGelPath.Text);
                    File.WriteAllText(bridgePath, BridgePowerShellTemplate.Build(bridgeOptions), new UTF8Encoding(true));
                    if (bridgeOptions.WritableVariables.Length > 0)
                    {
                        string adapterPath = Path.Combine(Path.GetDirectoryName(txtGelPath.Text), Path.GetFileNameWithoutExtension(txtGelPath.Text) + "_ccs_input_bridge.js");
                        File.WriteAllText(adapterPath, CcsInputAdapterTemplate.Build(cmbCcsVersion.Text, bridgeOptions), new UTF8Encoding(false));
                        extra += "\nCCS 输入适配器：\n" + adapterPath;
                    }
                    CopyChannelNames(variables);
                    extra = "\nVOFA+ 桥接脚本：\n" + bridgePath + extra + "\n\n通道名称已复制到剪贴板。";
                }
                statusLabel.Text = "生成成功：" + txtGelPath.Text;
                MessageBox.Show(this, "GEL 文件生成成功：\n" + txtGelPath.Text + extra, "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                statusLabel.Text = "生成失败：" + ex.Message;
                MessageBox.Show(this, ex.Message, "无法生成 GEL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private GeneratorConfig CurrentConfig(bool validate)
        {
            if (validate) ValidatePaths();
            return new GeneratorConfig
            {
                CsvPath = txtCsvPath.Text,
                PeriodValue = numPeriod.Value,
                PeriodUnit = cmbPeriodUnit.Text,
                GelPath = txtGelPath.Text,
                Variables = ReadVariables(validate),
                VofaEnabled = chkVofaEnabled.Checked,
                VofaAddress = txtVofaAddress.Text,
                VofaPort = Decimal.ToInt32(numVofaPort.Value),
                VofaReplayExisting = radReplay.Checked
                ,CcsVersion = cmbCcsVersion.Text
                ,VofaWriteEnabled = chkVofaWriteEnabled.Checked
                ,CcsCorePattern = cmbCcsCorePattern.Text
                ,GenerateVofaCommandConfig = chkGenerateVofaCommands.Checked
            };
        }

        private void SaveConfig()
        {
            try
            {
                GeneratorConfig config = CurrentConfig(false);
                using (var dialog = new SaveFileDialog { Filter = "GEL 生成器配置 (*.json)|*.json", FileName = "gel_generator_config.json" })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    string json = new JavaScriptSerializer().Serialize(config);
                    File.WriteAllText(dialog.FileName, json, new UTF8Encoding(false));
                    statusLabel.Text = "配置已保存：" + dialog.FileName;
                }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "保存配置失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void LoadConfig()
        {
            try
            {
                using (var dialog = new OpenFileDialog { Filter = "GEL 生成器配置 (*.json)|*.json|所有文件 (*.*)|*.*" })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    var config = new JavaScriptSerializer().Deserialize<GeneratorConfig>(File.ReadAllText(dialog.FileName, Encoding.UTF8));
                    if (config == null) throw new InvalidOperationException("配置文件内容为空或格式不正确。");
                    txtCsvPath.Text = config.CsvPath ?? String.Empty;
                    txtGelPath.Text = config.GelPath ?? String.Empty;
                    decimal value = config.PeriodValue;
                    if (value < numPeriod.Minimum) value = numPeriod.Minimum;
                    if (value > numPeriod.Maximum) value = numPeriod.Maximum;
                    numPeriod.Value = value;
                    cmbPeriodUnit.SelectedItem = config.PeriodUnit == "s" ? "s" : "ms";
                    txtVofaAddress.Text = String.IsNullOrWhiteSpace(config.VofaAddress) ? "127.0.0.1" : config.VofaAddress;
                    numVofaPort.Value = config.VofaPort >= 1 && config.VofaPort <= 65535 ? config.VofaPort : 1347;
                    radReplay.Checked = config.VofaReplayExisting;
                    radFollowNew.Checked = !config.VofaReplayExisting;
                    chkVofaEnabled.Checked = config.VofaEnabled;
                    chkGenerateVofaCommands.Checked = config.GenerateVofaCommandConfig;
                    cmbCcsVersion.SelectedItem = Array.IndexOf(CcsVersions, config.CcsVersion) >= 0 ? config.CcsVersion : "CCS 12.7.1";
                    chkVofaWriteEnabled.Checked = config.VofaEnabled && config.VofaWriteEnabled;
                    string core = Array.IndexOf(new[] { "C28xx_CPU1", "C28xx_CPU2", "CPU1", "CPU2" }, config.CcsCorePattern) >= 0 ? config.CcsCorePattern : "C28xx_CPU1";
                    cmbCcsCorePattern.SelectedItem = core;
                    grid.Rows.Clear();
                    if (config.Variables != null)
                    {
                        foreach (VariableItem item in config.Variables)
                        {
                            VariableItem migrated = GelTemplate.MigrateLegacy(item);
                            grid.Rows.Add(migrated.Direction, migrated.Name, migrated.Expression, migrated.Type, migrated.OutputFormat, config.VofaEnabled && item.SendToVofa);
                        }
                    }
                    ApplyVofaEnabledState(false);
                    statusLabel.Text = "配置已打开：" + dialog.FileName;
                }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "打开配置失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }

    internal sealed class BridgeOptions
    {
        public string CsvPath;
        public IPAddress Address;
        public int Port;
        public bool ReplayExisting;
        public int PeriodMs;
        public int ExpectedColumns;
        public int[] SelectedColumns;
        public bool[] HexColumns;
        public string[] ChannelNames;
        public int ControlPort;
        public bool WriteEnabled;
        public VariableItem[] WritableVariables;
        public string CorePattern;
    }

    internal sealed class BridgeStatus
    {
        public bool ClientConnected;
        public long Sent;
        public long Skipped;
        public string LastError;
        public bool ColumnMismatch;
        public bool AdapterConnected;
        public long Writes;
        public long WriteFailures;
        public string LastCommand;
        public string LastResponse;
        public string TargetSessionStatus;
    }

    internal static class VofaCommandConfigTemplate
    {
        public static string OutputPath(string gelPath)
        {
            return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(gelPath)),
                Path.GetFileNameWithoutExtension(gelPath) + "_vofa.cmds.json");
        }

        public static string Build(string gelName, IList<VariableItem> writableVariables)
        {
            if (writableVariables == null || writableVariables.Count == 0)
                throw new InvalidOperationException("VOFA+ 命令配置至少需要一个 input 或 inout 变量。");

            var input = new List<object>();
            var inout = new List<object>();
            for (int i = 0; i < writableVariables.Count; i++)
            {
                VariableItem item = GelTemplate.MigrateLegacy(writableVariables[i]);
                if (item.Direction != "input" && item.Direction != "inout") continue;
                GelTemplate.ValidateTypeAndFormat(item.Type, item.OutputFormat);
                if (String.IsNullOrWhiteSpace(item.Name)) throw new InvalidOperationException("VOFA+ 命令名称不能为空。");
                object command = Command(item);
                if (item.Direction == "input") input.Add(command); else inout.Add(command);
            }
            if (input.Count == 0 && inout.Count == 0)
                throw new InvalidOperationException("VOFA+ 命令配置中没有可写变量。");

            var groups = new List<object>();
            if (input.Count > 0) groups.Add(Group("input 输入变量", input));
            if (inout.Count > 0) groups.Add(Group("inout 双向变量", inout));
            var root = new Dictionary<string, object>
            {
                { "type", "cmds" },
                { "vnumber", 100 },
                { "ctx", Group(String.IsNullOrWhiteSpace(gelName) ? "GelGenerator" : gelName, groups) }
            };
            return new JavaScriptSerializer().Serialize(root);
        }

        private static object Command(VariableItem item)
        {
            string placeholder = Placeholder(item.Type);
            string text = "SET," + item.Name + "," + placeholder + "\n";
            string intro = item.Direction + "；" + item.Type + "；数值格式=" + item.OutputFormat + "。";
            if (item.OutputFormat == "十六进制")
                intro += "VOFA+ Str 参数是 Float 模式，动态命令按十进制占位符发送；需要十六进制字面量时请直接发送 SET," + item.Name + ",0x...。";
            if (item.Type == "uint32")
                intro += "动态参数超过 16777216 时可能受 Float 有效精度限制。";
            return new Dictionary<string, object>
            {
                { "name", item.Name }, { "intro_on", true }, { "intro", intro },
                { "hex_on", false }, { "loop_on", true }, { "loop_ms", 100 }, { "loop_count", 1 },
                { "cmd_hex", AsciiHex(text) }, { "is_group", false }, { "down", false },
                { "subCmds", new object[0] }
            };
        }

        private static object Group(string name, IList<object> children)
        {
            return new Dictionary<string, object>
            {
                { "name", name }, { "intro_on", true }, { "intro", "GelGenerator 自动生成" },
                { "hex_on", false }, { "loop_on", true }, { "loop_ms", 100 }, { "loop_count", 1 },
                { "cmd_hex", String.Empty }, { "is_group", true }, { "down", true },
                { "subCmds", children }
            };
        }

        private static string Placeholder(string type)
        {
            if (type == "float32" || type == "float64") return "%f";
            if (type == "uint8" || type == "uint16" || type == "uint32" || type == "bool") return "%u";
            return "%d";
        }

        private static string AsciiHex(string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            var parts = new string[bytes.Length];
            for (int i = 0; i < bytes.Length; i++) parts[i] = bytes[i].ToString("X2", CultureInfo.InvariantCulture);
            return String.Join(" ", parts);
        }
    }

    internal sealed class VofaBridgeService : IDisposable
    {
        private readonly BridgeOptions options;
        private readonly Action<BridgeStatus> report;
        private readonly ManualResetEvent stopEvent = new ManualResetEvent(false);
        private readonly object sync = new object();
        private TcpListener listener;
        private TcpClient client;
        private TcpListener controlListener;
        private TcpClient adapterClient;
        private readonly StringBuilder vofaCommands = new StringBuilder();
        private Thread worker;
        private long sent;
        private long skipped;
        private string lastError;
        private bool columnMismatch;
        private long writes;
        private long writeFailures;
        private string lastCommand;
        private string lastResponse;
        private string targetSessionStatus;
        private readonly StringBuilder adapterMessages = new StringBuilder();

        public VofaBridgeService(BridgeOptions options, Action<BridgeStatus> report)
        {
            this.options = options;
            this.report = report;
        }

        public void Start()
        {
            listener = new TcpListener(options.Address, options.Port);
            listener.Start(1);
            controlListener = new TcpListener(IPAddress.Loopback, options.ControlPort);
            controlListener.Start(1);
            worker = new Thread(Run) { IsBackground = true, Name = "VOFA CSV Bridge" };
            worker.Start();
            Publish();
        }

        public void SetWriteEnabled(bool enabled)
        {
            options.WriteEnabled = enabled;
            Publish();
        }

        public int BoundPort { get { return listener == null ? 0 : ((IPEndPoint)listener.LocalEndpoint).Port; } }
        public int BoundControlPort { get { return controlListener == null ? 0 : ((IPEndPoint)controlListener.LocalEndpoint).Port; } }

        private void Run()
        {
            long position = 0;
            long replayBoundary = 0;
            bool initialized = false;
            var pending = new List<byte>();
            DateTime lastWrite = DateTime.MinValue;
            while (!stopEvent.WaitOne(100))
            {
                try
                {
                    AcceptClientIfAvailable();
                    AcceptAdapterIfAvailable();
                    PollAdapterMessages();
                    PollVofaCommands();
                    if (options.ReplayExisting && !initialized && !HasClient()) continue;
                    if (!File.Exists(options.CsvPath)) { SetError("等待 CSV 文件出现：" + options.CsvPath, false); continue; }
                    using (var stream = new FileStream(options.CsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        DateTime write = File.GetLastWriteTimeUtc(options.CsvPath);
                        if (!initialized)
                        {
                            replayBoundary = stream.Length;
                            position = options.ReplayExisting ? 0 : stream.Length;
                            initialized = true;
                            lastWrite = write;
                            SetError(null, false);
                        }
                        else if (stream.Length < position || (write != lastWrite && stream.Length <= position))
                        {
                            position = 0; pending.Clear(); columnMismatch = false; replayBoundary = 0;
                        }
                        lastWrite = write;
                        if (stream.Length > position)
                        {
                            stream.Position = position;
                            byte[] buffer = new byte[Math.Min(65536, (int)Math.Min(Int32.MaxValue, stream.Length - position))];
                            int count;
                            while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                long blockStart = position;
                                position += count;
                                for (int i = 0; i < count; i++)
                                {
                                    byte value = buffer[i];
                                    if (value == 10)
                                    {
                                        if (pending.Count > 0 && pending[pending.Count - 1] == 13) pending.RemoveAt(pending.Count - 1);
                                        string line = DecodeLine(pending.ToArray());
                                        pending.Clear();
                                        bool historical = options.ReplayExisting && (blockStart + i + 1) <= replayBoundary;
                                        ProcessLine(line, historical);
                                    }
                                    else pending.Add(value);
                                }
                                if (stopEvent.WaitOne(0)) return;
                            }
                        }
                    }
                }
                catch (IOException ex) { SetError("CSV 暂时不可读：" + ex.Message, false); }
                catch (UnauthorizedAccessException ex) { SetError("CSV 访问被拒绝：" + ex.Message, false); }
                catch (Exception ex) { SetError(ex.Message, false); }
            }
        }

        private void AcceptClientIfAvailable()
        {
            if (listener == null || !listener.Pending()) return;
            TcpClient next = listener.AcceptTcpClient();
            next.NoDelay = true;
            lock (sync)
            {
                if (client != null) try { client.Close(); } catch { }
                client = next;
            }
            Publish();
        }

        private bool HasClient()
        {
            lock (sync) return client != null && client.Connected;
        }

        private void AcceptAdapterIfAvailable()
        {
            if (controlListener == null || !controlListener.Pending()) return;
            TcpClient next = controlListener.AcceptTcpClient(); next.NoDelay = true;
            lock (sync)
            {
                if (adapterClient != null) try { adapterClient.Close(); } catch { }
                adapterClient = next;
                adapterMessages.Length = 0;
                targetSessionStatus = "正在初始化";
            }
            Publish();
        }

        private void PollAdapterMessages()
        {
            TcpClient adapter; lock (sync) adapter = adapterClient;
            if (adapter == null || !adapter.Connected) return;
            NetworkStream stream = adapter.GetStream();
            if (!stream.DataAvailable) return;
            byte[] buffer = new byte[2048]; int count = stream.Read(buffer, 0, buffer.Length);
            if (count <= 0) return;
            adapterMessages.Append(Encoding.UTF8.GetString(buffer, 0, count));
            string line;
            while (TryTakeAdapterLine(out line)) HandleAdapterStatus(line);
        }

        private bool TryTakeAdapterLine(out string line)
        {
            string text = adapterMessages.ToString(); int newline = text.IndexOf('\n');
            if (newline < 0) { line = null; return false; }
            line = text.Substring(0, newline).TrimEnd('\r').Trim();
            adapterMessages.Remove(0, newline + 1); return true;
        }

        private bool HandleAdapterStatus(string line)
        {
            if (!line.StartsWith("STATUS,", StringComparison.OrdinalIgnoreCase)) return false;
            string[] parts = line.Split(new[] { ',' }, 3);
            if (parts.Length >= 3 && String.Equals(parts[1], "READY", StringComparison.OrdinalIgnoreCase)) targetSessionStatus = parts[2] + "，已就绪";
            else if (parts.Length >= 3) targetSessionStatus = parts[2];
            else targetSessionStatus = line;
            Publish(); return true;
        }

        private void PollVofaCommands()
        {
            TcpClient current;
            lock (sync) current = client;
            if (current == null || !current.Connected || !current.GetStream().DataAvailable) return;
            byte[] buffer = new byte[4096];
            int count = current.GetStream().Read(buffer, 0, buffer.Length);
            if (count <= 0) return;
            vofaCommands.Append(Encoding.ASCII.GetString(buffer, 0, count));
            while (true)
            {
                string all = vofaCommands.ToString(); int newline = all.IndexOf('\n');
                if (newline < 0) break;
                string command = all.Substring(0, newline).TrimEnd('\r').Trim();
                vofaCommands.Remove(0, newline + 1);
                if (command.Length > 0) HandleWriteCommand(command, current);
            }
        }

        private void HandleWriteCommand(string command, TcpClient vofa)
        {
            int setStart = command.IndexOf("SET,", StringComparison.OrdinalIgnoreCase);
            if (setStart > 0) command = command.Substring(setStart);
            string[] parts = command.Split(',');
            if (parts.Length != 3 || !String.Equals(parts[0].Trim(), "SET", StringComparison.OrdinalIgnoreCase))
            { CompleteWrite(vofa, command, "ERR,COMMAND,EXPECTED_SET_NAME_VALUE", false); return; }
            string name = parts[1].Trim(); string value = parts[2].Trim();
            if (!options.WriteEnabled) { CompleteWrite(vofa, command, "ERR," + name + ",WRITE_DISABLED", false); return; }
            VariableItem item = null;
            foreach (VariableItem candidate in options.WritableVariables ?? new VariableItem[0])
                if (String.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)) { item = candidate; break; }
            if (item == null) { CompleteWrite(vofa, command, "ERR," + name + ",UNKNOWN_VARIABLE", false); return; }
            string normalized;
            if (!InputValueValidator.TryNormalize(item.Type, value, out normalized))
            { CompleteWrite(vofa, command, "ERR," + name + ",INVALID_VALUE", false); return; }
            TcpClient adapter;
            lock (sync) adapter = adapterClient;
            if (adapter == null || !adapter.Connected) { CompleteWrite(vofa, command, "ERR," + name + ",CCS_NOT_CONNECTED", false); return; }
            try
            {
                NetworkStream stream = adapter.GetStream();
                byte[] request = Encoding.UTF8.GetBytes("SET," + name + "," + normalized + "\n");
                stream.Write(request, 0, request.Length); stream.ReadTimeout = 3000;
                string reply = null;
                while (reply == null)
                {
                    string line;
                    if (!TryTakeAdapterLine(out line))
                    {
                        int ch = stream.ReadByte(); if (ch < 0) throw new IOException("CCS adapter closed.");
                        adapterMessages.Append((char)ch); continue;
                    }
                    if (!HandleAdapterStatus(line)) reply = line;
                }
                CompleteWrite(vofa, command, reply, reply.StartsWith("ACK,", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                lock (sync) { if (adapterClient != null) try { adapterClient.Close(); } catch { } adapterClient = null; }
                CompleteWrite(vofa, command, "ERR," + name + ",CCS_IO_ERROR", false); SetError("CCS 写入失败：" + ex.Message, false);
            }
        }

        private void CompleteWrite(TcpClient target, string command, string response, bool success)
        {
            lastCommand = command; lastResponse = response;
            if (success) writes++; else writeFailures++;
            Publish();
        }

        private void ProcessLine(string line, bool historical)
        {
            string frame, error;
            bool header, mismatch;
            if (!CcsCsvConverter.TryBuildFireWaterFrame(line, options.ExpectedColumns, options.SelectedColumns, options.HexColumns, out frame, out header, out mismatch, out error))
            {
                if (header)
                {
                    if (mismatch) { columnMismatch = true; SetError("CSV 表头列数与当前 GEL 配置不一致，已暂停发送。", true); }
                    return;
                }
                skipped++;
                SetError(error, mismatch);
                return;
            }
            if (columnMismatch) return;
            TcpClient current;
            lock (sync) current = client;
            if (current == null || !current.Connected) { CloseClient(); return; }
            try
            {
                byte[] bytes = Encoding.ASCII.GetBytes(frame + "\n");
                current.GetStream().Write(bytes, 0, bytes.Length);
                sent++;
                lastError = null;
                Publish();
                if (historical) stopEvent.WaitOne(Math.Max(1, options.PeriodMs));
            }
            catch (Exception ex) { CloseClient(); SetError("VOFA+ 连接已断开：" + ex.Message, false); }
        }

        private static string DecodeLine(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            try { return new UTF8Encoding(false, true).GetString(bytes); }
            catch (DecoderFallbackException) { return Encoding.GetEncoding(936).GetString(bytes); }
        }

        private void SetError(string error, bool mismatch)
        {
            lastError = error;
            if (mismatch) columnMismatch = true;
            Publish();
        }

        private void CloseClient()
        {
            lock (sync)
            {
                if (client != null) try { client.Close(); } catch { }
                client = null;
            }
            Publish();
        }

        private void Publish()
        {
            if (report == null) return;
            TcpClient current;
            lock (sync) current = client;
            TcpClient adapter; lock (sync) adapter = adapterClient;
            report(new BridgeStatus { ClientConnected = current != null && current.Connected, AdapterConnected = adapter != null && adapter.Connected, TargetSessionStatus = targetSessionStatus, Sent = sent, Skipped = skipped, Writes = writes, WriteFailures = writeFailures, LastCommand = lastCommand, LastResponse = lastResponse, LastError = lastError, ColumnMismatch = columnMismatch });
        }

        public void Dispose()
        {
            stopEvent.Set();
            CloseClient();
            if (listener != null) try { listener.Stop(); } catch { }
            if (controlListener != null) try { controlListener.Stop(); } catch { }
            lock (sync) { if (adapterClient != null) try { adapterClient.Close(); } catch { } adapterClient = null; }
            if (worker != null && worker.IsAlive) worker.Join(2000);
            stopEvent.Dispose();
        }
    }

    internal static class BridgePowerShellTemplate
    {
        public static string Build(BridgeOptions options)
        {
            var b = new StringBuilder();
            b.AppendLine("# VOFA+ FireWater CSV 实时桥接；由 GelGenerator 自动生成");
            b.AppendLine("param(");
            b.AppendLine("  [string]$CsvPath = '" + Ps(options.CsvPath) + "',");
            b.AppendLine("  [string]$ListenAddress = '" + Ps(options.Address.ToString()) + "',");
            b.AppendLine("  [int]$Port = " + options.Port.ToString(CultureInfo.InvariantCulture) + ",");
            b.AppendLine("  [switch]$ReplayExisting = $" + (options.ReplayExisting ? "true" : "false"));
            b.AppendLine(")");
            b.AppendLine("$ErrorActionPreference = 'Stop'");
            b.AppendLine("$expectedColumns = " + options.ExpectedColumns.ToString(CultureInfo.InvariantCulture));
            b.AppendLine("$selectedColumns = @(" + JoinInts(options.SelectedColumns) + ")");
            b.AppendLine("$hexColumns = @(" + JoinHexIndexes(options.HexColumns) + ")");
            b.AppendLine("$periodMs = " + options.PeriodMs.ToString(CultureInfo.InvariantCulture));
            b.AppendLine("$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Parse($ListenAddress), $Port)");
            b.AppendLine("$client = $null; $position = 0L; $replayBoundary = 0L; $initialized = $false; $pending = [Collections.Generic.List[byte]]::new()");
            b.AppendLine("function Clean-Cell([string]$value) { $v = $value -replace '(?i)(?:(?:[A-Za-z0-9_.-]*(?:C28xx|CPU|Cortex|C6\\dxx)[A-Za-z0-9_.-]*)\\s*:\\s*)?GEL\\s+Output\\s*:\\s*',''; $p=$v.LastIndexOf(':'); if($p -ge 0){$v=$v.Substring($p+1)}; return $v.Trim() }");
            b.AppendLine("function Send-Line([string]$line) {");
            b.AppendLine("  if([string]::IsNullOrWhiteSpace($line)){return}; $cells=@($line.Split(',') | ForEach-Object { Clean-Cell $_ })");
            b.AppendLine("  $sample=0.0; if(-not [double]::TryParse($cells[0],[Globalization.NumberStyles]::Float,[Globalization.CultureInfo]::InvariantCulture,[ref]$sample)){return}");
            b.AppendLine("  if($cells.Count -ne $expectedColumns){Write-Warning \"列数变化：期望 $expectedColumns，实际 $($cells.Count)，该行已跳过。\"; return}");
            b.AppendLine("  $out=[Collections.Generic.List[string]]::new(); $out.Add($sample.ToString('R',[Globalization.CultureInfo]::InvariantCulture))");
            b.AppendLine("  foreach($i in $selectedColumns){ if($hexColumns -contains $i){$t=$cells[$i]; if($t.StartsWith('0x')){$t=$t.Substring(2)}; try{$out.Add(([Convert]::ToUInt64($t,16)).ToString())}catch{return}} else {$n=0.0; if(-not [double]::TryParse($cells[$i],[Globalization.NumberStyles]::Float,[Globalization.CultureInfo]::InvariantCulture,[ref]$n)){return}; $out.Add($n.ToString('R',[Globalization.CultureInfo]::InvariantCulture))} }");
            b.AppendLine("  if($null -ne $script:client -and $script:client.Connected){try{$bytes=[Text.Encoding]::ASCII.GetBytes(($out -join ',')+\"`n\"); $script:client.GetStream().Write($bytes,0,$bytes.Length)}catch{$script:client.Close();$script:client=$null}}");
            b.AppendLine("}");
            b.AppendLine("try {");
            b.AppendLine("  $listener.Start(); Write-Host \"桥接已启动：$ListenAddress`:$Port；等待 VOFA+ TCP Client。\"");
            b.AppendLine("  while($true){");
            b.AppendLine("    if($listener.Pending()){if($null -ne $client){$client.Close()};$client=$listener.AcceptTcpClient();$client.NoDelay=$true;Write-Host 'VOFA+ 已连接。'}");
            b.AppendLine("    if($ReplayExisting -and -not $initialized -and ($null -eq $client -or -not $client.Connected)){Start-Sleep -Milliseconds 100;continue}");
            b.AppendLine("    if(-not (Test-Path -LiteralPath $CsvPath)){Start-Sleep -Milliseconds 200;continue}");
            b.AppendLine("    try{$fs=[IO.File]::Open($CsvPath,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete); if(-not $initialized){$replayBoundary=$fs.Length;$position=if($ReplayExisting){0}else{$fs.Length};$initialized=$true}; if($fs.Length -lt $position){$position=0;$replayBoundary=0;$pending.Clear()}; if($fs.Length -gt $position){$fs.Position=$position;$buffer=New-Object byte[] 65536; while(($count=$fs.Read($buffer,0,$buffer.Length)) -gt 0){$blockStart=$position;$position+=$count;for($j=0;$j -lt $count;$j++){if($buffer[$j] -eq 10){if($pending.Count -gt 0 -and $pending[$pending.Count-1] -eq 13){$pending.RemoveAt($pending.Count-1)};$line=[Text.Encoding]::Default.GetString($pending.ToArray());$pending.Clear();Send-Line $line;if($ReplayExisting -and (($blockStart+$j+1) -le $replayBoundary) -and $null -ne $client){Start-Sleep -Milliseconds $periodMs}}else{$pending.Add($buffer[$j])}}}}}finally{if($null -ne $fs){$fs.Dispose()}}");
            b.AppendLine("    catch{Write-Warning $_.Exception.Message}; Start-Sleep -Milliseconds 100");
            b.AppendLine("  }");
            b.AppendLine("} finally { if($null -ne $client){$client.Close()};$listener.Stop();Write-Host '桥接已停止。' }");
            return b.ToString();
        }

        private static string Ps(string value) { return (value ?? String.Empty).Replace("'", "''"); }
        private static string JoinInts(int[] values)
        {
            var result = new string[values == null ? 0 : values.Length];
            for (int i = 0; i < result.Length; i++) result[i] = values[i].ToString(CultureInfo.InvariantCulture);
            return String.Join(",", result);
        }
        private static string JoinHexIndexes(bool[] values)
        {
            var result = new List<string>();
            if (values != null) for (int i = 0; i < values.Length; i++) if (values[i]) result.Add(i.ToString(CultureInfo.InvariantCulture));
            return String.Join(",", result.ToArray());
        }
    }

    internal static class InputValueValidator
    {
        public static bool TryNormalize(string type, string value, out string normalized)
        {
            normalized = null;
            if (String.IsNullOrWhiteSpace(value)) return false;
            value = value.Trim();
            if (type == "float32" || type == "float64")
            {
                double number;
                if (!Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) || Double.IsNaN(number) || Double.IsInfinity(number)) return false;
                if (type == "float32" && (number > Single.MaxValue || number < -Single.MaxValue)) return false;
                normalized = number.ToString("R", CultureInfo.InvariantCulture); return true;
            }
            long signed; ulong unsigned;
            if (type == "bool")
            {
                if (!TryUnsignedInteger(value, out unsigned) || unsigned > 1UL) return false;
                normalized = unsigned.ToString(CultureInfo.InvariantCulture); return true;
            }
            bool isUnsigned = type == "uint8" || type == "uint16" || type == "uint32";
            if (isUnsigned)
            {
                if (!TryUnsignedInteger(value, out unsigned)) return false;
                ulong max = type == "uint8" ? 255UL : type == "uint16" ? 65535UL : 4294967295UL;
                if (unsigned > max) return false; normalized = unsigned.ToString(CultureInfo.InvariantCulture); return true;
            }
            if (!TrySignedInteger(value, out signed)) return false;
            long min = type == "int8" ? -128L : (type == "int16" || type == "enum16") ? -32768L : -2147483648L;
            long maxSigned = type == "int8" ? 127L : (type == "int16" || type == "enum16") ? 32767L : 2147483647L;
            if (signed < min || signed > maxSigned) return false;
            normalized = signed.ToString(CultureInfo.InvariantCulture); return true;
        }

        private static bool TryUnsignedInteger(string value, out ulong result)
        {
            result = 0;
            string text = value.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return text.Length > 2 && UInt64.TryParse(text.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out result);
            return UInt64.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out result);
        }

        private static bool TrySignedInteger(string value, out long result)
        {
            result = 0;
            string text = value.Trim();
            bool negative = text.StartsWith("-", StringComparison.Ordinal);
            bool positive = text.StartsWith("+", StringComparison.Ordinal);
            string magnitude = (negative || positive) ? text.Substring(1) : text;
            if (!magnitude.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return Int64.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result);
            ulong raw;
            if (magnitude.Length <= 2 || !UInt64.TryParse(magnitude.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out raw)) return false;
            if (negative)
            {
                if (raw > 2147483648UL) return false;
                result = -(long)raw;
            }
            else
            {
                if (raw > Int64.MaxValue) return false;
                result = (long)raw;
            }
            return true;
        }
    }

    internal static class CcsInputAdapterTemplate
    {
        public static string Build(string ccsVersion, BridgeOptions options)
        {
            return ccsVersion == "CCS 20.x" ? Build20(options) : BuildLegacy(ccsVersion, options);
        }

        private static string BuildLegacy(string version, BridgeOptions options)
        {
            var b = new StringBuilder();
            b.AppendLine("// " + version + " Legacy DSS 输入适配器；在 CCS Scripting Console 中加载。\n// 先在 Debug 视图选中目标 CPU，使 activeDS 指向正确会话。");
            AppendLegacyMap(b, options.WritableVariables);
            b.AppendLine("var socket = new java.net.Socket('127.0.0.1', " + options.ControlPort.ToString(CultureInfo.InvariantCulture) + ");");
            b.AppendLine("var input = new java.io.BufferedReader(new java.io.InputStreamReader(socket.getInputStream(), 'UTF-8'));\nvar output = new java.io.PrintWriter(new java.io.OutputStreamWriter(socket.getOutputStream(), 'UTF-8'), true);");
            b.AppendLine("while (true) { var line=input.readLine(); if(line==null) break; var p=String(line).split(','); var name=p.length==3?p[1]:''; try { if(p.length!=3 || p[0]!='SET' || !writable[name]) throw 'BAD_COMMAND'; var item=writable[name]; activeDS.expression.evaluate(item.expr+' = ('+item.cast+')('+p[2]+')'); var actual=activeDS.expression.evaluateToString(item.expr); output.println('ACK,'+name+','+String(actual).replace(/^\\s+|\\s+$/g,'')); } catch(e) { output.println('ERR,'+name+',CCS_EVALUATE_FAILED'); } }\nsocket.close();");
            return b.ToString();
        }

        private static void AppendLegacyMap(StringBuilder b, VariableItem[] variables)
        {
            b.AppendLine("var writable = {};");
            foreach (VariableItem v in variables ?? new VariableItem[0])
                b.AppendLine("writable['" + Js(v.Name) + "']={expr:'" + Js(v.Expression) + "',cast:'" + Cast(v.Type) + "'};");
        }

        private static string Build20(BridgeOptions options)
        {
            var b = new StringBuilder();
            b.AppendLine("// CCS 20.x 输入适配器：兼容新 Scripting(session) 与 Legacy DSS(activeDS)。");
            b.AppendLine("(function () {");
            b.AppendLine("  var root = (typeof globalThis !== 'undefined') ? globalThis : this;");
            b.AppendLine("  var previous = root.__gelGeneratorInputBridge;");
            b.AppendLine("  if (previous && typeof previous.stop === 'function') { try { previous.stop(); } catch (_) {} }");
            b.AppendLine("  var netModule = require('net');");
            b.AppendLine("  var writable = {};");
            foreach (VariableItem v in options.WritableVariables ?? new VariableItem[0])
                b.AppendLine("  writable['" + Js(v.Name) + "']={expr:'" + Js(v.Expression) + "',cast:'" + Cast(v.Type) + "'};");
            b.AppendLine("  var corePattern = '" + Js(String.IsNullOrWhiteSpace(options.CorePattern) ? "C28xx_CPU1" : options.CorePattern) + "';");
            b.AppendLine("  var state = { socket:null, timer:null, pending:'', stopped:false, writable:writable, debug:null, debugError:null };");
            b.AppendLine("  function errorText(e) { return String(e && e.message ? e.message : e).replace(/[\\r\\n]+/g,' '); }");
            b.AppendLine("  function establishDebugger() {");
            b.AppendLine("    if (typeof session !== 'undefined' && session && session.expressions) return {kind:'modern',object:session,name:'session'};");
            b.AppendLine("    if (root.__ggSession && root.__ggSession.expressions) return {kind:'modern',object:root.__ggSession,name:'__ggSession'};");
            b.AppendLine("    if (typeof activeDS !== 'undefined' && activeDS && activeDS.expression) return {kind:'legacy',object:activeDS,name:'activeDS'};");
            b.AppendLine("    var scripting = (typeof ds !== 'undefined' && ds && typeof ds.openSession === 'function') ? ds : root.__ggDs;");
            b.AppendLine("    if (!scripting) { if (typeof initScripting !== 'function') throw new Error('initScripting不可用'); scripting=initScripting({inProcess:true}); root.__ggDs=scripting; }");
            b.AppendLine("    var listed=scripting.listCores(), cores=(listed&&listed.cores)?listed.cores:[], pattern=corePattern.toLowerCase();");
            b.AppendLine("    var exact=cores.filter(function(c){return String(c).split('/').pop().toLowerCase()===pattern;});");
            b.AppendLine("    var matches=exact.length?exact:cores.filter(function(c){var s=String(c);return s.toLowerCase().indexOf(pattern)>=0&&!/_CLA(?:1|2)?$/i.test(s);});");
            b.AppendLine("    if(matches.length!==1) throw new Error('CPU核匹配数量='+matches.length+'；pattern='+corePattern+'；cores='+cores.join('|'));");
            b.AppendLine("    root.__ggSession=scripting.openSession(matches[0]); return {kind:'modern',object:root.__ggSession,name:String(matches[0])};");
            b.AppendLine("  }");
            b.AppendLine("  function ensureDebugger() { if(state.debug) return state.debug; try{state.debug=establishDebugger();state.debugError=null;return state.debug;}catch(e){state.debugError=errorText(e);return null;} }");
            b.AppendLine("  function evaluate(context, expression) { return context.kind === 'modern' ? context.object.expressions.evaluate(expression) : context.object.expression.evaluate(expression); }");
            b.AppendLine("  function readBack(context, expression) { return context.kind === 'modern' ? context.object.expressions.evaluate(expression) : context.object.expression.evaluateToString(expression); }");
            b.AppendLine("  function send(text) { if (state.socket && !state.socket.destroyed) state.socket.write(text+'\\n'); }");
            b.AppendLine("  function processCommand(line) {");
            b.AppendLine("    var p=line.split(','); var name=p.length===3?p[1]:'';");
            b.AppendLine("    if(p.length!==3 || p[0]!=='SET' || !writable[name]) { send('ERR,'+name+',BAD_COMMAND'); return; }");
            b.AppendLine("    var item=writable[name], value=p[2], context=ensureDebugger();");
            b.AppendLine("    if(!context) { console.error('[GelGenerator] '+name+'：目标会话未就绪：'+state.debugError); send('STATUS,ERROR,'+String(state.debugError).replace(/[\\r\\n,]+/g,' ')); send('ERR,'+name+',NO_DEBUG_SESSION'); return; }");
            b.AppendLine("    var plain=item.expr+' = '+value, casted=item.expr+' = ('+item.cast+')('+value+')';");
            b.AppendLine("    try { evaluate(context,plain); }");
            b.AppendLine("    catch(first) { try { evaluate(context,casted); } catch(second) { console.error('[GelGenerator] '+name+' 赋值失败；plain='+errorText(first)+'；cast='+errorText(second)); send('ERR,'+name+',ASSIGN_FAILED'); return; } }");
            b.AppendLine("    try { var actual=readBack(context,item.expr); send('ACK,'+name+','+String(actual).replace(/[\\r\\n,]+/g,' ')); }");
            b.AppendLine("    catch(e) { console.error('[GelGenerator] '+name+' 回读失败：'+errorText(e)); send('ERR,'+name+',READBACK_FAILED'); }");
            b.AppendLine("  }");
            b.AppendLine("  function scheduleReconnect() { if(state.stopped || state.timer) return; state.timer=setTimeout(function(){state.timer=null;connect();},1000); }");
            b.AppendLine("  function connect() {");
            b.AppendLine("    if(state.stopped) return; if(state.socket){try{state.socket.removeAllListeners();state.socket.destroy();}catch(_){}} state.pending='';");
            b.AppendLine("    var socket=netModule.createConnection({host:'127.0.0.1',port:" + options.ControlPort.ToString(CultureInfo.InvariantCulture) + "}); state.socket=socket;");
            b.AppendLine("    socket.on('connect',function(){var context=ensureDebugger();if(context){console.log('[GelGenerator] 输入桥接已连接；目标会话='+context.name);send('STATUS,READY,'+String(context.name).replace(/[\\r\\n,]+/g,' '));}else{console.error('[GelGenerator] 目标会话初始化失败：'+state.debugError);send('STATUS,ERROR,'+String(state.debugError).replace(/[\\r\\n,]+/g,' '));}});");
            b.AppendLine("    socket.on('data',function(data){state.pending+=data.toString('utf8');var n;while((n=state.pending.indexOf('\\n'))>=0){var line=state.pending.slice(0,n).replace(/\\r$/,'');state.pending=state.pending.slice(n+1);if(line)processCommand(line);}});");
            b.AppendLine("    socket.on('error',function(e){console.error('[GelGenerator] 输入桥接连接错误：'+errorText(e));});");
            b.AppendLine("    socket.on('close',function(){if(!state.stopped){console.log('[GelGenerator] 输入桥接已断开，1秒后重连');scheduleReconnect();}});");
            b.AppendLine("  }");
            b.AppendLine("  state.stop=function(){state.stopped=true;if(state.timer){clearTimeout(state.timer);state.timer=null;}if(state.socket){try{state.socket.removeAllListeners();state.socket.destroy();}catch(_){}}};");
            b.AppendLine("  root.__gelGeneratorInputBridge=state; connect();");
            b.AppendLine("})();");
            return b.ToString();
        }

        private static string Cast(string type)
        {
            if (type == "float32") return "float"; if (type == "float64") return "double";
            if (type == "uint32") return "unsigned long"; if (type == "int32" || type == "enum32") return "long";
            if (type == "uint8" || type == "uint16" || type == "bool") return "unsigned int"; return "int";
        }
        private static string Js(string value) { return (value ?? String.Empty).Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "").Replace("\n", ""); }
    }

    internal static class CcsCsvConverter
    {
        private static readonly Regex GelOutputPrefix = new Regex(
            @"(?i)(?:(?:[A-Za-z0-9_.-]*(?:C28xx|CPU|Cortex|C6\dxx)[A-Za-z0-9_.-]*)\s*:\s*)?GEL\s+Output\s*:\s*",
            RegexOptions.Compiled);

        private static readonly Regex CpuPrefix = new Regex(
            @"(?i)(?<![A-Za-z0-9_.-])(?:[A-Za-z0-9_.-]*(?:C28xx|CPU\d*|Cortex|C6\dxx)[A-Za-z0-9_.-]*)\s*:\s*",
            RegexOptions.Compiled);

        public static void ConvertFile(string csvPath, string xlsxPath)
        {
            string text = ReadText(csvPath);
            List<List<string>> rows = ParseAndNormalize(text);
            XlsxWriter.Write(xlsxPath, rows);
        }

        public static List<List<string>> ParseAndNormalize(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) throw new InvalidDataException("CSV 文件为空。");
            string cleaned = StripCcsPrefixes(text).Replace("\uFEFF", String.Empty);
            List<List<string>> parsed = ParseCsv(cleaned);
            var tokens = new List<string>();
            foreach (List<string> row in parsed)
            {
                bool allEmpty = true;
                foreach (string value in row)
                    if (!String.IsNullOrWhiteSpace(value)) { allEmpty = false; break; }
                if (allEmpty) continue;
                int usableCells = row.Count;
                /* CCS 可能把每次 GEL_TextOut 记录单独换行；带逗号的片段会被
                 * CSV 解析成“值 + 行尾空字段”。该行尾空字段不是实际数据。 */
                if (usableCells > 1 && String.IsNullOrWhiteSpace(row[usableCells - 1])) usableCells--;
                for (int i = 0; i < usableCells; i++) tokens.Add(StripCcsPrefixes(row[i]).Trim());
            }
            if (tokens.Count < 2) throw new InvalidDataException("没有找到有效表头和数据。");

            int firstData = -1;
            for (int i = 0; i < tokens.Count; i++)
            {
                double number;
                if (TryNumber(tokens[i], out number)) { firstData = i; break; }
            }
            if (firstData <= 0) throw new InvalidDataException("无法识别表头；首个数据行应以数值采样序号开始。");
            int columns = firstData;
            int dataCells = tokens.Count - firstData;
            if (dataCells == 0) throw new InvalidDataException("CSV 只有表头，没有数据。");
            if ((dataCells % columns) != 0)
                throw new InvalidDataException("清理后数据列数不完整：表头 " + columns + " 列，数据单元格 " + dataCells + " 个。");

            var result = new List<List<string>>();
            result.Add(tokens.GetRange(0, columns));
            for (int offset = firstData; offset < tokens.Count; offset += columns)
                result.Add(tokens.GetRange(offset, columns));
            return result;
        }

        public static string StripCcsPrefixes(string text)
        {
            if (text == null) return String.Empty;
            string value = GelOutputPrefix.Replace(text, String.Empty);
            value = CpuPrefix.Replace(value, String.Empty);
            return value;
        }

        public static bool TryNumber(string value, out double number)
        {
            bool ok = Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
            return ok && !Double.IsNaN(number) && !Double.IsInfinity(number);
        }

        public static bool IsExcelSafeInteger(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return false;
            string s = value.Trim();
            int start = (s[0] == '+' || s[0] == '-') ? 1 : 0;
            if (start == s.Length) return false;
            int significant = 0;
            bool nonZeroSeen = false;
            for (int i = start; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch < '0' || ch > '9') return false;
                if (ch != '0') nonZeroSeen = true;
                if (nonZeroSeen) significant++;
            }
            return significant <= 15;
        }

        public static bool IsIntegerToken(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return false;
            string s = value.Trim();
            int start = (s[0] == '+' || s[0] == '-') ? 1 : 0;
            if (start == s.Length) return false;
            for (int i = start; i < s.Length; i++) if (s[i] < '0' || s[i] > '9') return false;
            return true;
        }

        public static bool TryBuildFireWaterFrame(string line, int expectedColumns, int[] selectedColumns, bool[] hexColumns,
            out string frame, out bool header, out bool mismatch, out string error)
        {
            frame = String.Empty; header = false; mismatch = false; error = null;
            if (String.IsNullOrWhiteSpace(line)) { header = true; return false; }
            List<List<string>> rows;
            try { rows = ParseCsv(line + "\n"); }
            catch (Exception ex) { error = "CSV 行解析失败：" + ex.Message; return false; }
            if (rows.Count == 0) { header = true; return false; }
            List<string> cells = rows[0];
            for (int i = 0; i < cells.Count; i++) cells[i] = StripBridgePrefix(cells[i]).Trim().TrimStart('\uFEFF');
            bool firstNumeric;
            double first;
            firstNumeric = Double.TryParse(cells.Count == 0 ? String.Empty : cells[0], NumberStyles.Float, CultureInfo.InvariantCulture, out first);
            if (!firstNumeric)
            {
                header = true;
                mismatch = cells.Count != expectedColumns;
                return false;
            }
            if (cells.Count != expectedColumns)
            {
                mismatch = true;
                error = "数据列数错误：期望 " + expectedColumns + " 列，实际 " + cells.Count + " 列。";
                return false;
            }
            var output = new List<string>();
            output.Add(NormalizeFireWaterNumber(cells[0], false));
            foreach (int column in selectedColumns)
            {
                if (column <= 0 || column >= cells.Count) { error = "通道索引超出 CSV 列范围。"; return false; }
                string normalized = NormalizeFireWaterNumber(cells[column], hexColumns != null && column < hexColumns.Length && hexColumns[column]);
                if (normalized == null) { error = "非数值字段：第 " + (column + 1) + " 列，内容为“" + cells[column] + "”。"; return false; }
                output.Add(normalized);
            }
            if (output[0] == null) { error = "样本序号不是有效数值。"; return false; }
            frame = String.Join(",", output.ToArray());
            return true;
        }

        private static string NormalizeFireWaterNumber(string value, bool hexadecimal)
        {
            if (hexadecimal)
            {
                ulong number;
                string token = (value ?? String.Empty).Trim();
                if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) token = token.Substring(2);
                return UInt64.TryParse(token, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out number)
                    ? number.ToString(CultureInfo.InvariantCulture) : null;
            }
            double real;
            return Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out real) && !Double.IsNaN(real) && !Double.IsInfinity(real)
                ? real.ToString("R", CultureInfo.InvariantCulture) : null;
        }

        private static string StripBridgePrefix(string value)
        {
            string cleaned = StripCcsPrefixes(value ?? String.Empty);
            int colon = cleaned.LastIndexOf(':');
            return colon >= 0 ? cleaned.Substring(colon + 1) : cleaned;
        }

        private static string ReadText(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            try { return new UTF8Encoding(false, true).GetString(bytes); }
            catch (DecoderFallbackException) { return Encoding.GetEncoding(936).GetString(bytes); }
        }

        private static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (quoted)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else quoted = false;
                    }
                    else field.Append(ch);
                }
                else
                {
                    if (ch == '"' && field.Length == 0) quoted = true;
                    else if (ch == ',') { row.Add(field.ToString()); field.Length = 0; }
                    else if (ch == '\r' || ch == '\n')
                    {
                        row.Add(field.ToString());
                        field.Length = 0;
                        rows.Add(row);
                        row = new List<string>();
                        if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    }
                    else field.Append(ch);
                }
            }
            if (quoted) throw new InvalidDataException("CSV 包含未闭合的双引号字段。");
            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row);
            }
            return rows;
        }
    }

    internal static class XlsxWriter
    {
        public static void Write(string path, IList<List<string>> rows)
        {
            if (rows == null || rows.Count < 2) throw new InvalidDataException("没有可写入 XLSX 的数据。");
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (FileStream stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    Add(archive, "[Content_Types].xml", ContentTypes());
                    Add(archive, "_rels/.rels", PackageRelationships());
                    Add(archive, "xl/workbook.xml", WorkbookXml());
                    Add(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationships());
                    Add(archive, "xl/styles.xml", StylesXml());
                    Add(archive, "xl/worksheets/sheet1.xml", SheetXml(rows));
                }
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        private static void Add(ZipArchive archive, string name, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false))) writer.Write(content);
        }

        private static string ContentTypes()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                "</Types>";
        }

        private static string PackageRelationships()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                "</Relationships>";
        }

        private static string WorkbookXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                "<sheets><sheet name=\"Data\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>";
        }

        private static string WorkbookRelationships()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                "</Relationships>";
        }

        private static string StylesXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                "<fonts count=\"2\"><font><sz val=\"10\"/><name val=\"Arial\"/></font><font><b/><color rgb=\"FFFFFFFF\"/><sz val=\"10\"/><name val=\"Arial\"/></font></fonts>" +
                "<fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF1F4E78\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
                "<borders count=\"2\"><border><left/><right/><top/><bottom/><diagonal/></border><border><left/><right/><top/><bottom style=\"thin\"><color rgb=\"FFD9E2F3\"/></bottom><diagonal/></border></borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                "<cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf></cellXfs>" +
                "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>";
        }

        private static string SheetXml(IList<List<string>> rows)
        {
            int columns = rows[0].Count;
            var widths = new int[columns];
            for (int c = 0; c < columns; c++) widths[c] = 10;
            foreach (List<string> row in rows)
                for (int c = 0; c < columns && c < row.Count; c++)
                    widths[c] = Math.Min(40, Math.Max(widths[c], DisplayWidth(row[c]) + 2));

            var b = new StringBuilder();
            b.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            b.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            b.Append("<dimension ref=\"A1:").Append(CellReference(columns - 1, rows.Count)).Append("\"/>");
            b.Append("<sheetViews><sheetView workbookViewId=\"0\" showGridLines=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
            b.Append("<sheetFormatPr defaultRowHeight=\"15\"/>");
            b.Append("<cols>");
            for (int c = 0; c < columns; c++)
                b.Append("<col min=\"").Append(c + 1).Append("\" max=\"").Append(c + 1).Append("\" width=\"").Append(widths[c]).Append("\" customWidth=\"1\"/>");
            b.Append("</cols><sheetData>");
            for (int r = 0; r < rows.Count; r++)
            {
                b.Append("<row r=\"").Append(r + 1).Append("\"");
                if (r == 0) b.Append(" ht=\"22\" customHeight=\"1\"");
                b.Append(">");
                for (int c = 0; c < columns; c++)
                {
                    string value = c < rows[r].Count ? rows[r][c] : String.Empty;
                    string reference = CellReference(c, r + 1);
                    double number;
                    bool integerToken = CcsCsvConverter.IsIntegerToken(value);
                    if (r > 0 && integerToken && CcsCsvConverter.IsExcelSafeInteger(value) &&
                        Double.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                    {
                        b.Append("<c r=\"").Append(reference).Append("\"><v>")
                            .Append(value.TrimStart('+')).Append("</v></c>");
                    }
                    else if (r > 0 && !integerToken && CcsCsvConverter.TryNumber(value, out number))
                    {
                        b.Append("<c r=\"").Append(reference).Append("\"><v>")
                            .Append(number.ToString("R", CultureInfo.InvariantCulture)).Append("</v></c>");
                    }
                    else
                    {
                        b.Append("<c r=\"").Append(reference).Append("\" t=\"inlineStr\"");
                        if (r == 0) b.Append(" s=\"1\"");
                        b.Append("><is><t xml:space=\"preserve\">").Append(Xml(value)).Append("</t></is></c>");
                    }
                }
                b.Append("</row>");
            }
            b.Append("</sheetData><autoFilter ref=\"A1:").Append(CellReference(columns - 1, rows.Count)).Append("\"/>");
            b.Append("</worksheet>");
            return b.ToString();
        }

        private static int DisplayWidth(string value)
        {
            int width = 0;
            foreach (char ch in value ?? String.Empty) width += ch > 255 ? 2 : 1;
            return width;
        }

        private static string CellReference(int column, int row)
        {
            string letters = String.Empty;
            int value = column + 1;
            while (value > 0)
            {
                value--;
                letters = (char)('A' + (value % 26)) + letters;
                value /= 26;
            }
            return letters + row.ToString(CultureInfo.InvariantCulture);
        }

        private static string Xml(string value)
        {
            return SecurityElement.Escape(value ?? String.Empty) ?? String.Empty;
        }
    }

    internal static class GelTemplate
    {
        private const string Int8 = "int8";
        private const string UInt8 = "uint8";
        private const string Int16 = "int16";
        private const string UInt16 = "uint16";
        private const string Int32 = "int32";
        private const string UInt32 = "uint32";
        private const string Float32 = "float32";
        private const string Float64 = "float64";
        private const string Bool = "bool";
        private const string Enum16 = "enum16";
        private const string Enum32 = "enum32";

        public static VariableItem MigrateLegacy(VariableItem item)
        {
            if (item == null) return new VariableItem { Direction = "output", Type = Float32, OutputFormat = "十进制" };
            string type = item.Type ?? String.Empty;
            string format = item.OutputFormat;
            if (type.StartsWith("int64", StringComparison.Ordinal) || type.StartsWith("uint64", StringComparison.Ordinal))
                throw new InvalidOperationException("旧配置包含已取消的 int64/uint64 类型；GEL 无法保证 64 位整数无损输出。");
            if (type == "float") type = Float32;
            else if (type == "double") type = Float64;
            else if (type == "int") type = Int16;
            else if (type == "unsigned int") type = UInt16;
            else if (type == "hex") { type = UInt16; format = "十六进制"; }
            else if (type == "string")
                throw new InvalidOperationException("旧配置包含 string 类型。GEL %s 仅接受主机端常量字符串，不能表示目标端字符串；请删除该项或改为具体数值成员。");
            else if (type.StartsWith("int16", StringComparison.Ordinal)) type = Int16;
            else if (type.StartsWith("uint16", StringComparison.Ordinal)) type = UInt16;
            else if (type.StartsWith("int32", StringComparison.Ordinal)) type = Int32;
            else if (type.StartsWith("uint32", StringComparison.Ordinal)) type = UInt32;
            else if (type.StartsWith("float32", StringComparison.Ordinal)) type = Float32;
            else if (type.StartsWith("float64", StringComparison.Ordinal)) type = Float64;
            else if (type.StartsWith("bool", StringComparison.Ordinal)) type = Bool;
            else if (type == "enum" || type.StartsWith("enum（", StringComparison.Ordinal))
                throw new InvalidOperationException("旧配置中的 enum 未标明底层宽度；请手工选择 enum16 或 enum32。");
            else if (type.StartsWith("enum16", StringComparison.Ordinal)) type = Enum16;
            else if (type.StartsWith("enum32", StringComparison.Ordinal)) type = Enum32;
            if (String.IsNullOrWhiteSpace(format)) format = "十进制";
            if (format == "科学计数法") format = "十进制";
            string direction = Array.IndexOf(MainForm.Directions, item.Direction) >= 0 ? item.Direction : "output";
            if (format == "无") format = "十进制";
            return new VariableItem { Direction = direction, Name = item.Name, Expression = item.Expression, Type = type, OutputFormat = format, SendToVofa = item.SendToVofa };
        }

        public static void ValidateTypeAndFormat(string type, string format)
        {
            bool floating = type == Float32 || type == Float64;
            bool integer = type == Int8 || type == UInt8 || type == Int16 || type == UInt16 || type == Int32 || type == UInt32 || type == Bool || type == Enum16 || type == Enum32;
            if (!floating && !integer) throw new InvalidOperationException("目标数据类型无效。");
            if (floating && format != "十进制")
                throw new InvalidOperationException("浮点类型只允许十进制格式。");
            if (integer && format != "十进制" && format != "十六进制")
                throw new InvalidOperationException("整数、布尔值和枚举只允许十进制或十六进制。");
        }

        public static string Build(string csvPath, int periodMs, IList<VariableItem> variables)
        {
            return Build(csvPath, periodMs, variables, "CCS 12.7.1");
        }

        public static string Build(string csvPath, int periodMs, IList<VariableItem> variables, string ccsVersion)
        {
            return Build(csvPath, periodMs, variables, ccsVersion, new VariableItem[0]);
        }

        public static string Build(string csvPath, int periodMs, IList<VariableItem> variables, string ccsVersion, IList<VariableItem> writableVariables)
        {
            for (int i = 0; i < variables.Count; i++)
            {
                VariableItem migrated = MigrateLegacy(variables[i]);
                variables[i].Type = migrated.Type;
                variables[i].OutputFormat = migrated.OutputFormat;
                ValidateTypeAndFormat(variables[i].Type, variables[i].OutputFormat);
            }
            var b = new StringBuilder();
            b.AppendLine("/*****************************************************************************");
            b.AppendLine(" * " + (String.IsNullOrWhiteSpace(ccsVersion) ? "CCS 12.7.1" : ccsVersion) + " GEL/JTAG 低速数据采集脚本");
            b.AppendLine(" * 由 CCS GEL 数据采集脚本生成器自动生成。");
            b.AppendLine(" * 仅适合几 Hz～几十 Hz，严禁用于 kHz 级高速控制环采集。");
            b.AppendLine(" ****************************************************************************/");
            b.AppendLine();
            b.AppendLine("#define DL_CSV_PATH \"" + EscapeGelString(csvPath) + "\"");
            b.AppendLine("long DL_SAMPLE_PERIOD_MS = " + periodMs.ToString(CultureInfo.InvariantCulture) + "L;");
            b.AppendLine("int DL_STATUS_PRINT_INTERVAL = 10;");
            b.AppendLine("int DL_TIMER_ID = 1001;");
            b.AppendLine("int __dl_running = 0;");
            b.AppendLine("int __dl_busy = 0;");
            b.AppendLine("long __dl_sample_count = 0L;");
            b.AppendLine("long __dl_error_count = 0L;");
            b.AppendLine();
            b.AppendLine("DataLogger_WriteHeader()");
            b.AppendLine("{");
            b.AppendLine("    GEL_EnableFileOutput(DL_CSV_PATH, 0, 0);");
            b.AppendLine("    GEL_TextOut(\"sample" + (variables.Count == 0 ? String.Empty : "," + EscapeGelString(String.Join(",", Names(variables)))) + "\\n\");");
            b.AppendLine("    GEL_DisableFileOutput();");
            b.AppendLine("}");
            b.AppendLine();
            b.AppendLine("DataLogger_StopInternal()");
            b.AppendLine("{");
            b.AppendLine("    GEL_CancelTimer(DL_TIMER_ID);");
            b.AppendLine("    'GEL'::__dl_running = 0;");
            b.AppendLine("    'GEL'::__dl_busy = 0;");
            b.AppendLine("}");
            b.AppendLine();
            b.AppendLine("DataLogger_SampleOnce()");
            b.AppendLine("{");
            for (int i = 0; i < variables.Count; i++)
                b.AppendLine("    " + LocalType(variables[i].Type) + " dl_v" + i + ";");
            b.AppendLine();
            b.AppendLine("    if ('GEL'::__dl_running == 0) return;");
            b.AppendLine("    if ('GEL'::__dl_busy != 0)");
            b.AppendLine("    {");
            b.AppendLine("        'GEL'::__dl_error_count++;");
            b.AppendLine("        GEL_TextOut(\"[DataLogger] 警告：跳过重入采样。\\n\");");
            b.AppendLine("        return;");
            b.AppendLine("    }");
            b.AppendLine("    'GEL'::__dl_busy = 1;");
            b.AppendLine("    if (GEL_IsConnected() == 0)");
            b.AppendLine("    {");
            b.AppendLine("        DataLogger_StopInternal();");
            b.AppendLine("        GEL_TextOut(\"[DataLogger] 错误：目标未连接，采集已停止。\\n\");");
            b.AppendLine("        return;");
            b.AppendLine("    }");
            b.AppendLine("    if (GEL_IsHalted() != 0)");
            b.AppendLine("    {");
            b.AppendLine("        DataLogger_StopInternal();");
            b.AppendLine("        GEL_TextOut(\"[DataLogger] 错误：CPU 已 Halt，采集已停止。\\n\");");
            b.AppendLine("        return;");
            b.AppendLine("    }");
            b.AppendLine();
            for (int i = 0; i < variables.Count; i++)
                b.AppendLine("    dl_v" + i + " = 'SYM'::" + variables[i].Expression + ";");
            b.AppendLine();
            b.AppendLine("    GEL_EnableFileOutput(DL_CSV_PATH, 1, 0);");
            b.AppendLine("    GEL_TextOut(\"%d" + (variables.Count == 0 ? "\\n" : ",") + "\",,,,,'GEL'::__dl_sample_count);");
            for (int i = 0; i < variables.Count; i++)
            {
                string suffix = i == variables.Count - 1 ? "\\n" : ",";
                b.AppendLine("    GEL_TextOut(\"" + Format(variables[i].Type, variables[i].OutputFormat) + suffix + "\",,,,,dl_v" + i + ");");
            }
            b.AppendLine("    GEL_DisableFileOutput();");
            b.AppendLine("    'GEL'::__dl_sample_count++;");
            b.AppendLine("    'GEL'::__dl_busy = 0;");
            b.AppendLine("    if ((DL_STATUS_PRINT_INTERVAL > 0) && (('GEL'::__dl_sample_count % DL_STATUS_PRINT_INTERVAL) == 0))");
            b.AppendLine("        GEL_TextOut(\"[DataLogger] samples=%d\\n\",,,,,'GEL'::__dl_sample_count);");
            b.AppendLine("}");
            b.AppendLine();
            b.AppendLine("menuitem \"Data Logger\"");
            b.AppendLine();
            b.AppendLine("hotmenu DataLogger_Start()");
            b.AppendLine("{");
            b.AppendLine("    if ('GEL'::__dl_running != 0) { GEL_TextOut(\"[DataLogger] 已在运行。\\n\"); return; }");
            b.AppendLine("    if (DL_SAMPLE_PERIOD_MS <= 0) { GEL_TextOut(\"[DataLogger] 周期必须大于 0。\\n\"); return; }");
            b.AppendLine("    if (GEL_IsConnected() == 0) { GEL_TextOut(\"[DataLogger] 目标未连接。\\n\"); return; }");
            b.AppendLine("    if (GEL_IsHalted() != 0) { GEL_TextOut(\"[DataLogger] 请先运行 CPU。\\n\"); return; }");
            b.AppendLine("    GEL_CancelTimer(DL_TIMER_ID);");
            b.AppendLine("    'GEL'::__dl_sample_count = 0;");
            b.AppendLine("    'GEL'::__dl_error_count = 0;");
            b.AppendLine("    'GEL'::__dl_busy = 0;");
            b.AppendLine("    DataLogger_WriteHeader();");
            b.AppendLine("    'GEL'::__dl_running = 1;");
            b.AppendLine("    GEL_SetTimer(DL_SAMPLE_PERIOD_MS, DL_TIMER_ID, \"DataLogger_SampleOnce()\");");
            b.AppendLine("    GEL_TextOut(\"[DataLogger] 已启动，period=%d ms\\n\",,,,,DL_SAMPLE_PERIOD_MS);");
            b.AppendLine("}");
            b.AppendLine();
            b.AppendLine("hotmenu DataLogger_Stop()");
            b.AppendLine("{");
            b.AppendLine("    DataLogger_StopInternal();");
            b.AppendLine("    GEL_TextOut(\"[DataLogger] 已停止，samples=%d\\n\",,,,,'GEL'::__dl_sample_count);");
            b.AppendLine("}");
            b.AppendLine();
            b.AppendLine("hotmenu DataLogger_Status()");
            b.AppendLine("{");
            b.AppendLine("    GEL_TextOut(\"[DataLogger] running=%d\\n\",,,,,'GEL'::__dl_running);");
            b.AppendLine("    GEL_TextOut(\"[DataLogger] samples=%d\\n\",,,,,'GEL'::__dl_sample_count);");
            b.AppendLine("    GEL_TextOut(\"[DataLogger] errors=%d\\n\",,,,,'GEL'::__dl_error_count);");
            b.AppendLine("}");
            b.AppendLine();
            b.AppendLine("hotmenu DataLogger_ForceCleanup()");
            b.AppendLine("{");
            b.AppendLine("    GEL_CancelTimer(DL_TIMER_ID);");
            b.AppendLine("    GEL_DisableFileOutput();");
            b.AppendLine("    'GEL'::__dl_running = 0;");
            b.AppendLine("    'GEL'::__dl_busy = 0;");
            b.AppendLine("    GEL_TextOut(\"[DataLogger] 已强制清理。\\n\");");
            b.AppendLine("}");
            if (writableVariables != null && writableVariables.Count > 0)
            {
                b.AppendLine();
                b.AppendLine("menuitem \"Target Variable Input\"");
                for (int i = 0; i < writableVariables.Count; i++)
                {
                    VariableItem item = MigrateLegacy(writableVariables[i]);
                    ValidateTypeAndFormat(item.Type, "十进制");
                    string functionName = "TargetInput_Set_" + (i + 1).ToString(CultureInfo.InvariantCulture) + "_" + GelIdentifier(item.Name);
                    b.AppendLine();
                    b.AppendLine("dialog " + functionName + "(" + LocalType(item.Type) + " value)");
                    b.AppendLine("{");
                    b.AppendLine("    'SYM'::" + item.Expression + " = value;");
                    b.AppendLine("    GEL_TextOut(\"[TargetInput] " + EscapeGelString(item.Name) + "=" + Format(item.Type, "十进制") + "\\n\",,,,,'SYM'::" + item.Expression + ");");
                    b.AppendLine("}");
                }
            }
            return b.ToString();
        }

        public static void ValidateGeneratedScript(string script, int variableCount)
        {
            if (String.IsNullOrWhiteSpace(script))
                throw new InvalidOperationException("生成结果为空。");
            if (script.Contains("\", ,,,,,"))
                throw new InvalidOperationException("生成结果包含错误的 GEL_TextOut 参数占位符。");
            if (!script.Contains("GEL_TextOut(\"%d,\",,,,,'GEL'::__dl_sample_count);") &&
                !script.Contains("GEL_TextOut(\"%d\\n\",,,,,'GEL'::__dl_sample_count);"))
                throw new InvalidOperationException("采样序号的 GEL_TextOut 参数位置不正确。");

            int valueOutputCount = 0;
            int position = 0;
            while ((position = script.IndexOf("\",,,,,dl_v", position, StringComparison.Ordinal)) >= 0)
            {
                valueOutputCount++;
                position += 10;
            }
            if (valueOutputCount != variableCount)
                throw new InvalidOperationException("变量输出语句数量与变量数量不一致。");
        }

        private static string[] Names(IList<VariableItem> variables)
        {
            var result = new string[variables.Count];
            for (int i = 0; i < variables.Count; i++) result[i] = variables[i].Name;
            return result;
        }

        private static string LocalType(string type)
        {
            if (type == Float32) return "float";
            if (type == Float64) return "long double";
            if (type == UInt32) return "unsigned long";
            if (type == UInt8 || type == UInt16 || type == Bool) return "unsigned int";
            if (type == Int32 || type == Enum32) return "long";
            return "int";
        }

        private static string Format(string type, string outputFormat)
        {
            if (outputFormat == "十六进制") return "%x";
            if (type == UInt8 || type == UInt16 || type == UInt32 || type == Bool) return "%u";
            if (type == Float32 || type == Float64) return "%f";
            return "%d";
        }

        private static string EscapeGelString(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
        }

        private static string GelIdentifier(string value)
        {
            var b = new StringBuilder();
            foreach (char ch in value ?? String.Empty)
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '_') b.Append(ch);
                else b.Append('_');
            }
            if (b.Length == 0) b.Append("variable");
            if (b[0] >= '0' && b[0] <= '9') b.Insert(0, '_');
            return b.ToString();
        }
    }
}
