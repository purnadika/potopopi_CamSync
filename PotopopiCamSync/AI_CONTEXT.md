# Potopopi CamSync - AI Context & Project Overview

## 1. What is this project? (5W + 1H)
- **Who**: Created by Purnadika for photographers/users who need to automate photo/video sync from SD cards/Cameras.
- **What**: A .NET 10 WPF desktop application that monitors device connections (MTP/SD) and syncs media to a local backup and/or Immich.
- **Where**: Syncs from external devices to a Local Folder and then to an Immich instance.
- **When**: Triggered automatically upon device detection (registered devices only) or manually via the Dashboard.
- **Why**: To provide a robust, memory-efficient, and user-friendly alternative to manual copying, specifically hardened against large media file crashes.
- **How**: Uses WMI for device monitoring, `MediaDevices` for MTP, and a Two-Stage Sync Pipeline for reliability.

## 2. Current Architecture & Implementation Details
- **Tech Stack**: .NET 10, WPF, CommunityToolkit.Mvvm, Microsoft.Extensions.Hosting (DI), Microsoft.Extensions.Logging.
- **DI & Hosting**: The app uses `IHost` to manage service lifetimes. Services are resolved via `App.ServiceProvider`.
- **Logging**: Uses `ILogger<T>` with a custom `FileLoggerProvider` that writes to `Logs/app.log` (with rotation).
- **Two-Stage Sync Pipeline**:
    1. **Stage 1**: Download file from device to Local Backup Folder.
    2. **Stage 2**: Stream file from Local Backup Folder to Immich API.
    - *Benefit*: No large files are buffered in memory; `FileStream` is used for direct streaming to avoid RAM spikes.
- **Device Management**:
    - Supports **MTP** (Cameras) and **SD Cards**.
    - Uses **Device Signatures** (IDs) to recognize registered devices.
    - Users can assign friendly names to devices in Settings.
- **UI/UX**:
    - **Dashboard**: Shows Active and Unregistered devices.
    - **Manual Sync**: Buttons to trigger sync for specific devices.
    - **Sync Local → Immich**: A dedicated button to push existing local backups to Immich.
    - **Cancellation**: A "Cancel" button appears during sync, using `CancellationToken`.
- **Testing**:
    - A test project `PotopopiCamSync.Tests` (net10.0-windows) is established.
    - Includes unit tests for Providers, Destinations, and the Orchestrator.
    - Mocking is done via `Moq`.

## 3. Key Files & Responsibilities
- `App.xaml.cs`: Bootstrapper, DI container setup, Tray icon management.
- `SyncOrchestrator.cs`: The core logic that coordinates the two-stage sync.
- `MtpDeviceProvider.cs` / `SdCardDeviceProvider.cs`: Handle file discovery and downloading from hardware.
- `ImmichSync.cs`: Handles multipart/form-data streaming upload to Immich.
- `LocalFolderSync.cs`: Handles organization of files into `yyyy-MM-dd` folder structures.
- `MainViewModel.cs`: Manages Dashboard state, logging visibility, and command execution.
- `SettingsService.cs`: Persistence for `AppConfig` and `SyncState`.

## 4. Current State & Known Issues
- **Status**: Phase 2 Refactoring complete. Core "Engine" is solid.
- **Build**: Successful (0 errors).
- **Tests**: 24 tests total. 100% passing.
- **Nullability**: ~16 warnings remain (CS8618/CS8600) in `DeviceMonitorService` and `App.xaml.cs`.

## 5. Handover: Recommended Enhancements (Next Steps)

### A. UI/UX "Premium" Polish (High Priority)
- **Typography**: Replace default fonts with **Outfit** or **Inter** (via Google Fonts).
- **Aesthetics**: Implement **Glassmorphism** or sleek dark mode gradients.
- **Animations**: Add smooth transitions and "shimmer" effects for active sync.

### B. Resilience & Safety
- **Exponential Backoff**: Implement retry policy for Immich uploads (network blips).
- **Partial Download Recovery**: Download to `.tmp` files and rename only on 100% completion to prevent corrupt backups.
- **Disk Space Check**: Verify target drive capacity before starting Stage 1.

### C. Performance
- **Parallel Pipeline**: Allow Stage 2 (Upload) to start for file A while Stage 1 (Download) is still processing file B.

### D. Features
- **Smart Cleanup**: Add "Delete from Device after successful sync" toggle.
- **Exclusion Filters**: Glob/Regex patterns for files/folders to skip.
- **Immich Albums**: specify album name per device.

### E. Distribution
- **Single-File Build**: Configure `.csproj` for `PublishSingleFile`.
- **Auto-Update**: GitHub-based version checker.

---
*License: Apache-2.0*
*Updated on: 2026-04-26*
