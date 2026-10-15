using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using NwApp = Autodesk.Navisworks.Api.Application;

namespace SpoolToSearchSets
{
    /// <summary>Content of the dockable pane. It's modeless, so the model stays usable
    /// (e.g. to change the selection) while it's open.</summary>
    internal sealed class MainPanel : UserControl
    {
        public const string Caption = "Spool To Search Sets";
        const string More = "More…";

        readonly Settings _settings = Settings.Load();
        bool _filling;

        readonly TabControl _tabs = new TabControl { Dock = DockStyle.Fill };
        readonly CheckBox _useSelection = new CheckBox { Text = "Use Selection", AutoSize = true };
        readonly CheckBox _deleteExisting = new CheckBox { Text = "Delete Existing", AutoSize = true };

        // Single tab
        readonly ComboBox _category = NewCombo(), _property = NewCombo();
        readonly TextBox _folderName = NewText(), _delimiter = NewText();
        readonly NumericUpDown _primaryIndex = NewIndex(), _secondaryIndex = NewIndex();

        // Multi-Conditions tab
        readonly ComboBox _folderCategory = NewCombo(), _folderProperty = NewCombo();
        readonly ComboBox _setCategory = NewCombo(), _setProperty = NewCombo();
        readonly TextBox _multiFolderName = NewText();

        // Always the current document: the pane outlives any one opened file.
        static Document Doc => NwApp.ActiveDocument;

        public MainPanel()
        {

            var single = NewTable();
            AddRow(single, "Category", _category);
            AddRow(single, "Property", _property);
            AddRow(single, "Folder Name", _folderName);
            AddRow(single, "Primary Folder delimiter", _delimiter);
            AddRow(single, "Primary Folder index", _primaryIndex);
            AddRow(single, "Secondary Folder index", _secondaryIndex);
            AddTab("Spool Sets", single);

            var multi = NewTable();
            AddRow(multi, "Folder Category", _folderCategory);
            AddRow(multi, "Folder Property", _folderProperty);
            AddRow(multi, "Set Category", _setCategory);
            AddRow(multi, "Set Property", _setProperty);
            AddRow(multi, "Folder Name", _multiFolderName);
            AddTab("Multi-Conditions", multi);

            var create = new Button { Text = "Create Sets", AutoSize = true };
            create.Click += (s, e) => Run();

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6) };
            bottom.Controls.AddRange(new Control[] { _useSelection, _deleteExisting, create });
            Controls.Add(_tabs);
            Controls.Add(bottom);

            WireCategory(_category, _property);
            WireCategory(_folderCategory, _folderProperty);
            WireCategory(_setCategory, _setProperty);
            LoadSettings();
        }

        void LoadSettings()
        {
            var s = _settings;
            Init(_category, s.Category, true); Init(_property, s.Property, false);
            Init(_folderCategory, s.MultiFolderCategory, true); Init(_folderProperty, s.MultiFolderProperty, false);
            Init(_setCategory, s.MultiSetCategory, true); Init(_setProperty, s.MultiSetProperty, false);
            _folderName.Text = s.FolderName;
            _delimiter.Text = s.Delimiter;
            _primaryIndex.Value = Clamp(s.PrimaryIndex, _primaryIndex);
            _secondaryIndex.Value = Clamp(s.SecondaryIndex, _secondaryIndex);
            _multiFolderName.Text = s.MultiFolderName;
            _useSelection.Checked = s.UseSelection;
            _deleteExisting.Checked = s.DeleteExisting;
            if (s.SelectedTab >= 0 && s.SelectedTab < _tabs.TabCount) _tabs.SelectedIndex = s.SelectedTab;
        }

        void Run()
        {
            Document doc = Doc;
            if (doc == null || doc.IsClear)
            {
                MessageBox.Show(this, "Open a model first.", Caption);
                return;
            }

            bool multi = _tabs.SelectedIndex == 1;
            string[] required = multi
                ? new[] { Value(_folderCategory), Value(_folderProperty), Value(_setCategory), Value(_setProperty) }
                : new[] { Value(_category), Value(_property) };
            if (Array.Exists(required, string.IsNullOrEmpty))
            {
                MessageBox.Show(this, "Category and Property are required.", Caption);
                return;
            }
            if (_useSelection.Checked && doc.CurrentSelection.IsEmpty)
            {
                MessageBox.Show(this, "\"Use Selection\" is checked but nothing is selected.", Caption);
                return;
            }

            SaveSettings();
            (int found, int created) result;
            Cursor = Cursors.WaitCursor;
            try
            {
                result = multi
                    ? SetBuilder.CreateMulti(doc, _useSelection.Checked,
                        Value(_folderCategory), Value(_folderProperty), Value(_setCategory), Value(_setProperty),
                        _multiFolderName.Text, _deleteExisting.Checked)
                    : SetBuilder.CreateSingle(doc, _useSelection.Checked,
                        Value(_category), Value(_property), _folderName.Text, _deleteExisting.Checked,
                        _delimiter.Text, (int)_primaryIndex.Value, (int)_secondaryIndex.Value);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally { Cursor = Cursors.Default; }

            MessageBox.Show(this, $"{result.found} unique value(s) found.\n{result.created} new Search Set(s) created.", Caption);
        }

        void SaveSettings()
        {
            var s = _settings;
            s.Category = Value(_category); s.Property = Value(_property);
            s.FolderName = _folderName.Text; s.Delimiter = _delimiter.Text;
            s.PrimaryIndex = (int)_primaryIndex.Value; s.SecondaryIndex = (int)_secondaryIndex.Value;
            s.MultiFolderCategory = Value(_folderCategory); s.MultiFolderProperty = Value(_folderProperty);
            s.MultiSetCategory = Value(_setCategory); s.MultiSetProperty = Value(_setProperty);
            s.MultiFolderName = _multiFolderName.Text;
            s.UseSelection = _useSelection.Checked; s.DeleteExisting = _deleteExisting.Checked;
            s.SelectedTab = _tabs.SelectedIndex;
            s.Save();
        }

        // Picking "More…" loads every category in the model (or selection);
        // picking a category loads its properties.
        void WireCategory(ComboBox category, ComboBox property)
        {
            category.SelectedIndexChanged += (s, e) =>
            {
                if (_filling || category.SelectedItem == null || Doc == null || Doc.IsClear) return;
                if ((string)category.SelectedItem == More)
                {
                    Fill(category, SetBuilder.CategoryNames(Doc, _useSelection.Checked), "", true);
                    category.DroppedDown = true;
                }
                else
                {
                    Fill(property, SetBuilder.PropertyNames(Doc, _useSelection.Checked, (string)category.SelectedItem),
                        property.Text, false);
                }
            };
        }

        void Fill(ComboBox box, IEnumerable<string> items, string text, bool addMore)
        {
            _filling = true;
            Cursor = Cursors.WaitCursor;
            try
            {
                box.BeginUpdate();
                box.Items.Clear();
                foreach (string item in items) box.Items.Add(item);
                if (addMore) box.Items.Add(More);
                box.Text = text;
            }
            finally
            {
                box.EndUpdate();
                Cursor = Cursors.Default;
                _filling = false;
            }
        }

        void Init(ComboBox box, string saved, bool addMore) =>
            Fill(box, string.IsNullOrEmpty(saved) ? new string[0] : new[] { saved }, saved, addMore);

        static string Value(ComboBox box)
        {
            string text = box.Text.Trim();
            return text == More ? "" : text;
        }

        static decimal Clamp(int value, NumericUpDown box) => Math.Max(box.Minimum, Math.Min(box.Maximum, value));

        void AddTab(string title, Control content)
        {
            var page = new TabPage(title) { Padding = new Padding(8) };
            page.Controls.Add(content);
            _tabs.TabPages.Add(page);
        }

        static TableLayoutPanel NewTable()
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return t;
        }

        static void AddRow(TableLayoutPanel t, string label, Control control)
        {
            control.Dock = DockStyle.Fill;
            t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, t.RowCount);
            t.Controls.Add(control, 1, t.RowCount);
            t.RowCount++;
        }

        static ComboBox NewCombo() => new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Sorted = false };
        static TextBox NewText() => new TextBox();
        // 0 = not used; otherwise the 1-based part of the delimited value.
        static NumericUpDown NewIndex() => new NumericUpDown { Minimum = 0, Maximum = 50 };
    }
}
