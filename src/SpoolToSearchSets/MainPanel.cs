using System;
using System.Collections.Generic;
using System.Linq;
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
        // Property list entries shown when no Category is given: "System Abbreviation  [Element]".
        const string PairOpen = "  [";

        readonly Settings _settings = Settings.Load();
        readonly ToolTip _tips = new ToolTip();

        // Category -> property names in scope; rebuilt after the model, the selection
        // (with Use Selection) or the Use Selection option changes.
        Dictionary<string, HashSet<string>> _index;
        Document _indexDoc;
        bool _indexUsesSelection;

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

            Wire(_category, _property);
            Wire(_folderCategory, _folderProperty);
            Wire(_setCategory, _setProperty);
            LoadSettings();
        }

        void LoadSettings()
        {
            var s = _settings;
            _category.Text = s.Category; _property.Text = s.Property;
            _folderCategory.Text = s.MultiFolderCategory; _folderProperty.Text = s.MultiFolderProperty;
            _setCategory.Text = s.MultiSetCategory; _setProperty.Text = s.MultiSetProperty;
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

            ResolveCategory(_category, _property);
            ResolveCategory(_folderCategory, _folderProperty);
            ResolveCategory(_setCategory, _setProperty);

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

        // Lists load when a box is opened or focused, so typing autocompletes. With Category
        // blank, Property lists every property in scope as "Property  [Category]"; picking one
        // fills in both, so a parameter can be found without knowing its category.
        void Wire(ComboBox category, ComboBox property)
        {
            category.DropDown += (s, e) => Fill(category, Index().Keys);
            category.Enter += (s, e) => Fill(category, Index().Keys);
            property.DropDown += (s, e) => Fill(property, PropertyChoices(category.Text.Trim()));
            property.Enter += (s, e) => Fill(property, PropertyChoices(category.Text.Trim()));
            property.SelectionChangeCommitted += (s, e) => BeginInvoke((Action)(() => ApplyPair(category, property)));
            property.Leave += (s, e) => ApplyPair(category, property);
            _tips.SetToolTip(property, "Leave Category blank to search every property, e.g. type \"System Abb\".");
        }

        IEnumerable<string> PropertyChoices(string category)
        {
            var index = Index();
            if (index.TryGetValue(category, out HashSet<string> props)) return props;
            return index.SelectMany(c => c.Value.Select(p => p + PairOpen + c.Key + "]"));
        }

        static void ApplyPair(ComboBox category, ComboBox property)
        {
            string text = property.Text;
            int i = text.LastIndexOf(PairOpen, StringComparison.Ordinal);
            if (i <= 0 || !text.EndsWith("]", StringComparison.Ordinal)) return;
            category.Text = text.Substring(i + PairOpen.Length, text.Length - i - PairOpen.Length - 1);
            property.Text = text.Substring(0, i);
        }

        // A property typed without a category is fine if only one category has it.
        void ResolveCategory(ComboBox category, ComboBox property)
        {
            ApplyPair(category, property);
            string name = property.Text.Trim();
            if (category.Text.Trim().Length > 0 || name.Length == 0) return;
            var owners = Index().Where(c => c.Value.Contains(name)).Select(c => c.Key).Take(2).ToList();
            if (owners.Count == 1) category.Text = owners[0];
        }

        Dictionary<string, HashSet<string>> Index()
        {
            Document doc = Doc;
            if (doc == null || doc.IsClear) return new Dictionary<string, HashSet<string>>();
            if (_index == null || doc != _indexDoc || _useSelection.Checked != _indexUsesSelection)
            {
                Watch(doc);
                Cursor = Cursors.WaitCursor;
                try { _index = SetBuilder.PropertyIndex(doc, _useSelection.Checked); }
                finally { Cursor = Cursors.Default; }
                _indexUsesSelection = _useSelection.Checked;
            }
            return _index;
        }

        void Watch(Document doc)
        {
            if (doc == _indexDoc) return;
            Unwatch();
            _indexDoc = doc;
            doc.CurrentSelection.Changed += OnSelectionChanged;
            doc.Models.CollectionChanged += OnModelsChanged;
        }

        void Unwatch()
        {
            if (_indexDoc == null) return;
            _indexDoc.CurrentSelection.Changed -= OnSelectionChanged;
            _indexDoc.Models.CollectionChanged -= OnModelsChanged;
            _indexDoc = null;
        }

        void OnSelectionChanged(object sender, EventArgs e) { if (_indexUsesSelection) _index = null; }
        void OnModelsChanged(object sender, EventArgs e) => _index = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing) { Unwatch(); _tips.Dispose(); }
            base.Dispose(disposing);
        }

        static void Fill(ComboBox box, IEnumerable<string> items)
        {
            string text = box.Text;
            box.BeginUpdate();
            try
            {
                box.Items.Clear();
                box.Items.AddRange(items.OrderBy(i => i, StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray());
                box.Text = text;
            }
            finally { box.EndUpdate(); }
        }

        static string Value(ComboBox box) => box.Text.Trim();

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

        static ComboBox NewCombo() => new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDown,
            MaxDropDownItems = 25,
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSource = AutoCompleteSource.ListItems,
        };
        static TextBox NewText() => new TextBox();
        // 0 = not used; otherwise the 1-based part of the delimited value.
        static NumericUpDown NewIndex() => new NumericUpDown { Minimum = 0, Maximum = 50 };
    }
}
