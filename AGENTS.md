# PicDispatch Agent Notes

## Project Shape
- WPF desktop app targeting `.NET Framework 4.8` with SDK-style `PicDispatch.csproj`.
- App state is split into `Models`, `Services`, and `ViewModels`; keep window code-behind limited to WPF events, dialogs, and window chrome.
- Theme colors and shared control styles live in `App.xaml`; do not hard-code UI colors in individual windows unless adding a new theme variable first.

## Core Flow
- `SettingsService` persists `%AppData%\PicDispatch\settings.json`.
- `ImageQueueService` scans source folders in source-folder order, sorting each folder by file name.
- `ImageLoaderService` uses WPF first and falls back to Magick.NET for formats such as WebP/AVIF.
- `FileMoveService` owns move history and session-only undo.
- `ShortcutService` gates browser shortcuts by interaction state and ignores shortcuts while text input is focused.

## Settings UX
- Settings changes auto-save; do not add Save/Cancel submit flows.
- Source/target folder rows are managed from Settings with icon-only Add buttons that open the modern Shell folder picker; do not reintroduce typed path inputs for adding folders.
- Target creation must pick a folder and bind an `A-Z` or `0-9` shortcut before adding.
- Target shortcut changes reuse `ShortcutInputDialog`; click the fixed-width shortcut button to open the binding dialog.
- Shortcut buttons should use a strong key-badge background, fixed width, and monospace font so list rows align.
- Delete source/target entries from the item context menu.

## Window/UI Notes
- Main window should open centered and show an empty state in the main content area when no folders are configured; do not auto-open Settings on startup.
- Use custom window chrome, rounded corners, DWM Windows 11 styling, and the dark theme variables from `App.xaml`.
- Run normal `dotnet build` for verification unless the executable is locked by a running app instance.
