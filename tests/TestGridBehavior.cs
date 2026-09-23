using System;
using System.Reflection;
using System.Windows.Forms;
using GelGenerator;

internal static class TestGridBehavior
{
    [STAThread]
    private static int Main()
    {
        using (var form = new MainForm())
        {
            var grid = (DataGridView)typeof(MainForm).GetField("grid", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            var bridgeConnection = (Label)typeof(MainForm).GetField("lblBridgeConnection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            var coreSelector = (ComboBox)typeof(MainForm).GetField("cmbCcsCorePattern", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            form.CreateControl();
            grid.CreateControl();
            int rowIndex = grid.Rows.Add("output", "name_only", "gObject.member", "float32", "十进制", false);
            if (grid.SelectionMode != DataGridViewSelectionMode.RowHeaderSelect)
                throw new Exception("Grid selection mode is not RowHeaderSelect.");
            if (!bridgeConnection.AutoSize || !bridgeConnection.Text.StartsWith("VOFA+ 波形连接："))
                throw new Exception("Bridge status label is still clipped or ambiguously named.");
            if (coreSelector.DropDownStyle != ComboBoxStyle.DropDownList || coreSelector.Text != "C28xx_CPU1" || coreSelector.Items.Count != 4)
                throw new Exception("CPU core selector is not the required fixed dropdown.");
            foreach (string expected in new[] { "C28xx_CPU1", "C28xx_CPU2", "CPU1", "CPU2" })
                if (!coreSelector.Items.Contains(expected)) throw new Exception("Missing CPU core option: " + expected);

            grid.Rows[rowIndex].Cells["DirectionColumn"].Value = "input";
            typeof(MainForm).GetMethod("ApplyDirectionState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { rowIndex });
            if (Convert.ToString(grid.Rows[rowIndex].Cells["FormatColumn"].Value) != "无" || !grid.Rows[rowIndex].Cells["FormatColumn"].ReadOnly)
                throw new Exception("Input row must use a disabled no-output format.");
            grid.Rows[rowIndex].Cells["DirectionColumn"].Value = "inout";
            typeof(MainForm).GetMethod("ApplyDirectionState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { rowIndex });
            if (grid.Rows[rowIndex].Cells["FormatColumn"].ReadOnly || Convert.ToString(grid.Rows[rowIndex].Cells["FormatColumn"].Value) != "十进制")
                throw new Exception("Inout row did not restore an editable output format.");
            grid.Rows[rowIndex].Cells["FormatColumn"].Value = "十六进制";

            grid.ClearSelection();
            grid.CurrentCell = grid.Rows[rowIndex].Cells["NameColumn"];
            grid.CurrentCell.Selected = true;
            string copied = Convert.ToString(grid.GetClipboardContent().GetData(DataFormats.Text)).Trim();
            if (copied != "name_only" || copied.IndexOf('\t') >= 0)
                throw new Exception("Single-cell copy unexpectedly included other columns: " + copied);

            grid.BeginEdit(true);
            var editor = grid.EditingControl as TextBox;
            if (editor == null) throw new Exception("Text editor was not created.");
            editor.Text = "continuous_input";
            typeof(MainForm).GetMethod("RefreshBridgeChannels", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
            if (!grid.IsCurrentCellInEditMode || editor.Text != "continuous_input")
                throw new Exception("Channel refresh ended text editing.");

            grid.EndEdit();
            grid.ClearSelection();
            grid.Rows[rowIndex].Selected = true;
            if (grid.SelectedRows.Count != 1)
                throw new Exception("Row-header style full-row selection is unavailable.");
        }
        Console.WriteLine("Grid behavior tests passed.");
        return 0;
    }
}
