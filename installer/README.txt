Spool To Search Sets for Autodesk Navisworks Manage / Simulate 2027
====================================================================

Install
  1. Close Navisworks.
  2. Extract this zip to any folder.
  3. Double-click Install.cmd. If Windows SmartScreen warns, choose More info > Run anyway.
  4. Start Navisworks 2027. Click "Spool To Search Sets" on the "Tool add-ins 1" ribbon tab
     to open the dockable pane. You can keep working in the model while it is open.

Finding a parameter (e.g. System Abbreviation)
  Leave Category blank and type part of the name in Property. Matches show as
  "Property  [Category]"; pick one to fill in both. With Use Selection checked, the lists
  only show properties of the selected items (select a model file to use that whole model).

  The add-in is copied to:
    %APPDATA%\Autodesk\ApplicationPlugins\SpoolToSearchSets.bundle

  If the original App Store version of Spools To Search Sets is installed, uninstall it first
  (Control Panel > Programs and Features) so two buttons don't appear.

Uninstall
  Close Navisworks and double-click Uninstall.cmd.

Manual install (instead of Install.cmd)
  Copy the SpoolToSearchSets.bundle folder to %APPDATA%\Autodesk\ApplicationPlugins\, then
  right-click Contents\v24\SpoolToSearchSets.dll > Properties and tick "Unblock" if it's shown.
