# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```powershell
dotnet build          # compile
dotnet run            # compile + launch (WinForms — opens a window)
```

To launch without blocking the terminal:
```powershell
Start-Process pwsh -ArgumentList "-Command dotnet run"
```

There are no tests and no linter configured.

## Architecture

Single-assembly WinForms app (.NET 10, Windows only). Entry point is `Program.cs` → `MainForm`.

**Data flow:**
1. `ScanForm` collects directories + options → `DirectoryScanner.ScanAsync` computes SHA256 for every file and writes to DB via `FileRepository`
2. `MainForm` calls `FileRepository.GetDuplicateGroups()` to load duplicate groups (files sharing the same SHA256)
3. Groups are displayed pair-by-pair in `ImageComparePanel` (reference vs duplicate)
4. Deleting a file calls `RecycleBinService.SendToRecycleBin` (Win32 `SHFileOperation`) then removes the record from the DB

**Database:** SQLite via `Microsoft.Data.Sqlite`, stored at `%AppData%\DuplicateFinder\store.db`. Two tables: `Crc` (unique SHA256 entries) and `Fichier` (file paths with FK to `Crc`). Duplicate detection is a `GROUP BY CrcId HAVING COUNT(*) > 1` query joined with `Crc`.

**Key design points:**
- `FileRepository` holds a single persistent `SqliteConnection` (opened in constructor, disposed with the form)
- `UpsertFile` uses `INSERT … ON CONFLICT(Id) DO UPDATE` — file identity is by `Id` (Guid), not path
- `DirectoryScanner.ScanAsync` enumerates files on the calling thread, then hashes inside a nested `Task.Run`; progress is reported via `IProgress<ScanProgress>` marshalled back to the UI thread with `Invoke`
- `ProgressForm` owns the `CancellationTokenSource`; it must be initialised in the constructor (not `OnLoad`) because the scan `Task.Run` starts before `ShowDialog` triggers `OnLoad`
- `ImageComparePanel` loads images without locking the file (`FileStream` + `new Bitmap(stream)`, then stream is disposed)
- `RecycleBinService` requires a double null-terminator (`path + "\0\0"`) as mandated by `SHFileOperation`
