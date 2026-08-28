# FavoriteHelper

FavoriteHelper adds safe Favorite and Unfavorite shortcuts to a local-image workflow between Windows File Explorer and Microsoft Photos. Favorites are portable relative `.lnk` files stored in each image folder's `收藏` subfolder. Original image contents are never modified.

## Features

- Explicitly binds an Explorer image session before operating in Microsoft Photos.
- Creates portable relative Favorite shortcuts without overwriting existing files.
- Safely removes only shortcuts that still match the selected image.
- Rejects broken, conflicting, replaced, or redirected shortcut locations.
- Provides non-activating notifications and a pink-heart notification-area icon.
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
6. Exit through the pink-heart notification-area icon.

Default settings are stored beside the executable in `config.json`.

## Privacy

- No telemetry or analytics.
- No upload of images or other user data.
- No production network communication.
- Diagnostic logs remain local and are bounded to `logs\app.log` and one rotated file.
- FavoriteHelper does not modify original image contents.

Logs may contain local image paths and file identities needed to diagnose exact-source and filesystem-safety failures.

## Platform status

- **Windows 10 x64:** runtime tested.
- **Windows 11 x64:** target compatibility; runtime verification pending.
- Distributed as a self-contained `win-x64` build with no installer required.
- Independent execution testing on a Windows host without separately installed .NET 8 is pending.

## Related Projects

[YandeSync](https://github.com/Dr0Why/YandeSync) — another Windows image-workflow project by the same developer.

## Support

[Buy Me a Coffee](https://buymeacoffee.com/dr0why)

See [RELEASE_NOTES-v6.1.0.md](RELEASE_NOTES-v6.1.0.md) for the release summary and known limitations.
