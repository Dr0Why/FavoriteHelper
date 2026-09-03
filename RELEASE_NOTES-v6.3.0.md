# FavoriteHelper 6.3.0

FavoriteHelper 6.3 updates the Portable Explorer-to-Photos workflow for current Windows while retaining the existing file-safety model.

## Highlights

- Added compatibility for the current Windows 11 Microsoft Photos process (`Photos.exe`) while retaining exact support for the Windows 10 `PhotosApp.exe` identity.
- Improved the non-activating notification layout so short and long messages adapt to the current Windows font and DPI without taking focus.
- Added explicit legacy shortcut Repair for eligible old Windows 10 `.lnk` files whose relative target remains valid but whose stored Shell target information is stale.
- Added batch Export for valid FavoriteHelper shortcuts. Export copies the corresponding source images without modifying the images or shortcuts and never overwrites existing output files.
- Added tray `Export...` and `Repair...` windows with explicit Explorer drag-and-drop selection followed by user confirmation.
- Added tray `Configuration...` for editing `favorite_folder_name`. The default is `Favorite`; changes affect current and future operations without migrating or scanning differently named old favorite folders.
- Retained one-shot `--export` and `--repair` command modes as advanced and compatibility entry points.
- Removed Explorer custom context-menu integration from the first v6.3 product scope. Export, Repair, and Configuration are accessed through the notification-area menu.
- Hardened Export destination creation against directory replacement races by anchoring no-overwrite creation to the already validated, non-reparse output directory.

## Platform status

- Target: Windows 10 and Windows 11 x64.
- Windows 11 current Photos (`Photos.exe`) has completed the real v6.3 runtime verification recorded in the project specification.
- Windows 10 legacy Photos (`PhotosApp.exe`) remains explicitly supported; final v6.3 physical and notification-layout regression on Windows 10 remains a release gate in the current project record.
- The release is built as a self-contained `win-x64` Portable application with no installer.

No Explorer Shell extension, custom context-menu registration, MSIX package, or installer is included.
