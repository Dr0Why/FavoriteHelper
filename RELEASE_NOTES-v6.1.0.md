# FavoriteHelper 6.1.0

Initial user-controlled Portable release.

## Highlights

- Explicit Explorer-to-Photos source sessions with `Ctrl+Shift+P`.
- Safe Favorite (`Ctrl+F`) and Unfavorite (`Ctrl+Shift+U`).
- Relative `.lnk` favorites survive supported directory relocation.
- Four-state shortcut classification with no-overwrite and TOCTOU protections.
- Reparse-point rejection and serialized, trigger-bound filesystem operations.
- Non-activating visible notifications and a pink-heart application/tray icon.
- App-local configuration and bounded diagnostic logs.

## Platform status

- Windows 10 x64: verified.
- Windows 11 x64: target compatibility; runtime verification pending.
- The release is a self-contained `win-x64` build by configuration. Execution on a Windows host without a separately installed .NET 8 runtime has not yet been independently exercised.

No installer is included. Extract the ZIP and run `FavoriteHelper.exe`.
