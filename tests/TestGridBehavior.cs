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
            form.CreateControl();
            grid.CreateControl();
            int rowIndex = grid.Rows.Add("name_only", "gObject.member", "float32", "十进制", false);
            if (grid.SelectionMode != DataGridViewSelectionMode.RowHeaderSelect)
                throw new Exception("Grid selection mode is not RowHeaderSelect.");

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
