# Building hasheous-taskrunner

## Self-Contained Executables

The hasheous-taskrunner can be built as a self-contained, single-file executable that includes all dependencies (including the .NET runtime). Users do not need to install .NET on their systems.

## Building for Different Platforms

### Linux x64
```bash
dotnet publish -c Release -r linux-x64 --self-contained
```

### Linux ARM64 (e.g., Raspberry Pi)
```bash
dotnet publish -c Release -r linux-arm64 --self-contained
```

### Windows x64
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

### Windows ARM64
```bash
dotnet publish -c Release -r win-arm64 --self-contained
```

### macOS x64 (Intel)
```bash
dotnet publish -c Release -r osx-x64 --self-contained
```

### macOS ARM64 (Apple Silicon)
```bash
dotnet publish -c Release -r osx-arm64 --self-contained
```

## Output Location

The self-contained executable will be located at:
```
bin/Release/net8.0/<runtime-identifier>/publish/
```

For example:
- Linux: `bin/Release/net8.0/linux-x64/publish/hasheous-taskrunner`
- Windows: `bin/Release/net8.0/win-x64/publish/hasheous-taskrunner.exe`
- macOS: `bin/Release/net8.0/osx-x64/publish/hasheous-taskrunner`

## Build Configuration

The project is configured with the following settings in `hasheous-taskrunner.csproj`:

- **PublishSingleFile**: Bundles everything into one executable
- **SelfContained**: Includes the .NET runtime (no installation required)
- **PublishReadyToRun**: Pre-compiles for faster startup
- **IncludeNativeLibrariesForSelfExtract**: Bundles native dependencies
- **EnableCompressionInSingleFile**: Compresses the bundle to reduce size

## Expected File Sizes

Approximate sizes for self-contained builds (without trimming):
- Linux x64: ~38 MB
- Linux ARM64: ~36 MB
- Windows x64: ~40 MB
- macOS x64: ~38 MB
- macOS ARM64: ~36 MB

Note: Sizes may vary slightly based on .NET runtime version and dependencies.

## Framework-Dependent Build (Requires .NET Runtime)

If you prefer smaller executables and have .NET 8.0 runtime installed:

```bash
dotnet publish -c Release --no-self-contained
```

This will produce a much smaller executable (~500KB) but requires .NET 8.0 runtime to be installed on the target system.

## Troubleshooting

### Trimming Disabled
Code trimming is disabled due to extensive use of reflection in the Capabilities and Tasks systems. Enabling trimming may cause runtime errors.

### Platform-Specific Notes

**Linux**: The executable will have execute permissions set automatically.

**macOS**: You may need to remove the quarantine attribute after download:
```bash
xattr -d com.apple.quarantine hasheous-taskrunner
```

**Windows**: The executable may be flagged by Windows Defender. You may need to add an exclusion.
