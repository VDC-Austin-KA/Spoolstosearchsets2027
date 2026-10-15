using System.Windows.Forms;
using Autodesk.Navisworks.Api.Plugins;
using NwApp = Autodesk.Navisworks.Api.Application;

namespace SpoolToSearchSets
{
    /// <summary>Ribbon button ("Tool add-ins 1" tab) that shows or hides the dockable pane.</summary>
    [Plugin("SpoolToSearchSets", "VDCA", DisplayName = "Spool To Search Sets",
        ToolTip = "Create and maintain Search Sets for each unique property value")]
    [AddInPlugin(AddInLocation.AddIn)]
    public class SpoolToSearchSetsPlugin : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            PluginRecord record = NwApp.Plugins.FindPlugin(SpoolToSearchSetsPane.PaneId);
            if (record == null)
            {
                MessageBox.Show("The Spool To Search Sets pane failed to register.", MainPanel.Caption);
                return 0;
            }
            if (!record.IsLoaded) record.LoadPlugin();

            if (record.LoadedPlugin is DockPanePlugin pane)
            {
                pane.Visible = !pane.Visible;
                if (pane.Visible) pane.ActivatePane();
            }
            return 0;
        }
    }

    /// <summary>Modeless, dockable pane (also listed under View > Windows).</summary>
    [Plugin("SpoolToSearchSets.Pane", "VDCA", DisplayName = "Spool To Search Sets")]
    [DockPanePlugin(420, 340, FixedSize = false, AutoScroll = true, MinimumHeight = 300, MinimumWidth = 360)]
    public class SpoolToSearchSetsPane : DockPanePlugin
    {
        public const string PaneId = "SpoolToSearchSets.Pane.VDCA";

        public override Control CreateControlPane() => new MainPanel { Dock = DockStyle.Fill };

        public override void DestroyControlPane(Control pane) => pane.Dispose();
    }
}
