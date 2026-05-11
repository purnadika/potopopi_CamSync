# 📸 Potopopi CamSync

**Potopopi CamSync** is a professional-grade media synchronization tool designed for photographers and enthusiasts. It bridges the gap between your camera (SD Cards, MTP devices) and [Immich](https://immich.app/), providing a seamless, automated, and AI-powered backup workflow.

![Version](https://img.shields.io/badge/version-1.4.0-green)
![Platform](https://img.shields.io/badge/platform-windows-blue)

## ✨ Key Features

- **🚀 Smart Scanning**: Incremental sync logic that only processes new files since your last backup.
- **🤖 AI Review Tab**: 
  - Dedicated interface for reviewing flagged photos (blurry/duplicates).
  - **Configurable Blur Sensitivity**: Adjust sensitivity to avoid false positives on bokeh and portrait photography, utilizing an advanced grid-based Laplacian variance method.
  - **Persistent State**: Review results stay across restarts until you allow or delete them.
  - **Local Cleanup**: Mark and delete low-quality files directly from the dashboard.
- **🦾 Robust Pipeline**:
  - **Immich Health Check**: Proactive server connectivity validation.
  - **Deduplication**: Precision perceptual hashing (pHash) for burst shot management.
  - **Hardware Aware**: Leverages NVIDIA GPUs (CUDA) or DirectML.
- **📱 Multi-Device Profiles**: Individual settings (Local folder, Immich account, Album) for every SD card or camera.
- **☁️ Immich Integration**: Parallel upload pipeline with bandwidth throttling and robust retry policies.
- **🏗️ Modern Architecture**: Clean separation between core logic (`.Core`) and UI, enabling future cross-platform expansion.

## 🏗️ Project Architecture

The project is built using a **Clean Architecture** approach:
- **`PotopopiCamSync.Core`**: A standalone .NET 10 library containing the business logic, sync engine, and platform-agnostic models.
- **`PotopopiCamSync`**: The primary Windows desktop application built with WPF.
- **`PotopopiCamSync.Tests`**: Comprehensive unit and integration testing suite using xUnit.

## 🛠️ Technology Stack

- **Framework**: .NET 10 (WPF & Class Library)
- **AI/ML**: ONNX Runtime (DirectML/CUDA), OpenCV (OpenCvSharp4)
- **Reliability**: Polly (Exponential Backoff), Microsoft Extensions Hosting
- **UI/UX**: CommunityToolkit.Mvvm, Modern WPF Aesthetics

## 🚀 Getting Started

### Prerequisites
- Windows 10/11
- .NET 10 Runtime

### Installation
1. Download the latest release from the Releases.
2. Run the executable.
3. Follow the **Setup Wizard** to configure your local backup folder and Immich credentials.

### Development
1. Clone the repository.
2. Open `PotopopiCamSync.sln` in Visual Studio 2022 or VS Code.
3. Build in `Release` mode.

## 🤝 Author
Created with <span title="AI from Antigravity and Copilot">❤️</span> by **purnadika** ([purnadika@proton.me](mailto:purnadika@proton.me))

## 📄 License
This project is for personal use and study. See the repository for details.

