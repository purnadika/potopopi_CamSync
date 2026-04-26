# Potopopi CamSync 📸🚀

Potopopi CamSync is a high-performance, memory-efficient C# .NET 10 WPF application designed for photographers who need to automate their workflow. It monitors device connections (MTP Cameras or SD Cards) and seamlessly syncs media to a local backup and/or an [Immich](https://immich.app/) server.

Built with a **Two-Stage Sync Pipeline**, it is specifically engineered to handle large RAW files and 4K videos without crashing or consuming excessive system memory.

## ✨ Key Features

- **🚀 Two-Stage Sync Pipeline**: Files are first downloaded to a local staging area and then streamed directly to Immich. This ensures zero-RAM buffering and maximum reliability.
- **🔌 Plug-and-Play Detection**: Uses WMI events to instantly recognize registered cameras (via MTP) or SD cards the moment they are connected.
- **☁️ Immich Integration**: Native support for Immich's asset API, preserving original EXIF, GPS, and metadata.
- **🛡️ Robust MTP Engine**: Iterative folder traversal ensures the app never crashes even on cameras with thousands of deeply nested folders.
- **🛑 Full Cancellation Support**: Long-running syncs can be stopped at any moment with the click of a button.
- **🔄 Local → Immich Sync**: A dedicated manual feature to push your local backup library to Immich at any time.
- **📁 Smart Organization**: Automatically organizes your local backups into clean `yyyy-MM-dd` date-based folder structures.
- **💉 Dependency Injection**: Modern architecture using `Microsoft.Extensions.Hosting` for better maintainability and testability.
- **📜 Structured Logging**: Comprehensive logging to `app.log` for easy troubleshooting.

## 🛠️ Technology Stack

- **Framework**: .NET 10 (WPF)
- **Architecture**: MVVM with `CommunityToolkit.Mvvm`
- **DI Container**: `Microsoft.Extensions.Hosting`
- **Logging**: `Microsoft.Extensions.Logging` with custom File Provider
- **MTP Communication**: `MediaDevices` (Windows Portable Devices API)
- **Testing**: xUnit with `Moq` for unit testing the sync logic

## 🚀 Getting Started

### Prerequisites
- Windows 10/11
- .NET 10 SDK

### Installation & Run
1. **Clone the repository**:
   ```powershell
   git clone <repository_url>
   cd CameraSync
   ```

2. **Build and Run**:
   ```powershell
   dotnet run --project PotopopiCamSync
   ```

3. **Run Tests**:
   ```powershell
   dotnet test
   ```

### First-Time Setup
On the first launch, a **Setup Wizard** will guide you through:
1. Configuring your **Local Backup Folder** (Mandatory).
2. Setting up your **Immich URL** and **API Key** (Optional).
3. Assigning friendly names to your detected devices.

## 🧪 Testing
The project includes a dedicated test suite `PotopopiCamSync.Tests` covering:
- iterative MTP and SD Card scanning logic.
- The two-stage orchestration pipeline.
- Local folder organization and duplicate skipping.
- Immich API communication mocking.

## 🗺️ Roadmap
- [ ] **UI Polish**: Modernizing the interface with Gradients and Outfit/Inter typography.
- [ ] **Parallel Processing**: Allowing Stage 2 uploads while Stage 1 downloads are still in progress.
- [ ] **Auto-Cleanup**: Optional feature to delete files from the source device after a successful verified sync.
- [ ] **Resilience**: Implementing exponential backoff for network-related failures.

## 📄 License
This project is licensed under the MIT License - see the LICENSE file for details.