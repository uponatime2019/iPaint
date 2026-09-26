# Contributing to iPaint

First off — **thank you**! Whether you are fixing a bug, adding new brush types, improving canvas performance, or refining the UI, your contributions are welcome.

**iPaint** is an open-source, lightweight digital painting and sketching application built with **C#** and **WinUI 3** (Windows App SDK). It is a **pure unpackaged** desktop application: no MSIX requirements, no Microsoft Store identity, no telemetry, and zero installation friction. All artwork and settings are stored locally on your machine.

---

## 🚀 Quick Start

### Prerequisites

| Component | Minimum Requirement | Recommended |
|---|---|---|
| **OS** | Windows 10 version 1809 (Build 17763)+ | Windows 11 (Build 22000+) |
| **.NET SDK** | .NET 8.0 SDK | Latest .NET 8.0.x SDK |
| **IDE / Tools** | Visual Studio 2022 (17.10+) or VS Code | Visual Studio 2022 with **Windows App SDK / WinUI** workload |

### Clone & Build

```powershell
# Clone the repository
git clone https://github.com/uponatime2019/iPaint.git
cd iPaint

# Restore and build the project
dotnet restore
dotnet build "iPaint.csproj" -p:Platform=x64

# Run locally
dotnet run --project "iPaint.csproj" -p:Platform=x64
```

---

## 📂 Project Architecture

```
iPaint/
├── App.xaml / App.xaml.cs          # App lifecycle, unhandled exception logging
├── MainWindow.xaml / .cs           # Shell window, Mica backdrop, navigation host
├── app.manifest                    # Windows 10/11 compatibility and PerMonitorV2 DPI awareness
├── iPaint.csproj                   # Pure unpackaged project configuration (SelfContained, WinUI 3)
├── iPaint.sln                      # Visual Studio solution
├── Assets/                         # App icon, tiles, UI icons, and sample artworks
├── Controls/
│   ├── IbisColorWheel.cs           # Interactive HSB color wheel with hue ring & SV diamond
│   ├── IbisLogo.xaml(.cs)          # Vector brand logo mark
│   └── WrapGraph.cs                # Flow wrap panel control
├── Models/
│   └── PaintModels.cs              # PaintLayer, ArtworkDocument, blend modes, and history models
├── Services/
│   ├── BrushEngine.cs              # Raster stamp brush engine, pressure simulation, compositing
│   ├── ArtworkStore.cs             # Local persistence (layers PNG + metadata.json), PNG encoding
│   └── IbisSettings.cs             # App settings and built-in brush catalog
└── Views/
    ├── HomePage.xaml(.cs)          # Start screen: new canvas setup, samples, recent history
    ├── EditorPage.xaml(.cs)        # Main drawing canvas, zoom/pan scroller, pointer event routing
    ├── EditorPage.Layers.cs        # Multi-layer management, blend modes, opacity, layer clipping
    ├── EditorPage.Tools.cs         # Tool rail (Brush, Eraser, Bucket, Eyedropper, Stabilizer)
    └── EditorPage.Export.cs        # Canvas export to PNG and file save dialogs
```

---

## 🛠️ Contribution Workflow

1. **Fork the Repository**: Create your fork on GitHub and clone it locally.
2. **Create a Feature Branch**:
   ```powershell
   git checkout -b feature/my-cool-feature
   ```
3. **Make Your Changes**:
   - Keep the codebase clean, readable, and well-commented.
   - Maintain the pure unpackaged architecture (never add store-locked dependencies).
   - Ensure local data storage remains strictly within user local app data.
4. **Test Your Build**:
   ```powershell
   dotnet build "iPaint.csproj" -p:Platform=x64
   dotnet publish "iPaint.csproj" -c Release -p:Platform=x64 -o publish_test
   ```
   Verify the published standalone `.exe` launches cleanly with 0 errors.
5. **Submit a Pull Request**:
   - Provide a clear description of the problem solved or feature added.
   - Attach screenshots or GIFs for UI/UX modifications.

---

## 🐛 Reporting Bugs & Feature Requests

Found a bug or have a suggestion?
- Check existing [GitHub Issues](https://github.com/uponatime2019/iPaint/issues) to see if it's already discussed.
- If not, open a new Issue detailing:
  - Steps to reproduce
  - Expected vs. actual behavior
  - Windows version and display scale factor (DPI)
  - Log entries from `%LOCALAPPDATA%\iPaint\logs\` if applicable

Thank you for helping make **iPaint** better for everyone!
