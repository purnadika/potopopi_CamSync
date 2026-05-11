# 🚀 Potopopi CamSync v1.3.1 - Architectural Modernization

This version marks a major architectural milestone: the decoupling of the core sync engine from the WPF user interface.

### 🏗️ Architectural Changes
- **Core Library Extraction**: Successfully moved all business logic, models, and sync services into a standalone `.NET 10` Class Library (`PotopopiCamSync.Core`).
- **Clean Architecture Implementation**:
    - The core engine is now platform-agnostic, referencing no UI or hardware-specific Windows namespaces.
    - Established clear interfaces (`IDeviceProvider`, `IMediaAnalyzer`, `ISyncDestination`) to allow for future native mobile and cross-platform implementations.
- **Improved Dependency Injection**: Unified the service container across the library and application for better testability and maintainability.

### 🦾 Stability & Refactoring
- **WMI Mapping Decoupling**: Refactored hardware monitoring logic to keep Windows-specific management objects out of the Core models.
- **UI Dependency Cleanup**: Removed all direct UI calls (tray notifications, dispatchers) from the background sync services, replacing them with event-based communication.

---
*Note: This is an internal developmental release focusing on the groundwork for the native Android port.*
