# Spool To Search Sets — Navisworks Manage 2027

A clean-room rebuild of the *Spools To Search Sets* add-in, written from the behavior described on its
[Autodesk App Store listing](https://marketplace.autodesk.com/apps/5fb2be57-3732-4b04-b899-1b809c701303),
targeting Navisworks Manage / Simulate 2027. It does not contain any of the original product's code.

## Features

- **Spool Sets tab**: one Search Set per unique value of a Category/Property, such as `CADmep+` / `Notes`.
  - **Folder Name**: the parent folder. If it's blank, the folder is named `Category-Property`.
  - **Primary/Secondary Folder**: splits each value on **delimiter**, then nests sets in folders named after
    the 1-based **index** part. For example, with `4-CPWR-12003-CS-001`, delimiter `-`, primary index 2 and
    secondary index 3, the set goes in `CPWR\12003`. An index of 0 means that folder level isn't used.
- **Multi-Conditions tab**: a folder per value of the first Category/Property, with a set per value of the
  second Category/Property inside it.
- **Category "More…"** loads every category in the model, or in the selection when **Use Selection** is checked.
- **Use Selection** limits the search scope of the sets to the current selection.
- **Re-running** with the same settings only adds sets that don't exist yet.
  **Delete Existing** rebuilds the parent folder, which removes stale sets.
- The last-used settings are saved to `%AppData%\SpoolToSearchSets\settings.xml`.

The button appears on the **Tool add-ins 1** ribbon tab.

## Build

Requirements: Windows, Navisworks Manage (or Simulate) 2027, and Visual Studio 2022 or the .NET SDK
with the .NET Framework 4.8 targeting pack.

```
dotnet build src/SpoolToSearchSets/SpoolToSearchSets.csproj -c Release
```

If Navisworks isn't installed in the default location, add
`-p:NavisworksDir="<install folder>"`. The build copies `SpoolToSearchSets.dll` into
`bundle/SpoolToSearchSets.bundle/Contents/v24/`.

## Install

Copy the `bundle/SpoolToSearchSets.bundle` folder to `%AppData%\Autodesk\ApplicationPlugins\`
(for the current user) or `C:\ProgramData\Autodesk\ApplicationPlugins\` (for all users), then restart Navisworks.

Uninstall the original App Store version first so that two "Spool To Search Sets" buttons don't appear.

## Notes

- Navisworks 2027 is release series `Nw24`. The .NET API only works within one major version, so this
  build loads only in 2027. To support another year, add a `Contents\vNN` folder and a
  `ComponentEntry` that are built against that year's API.
- Values are matched with the property's actual stored value, so numeric and boolean properties also work.
  Blank values are skipped.
