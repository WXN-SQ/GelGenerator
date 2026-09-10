using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
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
        public string Name { get; set; }
        public string Expression { get; set; }
        public string Type { get; set; }
        public string OutputFormat { get; set; }
    }

    public sealed class GeneratorConfig
    {
        public string CsvPath { get; set; }
        public decimal PeriodValue { get; set; }
        public string PeriodUnit { get; set; }
        public string GelPath { get; set; }
        public List<VariableItem> Variables { get; set; }
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

        internal static readonly string[] GelTypes =
        {
            "int8", "uint8", "int16", "uint16", "int32", "uint32",
            "float32", "float64", "bool", "enum16", "enum32"
        };

        internal static readonly string[] OutputFormats =
        {
            "十进制", "十六进制"
        };

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
            var converterPage = new TabPage("CSV 转 XLSX");
            tabs.TabPages.Add(generatorPage);
            tabs.TabPages.Add(converterPage);
            Controls.Add(tabs);
            BuildGeneratorUi(generatorPage);
            BuildConverterUi(converterPage);
            txtCsvPath.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "data_logger.csv");
            numPeriod.Value = 1000;
            cmbPeriodUnit.SelectedIndex = 0;
            txtGelPath.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "data_logger.gel");
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
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = true;
            grid.RowHeadersWidth = 55;
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "NameColumn", HeaderText = "CSV 列名称", DataPropertyName = "Name",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 30
            });
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
                Name = "FormatColumn", HeaderText = "输出格式", DataPropertyName = "OutputFormat",
                Width = 105, FlatStyle = FlatStyle.Flat
            };
            formatColumn.Items.AddRange(OutputFormats);
            grid.Columns.Add(formatColumn);
            grid.DefaultValuesNeeded += delegate(object sender, DataGridViewRowEventArgs e)
            {
                e.Row.Cells["TypeColumn"].Value = "float32";
                e.Row.Cells["FormatColumn"].Value = "十进制";
            };
            grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                e.ThrowException = false;
            };
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

            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var status = new StatusStrip { SizingGrip = false, Dock = DockStyle.Fill };
            statusLabel.Text = "就绪：请添加至少一个变量。";
            status.Items.Add(statusLabel);
            bottom.Controls.Add(status, 0, 0);
            var generate = ButtonFor("生成 GEL 文件", delegate { GenerateGel(); });
            generate.AutoSize = true;
            generate.Font = new Font(Font, FontStyle.Bold);
            generate.Padding = new Padding(16, 5, 16, 5);
            bottom.Controls.Add(generate, 1, 0);
            root.Controls.Add(bottom, 0, 4);
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

        private void AddRow()
        {
            int index = grid.Rows.Add();
            grid.Rows[index].Cells["TypeColumn"].Value = "float32";
            grid.Rows[index].Cells["FormatColumn"].Value = "十进制";
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
                string name = CellText(row, "NameColumn");
                string expression = CellText(row, "ExpressionColumn");
                string type = CellText(row, "TypeColumn");
                string outputFormat = CellText(row, "FormatColumn");
                if (validate)
                {
                    if (String.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("第 " + logicalRow + " 行的 CSV 列名称为空。");
                    if (String.IsNullOrWhiteSpace(expression)) throw new InvalidOperationException("第 " + logicalRow + " 行的变量表达式为空。");
                    if (name.IndexOfAny(new[] { ',', '\r', '\n', '"' }) >= 0) throw new InvalidOperationException("CSV 列名称不能包含逗号、引号或换行：" + name);
                    if (!names.Add(name)) throw new InvalidOperationException("CSV 列名称重复：" + name);
                    if (Array.IndexOf(GelTypes, type) < 0) throw new InvalidOperationException("第 " + logicalRow + " 行的变量类型无效。");
                    GelTemplate.ValidateTypeAndFormat(type, outputFormat);
                }
                list.Add(new VariableItem { Name = name, Expression = expression, Type = type, OutputFormat = outputFormat });
            }
            if (validate && list.Count == 0) throw new InvalidOperationException("请至少添加一个采集变量。");
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
                string script = GelTemplate.Build(txtCsvPath.Text, period, variables);
                GelTemplate.ValidateGeneratedScript(script, variables.Count);
                File.WriteAllText(txtGelPath.Text, script, new UTF8Encoding(false));
                statusLabel.Text = "生成成功：" + txtGelPath.Text;
                MessageBox.Show(this, "GEL 文件生成成功：\n" + txtGelPath.Text, "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                Variables = ReadVariables(validate)
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
                    grid.Rows.Clear();
                    if (config.Variables != null)
                    {
                        foreach (VariableItem item in config.Variables)
                        {
                            VariableItem migrated = GelTemplate.MigrateLegacy(item);
                            grid.Rows.Add(migrated.Name, migrated.Expression, migrated.Type, migrated.OutputFormat);
                        }
                    }
                    statusLabel.Text = "配置已打开：" + dialog.FileName;
                }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "打开配置失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
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
            if (item == null) return new VariableItem { Type = Float32, OutputFormat = "十进制" };
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
            return new VariableItem { Name = item.Name, Expression = item.Expression, Type = type, OutputFormat = format };
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
            for (int i = 0; i < variables.Count; i++)
            {
                VariableItem migrated = MigrateLegacy(variables[i]);
                variables[i].Type = migrated.Type;
                variables[i].OutputFormat = migrated.OutputFormat;
                ValidateTypeAndFormat(variables[i].Type, variables[i].OutputFormat);
            }
            var b = new StringBuilder();
            b.AppendLine("/*****************************************************************************");
            b.AppendLine(" * CCS 12.7.1 GEL/JTAG 低速数据采集脚本");
            b.AppendLine(" * 由 CCS GEL 数据采集脚本生成器自动生成。");
            b.AppendLine(" * 仅适合几 Hz～几十 Hz，严禁用于 kHz 级高速控制环采集。");
            b.AppendLine(" ****************************************************************************/");
            b.AppendLine();
            b.AppendLine("string DL_CSV_PATH = \"" + EscapeGelString(csvPath) + "\";");
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
            b.AppendLine("    GEL_TextOut(\"sample," + EscapeGelString(String.Join(",", Names(variables))) + "\\n\");");
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
            b.AppendLine("    GEL_TextOut(\"%d,\",,,,,'GEL'::__dl_sample_count);");
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
            return b.ToString();
        }

        public static void ValidateGeneratedScript(string script, int variableCount)
        {
            if (String.IsNullOrWhiteSpace(script))
                throw new InvalidOperationException("生成结果为空。");
            if (script.Contains("\", ,,,,,"))
                throw new InvalidOperationException("生成结果包含错误的 GEL_TextOut 参数占位符。");
            if (!script.Contains("GEL_TextOut(\"%d,\",,,,,'GEL'::__dl_sample_count);"))
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
    }
}
