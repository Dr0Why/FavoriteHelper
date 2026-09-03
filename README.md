# FavoriteHelper

FavoriteHelper adds safe Favorite and Unfavorite shortcuts to a local-image workflow between Windows File Explorer and Microsoft Photos. Favorites are portable relative `.lnk` files stored in each image folder's `Favorite` subfolder by default. Original image contents are never modified.

## Features

- Explicitly binds an Explorer image session before operating in Microsoft Photos.
- Creates portable relative Favorite shortcuts without overwriting existing files.
- Safely removes only shortcuts that still match the selected image.
- Rejects broken, conflicting, replaced, or redirected shortcut locations.
- Provides non-activating notifications and a pink-heart notification-area icon.
- Provides tray `Export...` and `Repair...` drag-and-drop windows for explicit batch operations.
- Provides tray `Configuration...` for changing the favorite folder name.
- Runs as a Portable notification-area application with no installer.

## Default shortcuts

- **Explorer Open:** `Ctrl+Shift+P`
- **Favorite:** `Ctrl+F`
- **Unfavorite:** `Ctrl+Shift+U`

## Usage

1. Download and extract the Portable release.
2. Run `FavoriteHelper.exe`.
3. Select exactly one image in a physical File Explorer folder.
4. Press `Ctrl+Shift+P` to open it in Microsoft Photos through FavoriteHelper.
5. In Photos, use `Ctrl+F` to Favorite and `Ctrl+Shift+U` to Unfavorite.
6. Use the pink-heart notification-area menu for `Export...`, `Repair...`, `Configuration...`, or `Exit`.

Default settings are stored beside the executable in `config.json`. The `favorite_folder_name` setting defaults to `Favorite` and can be changed through `Configuration...`. A new name affects current and future operations only; differently named old favorite folders are not migrated or scanned automatically.

FavoriteHelper v6.3 does not install an Explorer custom context menu. Export and Repair are opened from the notification-area menu, then receive explicitly selected `.lnk` files through drag and drop.

## Privacy

- No telemetry or analytics.
- No upload of images or other user data.
- No production network communication.
- Diagnostic logs remain local and are bounded to `logs\app.log` and one rotated file.
- FavoriteHelper does not modify original image contents.

Logs may contain local image paths and file identities needed to diagnose exact-source and filesystem-safety failures.

## Platform status

- **Target:** Windows 10 and Windows 11 x64.
- **Windows 11:** the current Microsoft Photos process (`Photos.exe`) has completed real Source Session, navigation, Favorite/Unfavorite, relative-link, Repair, Export, and notification runtime verification recorded for v6.3.
- **Windows 10:** the legacy Photos process (`PhotosApp.exe`) remains explicitly supported. The repository still records final v6.3 physical and notification-layout regression on Windows 10 as a release gate.
- Distributed as a self-contained `win-x64` build with no installer required.
- Independent execution testing on a Windows host without separately installed .NET 8 is pending.

## Related Projects

[YandeSync](https://github.com/Dr0Why/YandeSync) — another Windows image-workflow project by the same developer.

## Support

[Buy Me a Coffee](https://buymeacoffee.com/dr0why)

See [RELEASE_NOTES-v6.3.0.md](RELEASE_NOTES-v6.3.0.md) for the current release summary and known limitations.
