using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using NwApp = Autodesk.Navisworks.Api.Application;

namespace SpoolToSearchSets
{
    // AddInLocation.AddIn places the button on the "Tool add-ins 1" ribbon tab.
    [Plugin("SpoolToSearchSets", "VDCA", DisplayName = "Spool To Search Sets",
        ToolTip = "Create and maintain Search Sets for each unique property value")]
    [AddInPlugin(AddInLocation.AddIn)]
    public class SpoolToSearchSetsPlugin : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            Document doc = NwApp.ActiveDocument;
            if (doc == null || doc.IsClear)
            {
                MessageBox.Show("Open a model first.", MainForm.Caption);
                return 0;
            }

            using (var form = new MainForm(doc))
                form.ShowDialog();
            return 0;
        }
    }
}
