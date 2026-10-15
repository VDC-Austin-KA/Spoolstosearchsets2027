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

The **Spool To Search Sets** button on the **Tool add-ins 1** ribbon tab shows or hides a dockable pane.
The pane is modeless, so you can keep working in the model (for example, changing the selection for
**Use Selection**) while it's open. Click **Create Sets** to run; the pane stays open for the next run.
The pane is also listed under **View > Windows**.

## Download and install

1. Download [`dist/SpoolToSearchSets-Navisworks2027-v2.0.1.zip`](dist/SpoolToSearchSets-Navisworks2027-v2.0.1.zip).
2. Close Navisworks, extract the zip, and double-click `Install.cmd`.

The installer copies the add-in to `%AppData%\Autodesk\ApplicationPlugins\SpoolToSearchSets.bundle` for the
current user (no admin rights needed) and unblocks the downloaded DLL. `Uninstall.cmd` removes it.

Uninstall the original App Store version first so that two "Spool To Search Sets" buttons don't appear.

## Build

Requirements: the .NET SDK (6 or later) on Windows, macOS or Linux. Navisworks doesn't have to be installed:
the Navisworks 2027 API reference assemblies come from the `Speckle.Navisworks.API` NuGet package and
are used at compile time only.

```
dotnet build src/SpoolToSearchSets/SpoolToSearchSets.csproj -c Release
```

This builds `SpoolToSearchSets.dll`, copies it into `bundle/SpoolToSearchSets.bundle/Contents/v24/`, and
packs the bundle and the `installer/` scripts into `dist/SpoolToSearchSets-Navisworks2027-v<version>.zip`.

## Notes

- `PackageContents.xml` must give the `ComponentEntry` the attribute `AppType="ManagedPlugin"`, or Navisworks
  silently ignores the DLL and no ribbon button appears.

- Navisworks 2027 is release series `Nw24`. The .NET API only works within one major version, so this
  build loads only in 2027. To support another year, add a `Contents\vNN` folder and a
  `ComponentEntry` that are built against that year's API.
- Values are matched with the property's actual stored value, so numeric and boolean properties also work.
  Blank values are skipped.
