# Potopopi CamSync

Potopopi CamSync is a C# .NET 10 WPF application designed to automatically detect your photography devices (like a Canon EOS 6D or an SD Card) via USB and seamlessly sync your media files. It backups your photos locally and optionally uploads them directly to an Immich server, preserving all original EXIF, GPS, and metadata.

## Features

- **Automatic Device Detection**: Sits quietly in your system tray and instantly recognizes when your registered camera or SD card is inserted using WMI events.
- **MTP & SD Card Support**: Connects to digital cameras directly over MTP (using `MediaDevices`) or standard removable drives.
- **Local Backup & Immich Integration**: Copies photos locally to date-based folders and asynchronously uploads them to an Immich server via its REST API (`POST /api/assets`).
- **State Tracking**: Never uploads the same photo twice! A local state tracker keeps a signature of successfully uploaded files.
- **Setup Wizard & Configuration**: Intuitive UI to handle your settings, API keys, and file cleanup options (such as deleting files after a successful sync or keeping them for X days).
- **Extensible Architecture**: Built heavily around abstract `IDeviceProvider` and `ISyncDestination` interfaces, making it trivial to add new backup destinations like Google Photos, OneDrive, etc.

## Technology Stack

- **.NET 10**: The latest LTS framework for speed and stability.
- **WPF (Windows Presentation Foundation)**: For robust desktop UI.
- **CommunityToolkit.Mvvm**: For modern and efficient MVVM patterns.
- **MediaDevices**: A wrapper over the Windows Portable Devices API for flawless MTP communication.

## Getting Started

1. **Clone the repository**:
   ```bash
   git clone <repository_url>
   cd CameraSync/PotopopiCamSync
   ```

2. **Run the Application**:
   ```bash
   dotnet run
   ```

3. **First-time Setup**: 
   On your very first run, you will be greeted with a Setup Wizard. Here you can configure your Local Backup folder and your Immich API credentials.

4. **Syncing**:
   Leave the app running (it will live in your system tray). Plug in your Canon EOS 6D or insert an SD card. The application will detect the device, scan the `DCIM` folder, and automatically start pushing files to your local folder and Immich instance!

## License

This project is licensed under the MIT License - see the LICENSE file for details.