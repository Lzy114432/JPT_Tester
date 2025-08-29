# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build all target frameworks
dotnet build

# Build specific framework
dotnet build -f net461
dotnet build -f netcoreapp3.1
dotnet build -f net6.0-windows

# Clean build
dotnet clean && dotnet build
```

## Architecture Overview

This is a layered IO control library implementing a three-layer architecture for Digital Input (DI) and two-layer architecture for Digital Output (DO).

### DI Architecture (3 layers)
- **Hardware Layer**: Physical IO via IOC0640 driver or PLC communication
- **Mapping Layer**: Logical-to-physical address mapping with NO/NC signal conversion
- **Simulation Layer**: Debug override for testing without hardware

### DO Architecture (2 layers)  
- **Mapping Layer**: Logical-to-physical mapping with signal conversion
- **Hardware Layer**: Direct hardware control

## Folder Structure

```
IOLibrary/
├── Core/                     # Core functionality
│   ├── Interfaces/          # All interfaces
│   ├── Models/              # Data models and enums
│   └── Layered/             # Layered IO system
├── Hardware/                # Hardware implementations
│   ├── Mitsubishi/          # Mitsubishi PLC support
│   └── IOC0640/             # IOC0640 board support
├── Extensions/              # Extension features
│   └── FluentAPI/          # Fluent API extensions
└── Documentation/           # Documentation files
```

## Key Features

- **Factory Pattern**: Flexible hardware abstraction
- **IO Mapping**: Configurable logical-to-physical mapping
- **Simulation Mode**: ForceOn/ForceOff/None for debugging
- **Edge Detection**: Rising and falling edge detection
- **Multi-Hardware Support**: PLC, IOC0640, and extensible