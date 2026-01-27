# eQuantic.UI.Sdk

The MSBuild SDK that integrates eQuantic.UI into your .NET projects.

## Overview

This SDK streamlines the setup of eQuantic.UI projects by automatically configuring:

- **Build Pipeline**: Injects the `eQuantic.UI.Compiler` tasks into the build process.
- **Dependencies**: Automatically references `eQuantic.UI.Core` and `eQuantic.UI.Components`.
- **Runtime Assets**: Manages the copying of eQuantic.UI JavaScript runtime files to `wwwroot`.
- **Hot Reload**: Configures development-time monitoring.

## Usage

Add the SDK to your project file (`.csproj`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="eQuantic.UI.Sdk" Version="0.1.0" />

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>
```
