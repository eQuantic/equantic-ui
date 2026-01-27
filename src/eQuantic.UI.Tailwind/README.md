# eQuantic.UI.Tailwind

Tailwind CSS integration for **eQuantic.UI**.

## Overview

This package provides a `StyleBuilder` implementation compatible with Tailwind CSS utility classes. It allows you to use standard Tailwind class names in your C# components with full type safety and zero runtime overhead.

## Usage

```csharp
new Container
{
    ClassName = "p-4 bg-blue-500 text-white rounded-lg hover:bg-blue-600"
}
```
