using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Navisworks.Api;

namespace SpoolToSearchSets
{
    internal static class SetBuilder
    {
        // ---- Dialog population ------------------------------------------------

        public static List<string> CategoryNames(Document doc, bool useSelection) =>
            Scope(doc, useSelection)
                .SelectMany(i => i.PropertyCategories)
                .Select(c => c.DisplayName)
                .Distinct()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

        public static List<string> PropertyNames(Document doc, bool useSelection, string category) =>
            Scope(doc, useSelection)
                .Select(i => i.PropertyCategories.FindCategoryByDisplayName(category))
                .Where(c => c != null)
                .SelectMany(c => c.Properties)
                .Select(p => p.DisplayName)
                .Distinct()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

        static IEnumerable<ModelItem> Scope(Document doc, bool useSelection) =>
            useSelection
                ? doc.CurrentSelection.SelectedItems.SelectMany(i => i.DescendantsAndSelf).Distinct()
                : doc.Models.RootItemDescendantsAndSelf;

        // ---- Set creation -----------------------------------------------------

        /// <summary>One Search Set per unique value of category/property, optionally
        /// nested in Primary/Secondary folders taken from delimited parts of the value.</summary>
        public static (int found, int created) CreateSingle(Document doc, bool useSelection,
            string category, string property, string folderName, bool deleteExisting,
            string delimiter, int primaryIndex, int secondaryIndex)
        {
            string root = string.IsNullOrWhiteSpace(folderName) ? category + "-" + property : folderName.Trim();
            var values = UniqueValues(doc, useSelection, category, property);
            int created = 0;

            using (Transaction tx = doc.BeginTransaction(MainForm.Caption))
            {
                if (deleteExisting) RemoveRootFolder(doc, root);

                foreach (var kv in values.OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var path = new List<string> { root };
                    if (!string.IsNullOrEmpty(delimiter))
                    {
                        string[] parts = kv.Key.Split(new[] { delimiter }, StringSplitOptions.None);
                        AddPart(path, parts, primaryIndex);
                        AddPart(path, parts, secondaryIndex);
                    }

                    Search search = NewSearch(doc, useSelection);
                    search.SearchConditions.Add(Equal(category, property, kv.Value));
                    if (AddSet(doc, path, kv.Key, search)) created++;
                }
                tx.Commit();
            }
            return (values.Count, created);
        }

        /// <summary>A folder per unique value of the first property, containing a Search Set
        /// per unique value of the second property found within it.</summary>
        public static (int found, int created) CreateMulti(Document doc, bool useSelection,
            string folderCategory, string folderProperty, string setCategory, string setProperty,
            string folderName, bool deleteExisting)
        {
            string root = string.IsNullOrWhiteSpace(folderName)
                ? folderCategory + "-" + folderProperty + "-" + setProperty
                : folderName.Trim();
            var outer = UniqueValues(doc, useSelection, folderCategory, folderProperty);
            int found = 0, created = 0;

            using (Transaction tx = doc.BeginTransaction(MainForm.Caption))
            {
                if (deleteExisting) RemoveRootFolder(doc, root);

                foreach (var o in outer.OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var inner = UniqueValues(doc, useSelection, setCategory, setProperty,
                        Equal(folderCategory, folderProperty, o.Value));
                    found += inner.Count;

                    foreach (var i in inner.OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        Search search = NewSearch(doc, useSelection);
                        search.SearchConditions.Add(Equal(folderCategory, folderProperty, o.Value));
                        search.SearchConditions.Add(Equal(setCategory, setProperty, i.Value));
                        if (AddSet(doc, new[] { root, o.Key }, i.Key, search)) created++;
                    }
                }
                tx.Commit();
            }
            return (found, created);
        }

        // ---- Helpers ----------------------------------------------------------

        static Search NewSearch(Document doc, bool useSelection)
        {
            var search = new Search();
            if (useSelection) search.Selection.CopyFrom(doc.CurrentSelection.SelectedItems);
            else search.Selection.SelectAll();
            search.Locations = SearchLocations.DescendantsAndSelf;
            return search;
        }

        static SearchCondition Equal(string category, string property, VariantData value) =>
            SearchCondition.HasPropertyByDisplayName(category, property).EqualValue(value);

        /// <summary>Unique display text -> the property's actual value (used for the search
        /// condition so non-string properties match exactly).</summary>
        static Dictionary<string, VariantData> UniqueValues(Document doc, bool useSelection,
            string category, string property, SearchCondition filter = null)
        {
            Search search = NewSearch(doc, useSelection);
            search.SearchConditions.Add(SearchCondition.HasPropertyByDisplayName(category, property));
            if (filter != null) search.SearchConditions.Add(filter);

            var result = new Dictionary<string, VariantData>(StringComparer.Ordinal);
            foreach (ModelItem item in search.FindAll(doc, false))
            {
                DataProperty p = item.PropertyCategories.FindPropertyByDisplayName(category, property);
                if (p == null) continue;
                string text = Display(p.Value);
                if (!string.IsNullOrWhiteSpace(text) && !result.ContainsKey(text))
                    result.Add(text, p.Value);
            }
            return result;
        }

        static string Display(VariantData v)
        {
            if (v == null) return "";
            switch (v.DataType)
            {
                case VariantDataType.DisplayString: return v.ToDisplayString();
                case VariantDataType.IdentifierString: return v.ToIdentifierString();
                case VariantDataType.Int32: return v.ToInt32().ToString(CultureInfo.InvariantCulture);
                case VariantDataType.Double: return v.ToDouble().ToString(CultureInfo.InvariantCulture);
                case VariantDataType.Boolean: return v.ToBoolean().ToString();
                case VariantDataType.NamedConstant: return v.ToNamedConstant().DisplayName;
                default:
                    // VariantData.ToString() is "Type:value".
                    string s = v.ToString();
                    int colon = s.IndexOf(':');
                    return colon >= 0 ? s.Substring(colon + 1) : s;
            }
        }

        static void AddPart(List<string> path, string[] parts, int index)
        {
            if (index >= 1 && index <= parts.Length && parts[index - 1].Trim().Length > 0)
                path.Add(parts[index - 1].Trim());
        }

        /// <summary>Adds the set under the folder path unless a set of that name already
        /// exists there, so re-running only adds new values.</summary>
        static bool AddSet(Document doc, IList<string> folderPath, string name, Search search)
        {
            GroupItem folder = EnsurePath(doc, folderPath);
            if (folder.Children.Any(c => !(c is GroupItem) && c.DisplayName == name)) return false;
            doc.SelectionSets.AddCopy(folder, new SelectionSet(search) { DisplayName = name });
            return true;
        }

        static void RemoveRootFolder(Document doc, string root)
        {
            GroupItem existing = FindFolder(doc.SelectionSets.RootItem, root);
            if (existing != null) doc.SelectionSets.Remove(existing);
        }

        // Saved-item references can go stale after an edit, so each step re-walks from the root.
        static GroupItem EnsurePath(Document doc, IList<string> path)
        {
            for (int depth = 0; depth < path.Count; depth++)
            {
                GroupItem parent = Walk(doc, path, depth);
                if (FindFolder(parent, path[depth]) == null)
                    doc.SelectionSets.AddCopy(parent, new FolderItem { DisplayName = path[depth] });
            }
            return Walk(doc, path, path.Count);
        }

        static GroupItem Walk(Document doc, IList<string> path, int depth)
        {
            GroupItem g = doc.SelectionSets.RootItem;
            for (int i = 0; i < depth; i++)
                g = FindFolder(g, path[i]) ?? throw new InvalidOperationException("Folder not found: " + path[i]);
            return g;
        }

        static GroupItem FindFolder(GroupItem parent, string name) =>
            parent.Children.OfType<GroupItem>().FirstOrDefault(g => g.DisplayName == name);
    }
}
