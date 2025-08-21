# DarkerConsole

A lightweight .NET tray application that toggles Windows light/dark theme with a single click.

## Project Overview

DarkerConsole is an ultra-minimal system tray application built with .NET 10 (preview!)
that provides instant Windows theme switching. It features custom high-DPI icons, toast notifications, and AOT compilation for fast startup and minimal resource usage.

## Architecture

- **Technology Stack**: .NET 10, C#, Win32 API via P/Invoke
- **UI Framework**: None (pure Win32 tray implementation)
- **Dependency Injection**: Jab (compile-time DI)
- **Build System**: MSBuild with custom targets
- **Deployment**: Self-contained AOT executable with UPX compression

## Key Features

- System tray icon that changes based on current theme
- Left-click to toggle Windows light/dark theme
- Right-click context menu with configuration options
- Toast notifications for theme changes (configurable)
- High-DPI aware with crisp multi-resolution icons
- Graceful shutdown with Ctrl+C support
- Ultra-lightweight (~1-2MB compressed executable)

## Build System

### Icon Generation

The project automatically compiles SVG icons to multi-resolution ICO files:

- Source: `src/DarkerConsole/assets/*.svg`
- Intermediate: `obj/icons/` (build artifacts)
- Output: Final executable directory
- Resolutions: 16x16, 32x32, 48x48, 256x256 pixels
- Transparent backgrounds with high-quality anti-aliasing

### Build Commands

- **Development**: `dotnet build`
- **Release**: `dotnet publish`
- **AOT Release**: `dotnet publish -p:PublishAot=true`

## Configuration

### Application Settings

- Runtime settings: `config.toml`
- No registry modifications required

### Build Configuration

- **Development**: Full debugging, no optimizations
- **Release**: Standard optimizations, ready-to-run images
- **AOT**: Maximum size/speed optimizations, native compilation

## Dependencies

### Runtime Dependencies

- **ConsoleAppFramework** (5.5.0): Command-line interface
- **Jab**: Compile-time dependency injection
- **Microsoft.Extensions.*** : Logging, configuration, options
- **Tomlyn**: TOML configuration parsing
- **PublishAotCompressed**: Automatic UPX compression

### Build Dependencies

- **ImageMagick**: SVG to ICO conversion (external tool)
- **CSharpier**: Code formatting (external tool)

## Development Workflow

### Build Process

1. CSharpier formats all C# code
2. SVG assets compiled to multi-resolution ICO files
3. Standard .NET compilation
4. AOT native code generation (if enabled)
5. UPX compression (for AOT builds)

### Icon Development

- Edit SVG files in `src/DarkerConsole/assets/`
- Icons automatically compiled on build
- High-resolution rendering (1200 DPI) with downsampling
- Transparent backgrounds supported

## Technical Details

### Win32 Integration

- Custom message window for tray icon events
- Direct Shell_NotifyIcon API usage
- High-DPI aware icon loading
- Proper message loop with thread safety

### Memory Management

- Minimal allocations in hot paths
- Proper disposal of Win32 handles
- Async/await for non-blocking operations
- Compile-time dependency injection for zero reflection

### Performance Optimizations

- AOT compilation eliminates JIT overhead
- Trimming removes unused framework code
- Size-optimized for minimal disk footprint
- Fast startup through native code generation

## Recent Improvements

1. **Fixed Ctrl+C shutdown**: Proper thread message handling
2. **High-quality icons**: Multi-resolution ICO with transparent backgrounds
3. **DPI awareness**: Manifest-based high-DPI support
4. **Build cleanup**: Icons generated to intermediate directories
5. **Warning resolution**: All AOT/trimming warnings addressed
6. **Compression**: Integrated UPX LZMA compression

## Commands to Remember

```bash
# Standard development build
dotnet build

# Release build with compression
dotnet publish -c Release -p:PublishAot=true

# Clean and rebuild icons
dotnet clean && dotnet build

# Run with console (for debugging)
dotnet run --project src/DarkerConsole -- --console
```
