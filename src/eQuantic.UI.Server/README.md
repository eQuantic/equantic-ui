# eQuantic.UI.Server

Server-side rendering (SSR) and Server Action handling for **eQuantic.UI**.

## Overview

This package enables:

- **SSR**: Rendering components to HTML strings on the server for initial load performance and SEO.
- **Server Actions**: Handling calls from client-side components to server-side methods securely.
- **Context**: Accessing HTTP context, dependency injection services, and user identity within components.

## Usage

This package is typically used in your ASP.NET Core host project.

```csharp
builder.Services.AddEquanticUI();
```
