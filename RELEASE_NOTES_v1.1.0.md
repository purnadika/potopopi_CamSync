# Release v1.1.0 - The "Polish & Progress" Update 🚀

This release focuses on improving the user experience, application stability, and visual feedback during the sync process. We've also addressed several architectural warnings and potential bugs to ensure a rock-solid production experience.

## What's New?

### ✨ Features & UI Improvements
*   **Real-time Progress Bar**: A new, smooth progress bar has been added to the main dashboard. It tracks both the download (from camera) and upload (to Immich) stages, giving you clear visibility into the synchronization progress.
*   **Single Instance Enforcement**: No more multiple tray icons! The app now ensures only one instance runs at a time. If you try to open it again, the existing window will automatically be brought to the foreground.
*   **Interactive Connection Testing**: When testing your Immich settings, the URL and API Key fields now provide immediate visual feedback with green/red/orange highlights based on the connection status.

### 🛠️ Stability & Bug Fixes
*   **Zero-Warning Build**: Fixed 8 compiler warnings across the project, ensuring the codebase adheres to the latest .NET standards.
*   **Critical Recursion Fix**: Resolved a hidden bug in the loading window that could cause an infinite loop and application crash.
*   **Improved Logging Architecture**: Rewrote the internal logging dependency injection to ensure all background synchronization activities are properly captured in `app.log`.
*   **Enhanced Error Handling**: Added detailed logging for WMI (USB detection) failures to help troubleshoot device connection issues.

### 🧪 Quality Assurance
*   Verified 40 unit tests across the entire sync engine and configuration logic.
*   Optimized deployment footprint (Framework-Dependent build) resulting in a lightweight ~3.6MB installer.

---
*Note: This is a minor release (v1.1.0) containing both features and hotfixes over v1.0.0.*
