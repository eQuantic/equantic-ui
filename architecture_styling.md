# eQuantic.UI Styling Architecture

## Core Philosophy

**eQuantic.UI** aims to provide a **Flutter-inspired** developer experience (DX) while leveraging the full power of the **Modern Web** platform.

The styling architecture is built on three pillars:

1.  **Abstraction**: Components define _what_ to style, not _how_.
2.  **Flexibility**: Tailwind CSS is the "Happy Path", but not the only path.
3.  **Performance**: Compilation-time class generation, minimizing runtime CSS-in-JS overhead.

---

## 1. The Core Abstraction (`eQuantic.UI.Core`)

At the core, every visual component inherits from `HtmlElement`, exposing two standard properties:

```csharp
public abstract class HtmlElement : Component {
    /// <summary>
    /// Raw CSS classes (space-separated).
    /// </summary>
    public string ClassName { get; set; }

    /// <summary>
    /// Inline styles for dynamic values (e.g., coordinates, colors from DB).
    /// </summary>
    public Dictionary<string, string> Style { get; set; }
}
```

This simple contract means **eQuantic.UI does not enforce any CSS framework**. It simply renders HTML attributes.

### The `Theme` Concept

To support multiple frameworks, we propose an `ITheme` interface in the Code:

```csharp
public interface ITheme {
    string PrimaryButton { get; }
    string Card { get; }
    string Input { get; }
}
```

---

## 2. Tailwind CSS: The Default Implementation

We recommend **Tailwind CSS v4** as the standard implementation due to its utility-first nature, which aligns perfectly with component composition.

### Integration Flow

1.  **Compiler**: Transpiles C# `ClassName="p-4"` directly to JS/HTML.
2.  **Tailwind CLI**: Scans the **output folder** (`wwwroot/_equantic/**/*.js`) for class names.
3.  **Browser**: Receives an optimized `.css` file.

### Helper Package: `eQuantic.UI.Tailwind` (Proposed)

Instead of hardcoding strings, users can use a helper library:

```csharp
using static eQuantic.UI.Tailwind.Utility;

new Button {
    // Type-safe(ish) helpers
    ClassName = Flex.Row + Gap(4) + Bg.Blue500 + Text.White
};
```

_Note: Strings are still preferred for developers familiar with Tailwind semantics._

### 2.1 Installation & Setup Guide

To use Tailwind with eQuantic.UI in a .NET project:

#### 1. Install Tailwind (via NPM)

Run this in your web project root (e.g., adjacent to `Program.cs`):

```bash
npm install -D tailwindcss@next @tailwindcss/cli
```

#### 2. Configure CSS Input

Create `src/styles.css`:

```css
@import "tailwindcss";

@theme {
    --font-family-sans: "Inter", "sans-serif";
    --color-primary: #3b82f6;
}
```

#### 3. Update Build Pipeline (csproj)

Add a target to your `.csproj` to run the Tailwind CLI during build. This ensures the output CSS is generated based on the _compiled JS_ files.

```xml
<Target Name="BuildCSS" BeforeTargets="Build">
    <!-- Scan the compiled output directory for class names -->
    <Exec Command="npx @tailwindcss/cli -i ./src/styles.css -o ./wwwroot/css/app.css --content './wwwroot/_equantic/**/*.js'" />
</Target>
```

#### 4. Reference in HTML

In your `index.html` (served by the backend):

```html
<link href="/css/app.css" rel="stylesheet" />
```

> [!TIP]
> **Magic Automation**: In the final release, Step 3 will be automated by the `eQuantic.UI.Build` NuGet package, which will include pre-defined MSBuild targets. The user will only need to run `npm install` and define the `<UseTailwind>true</UseTailwind>` property.

---

## 3. Extensibility & Other Frameworks

Because `ClassName` is just a string, adapting to other frameworks is trivial. The user can create extension methods or a dedicated theme package.

### Example: Bootstrap 5

A user or a community package (`eQuantic.UI.Bootstrap`) could provide:

```csharp
public static class BootstrapTheme {
    public const string BtnPrimary = "btn btn-primary";
    public const string Card = "card p-3";
}

// Usage
new Button { ClassName = BootstrapTheme.BtnPrimary, Text = "Click Me" }
```

### Example: Custom / Legacy CSS

For legacy projects, standard CSS classes work natively:

```csharp
new Container { ClassName = "my-legacy-sidebar" }
```

---

## 4. Proposed Package Structure

To maintain separation of concerns, we suggest splitting the styling helpers:

| Package                   | Purpose                                                                       |
| :------------------------ | :---------------------------------------------------------------------------- |
| **eQuantic.UI.Core**      | Base definitions, `HtmlElement`, `ITheme` interface.                          |
| **eQuantic.UI.Tailwind**  | (Recommended) Helpers, predefined design system matching basic specifictaion. |
| **eQuantic.UI.Bootstrap** | (Optional) Token mapping for Bootstrap classes.                               |
| **eQuantic.UI.Material**  | (Optional) Implementation of Material Design using CSS variables/classes.     |

## 5. Global Styling & Configuration

### Application Entry Point

The specific theme implementation is configured at startup (conceptually), although mostly it's a compile-time decision of which CSS file to include in `index.html`.

In `Program.cs` or `App.cs`:

```csharp
// Defines standard "semantic" tokens for the app
public static class AppTheme {
    public static string Primary => "bg-indigo-600 hover:bg-indigo-700 text-white"; // Tailwind impl
    // OR
    // public static string Primary => "btn btn-primary"; // Bootstrap impl
}
```

### Component Composition

Developers are encouraged to create their own specialized components rather than repeating styles:

```csharp
public class PrimaryButton : Button {
    public PrimaryButton() {
        ClassName = AppTheme.Primary + " px-4 py-2 rounded shadow";
    }
}
```

This brings the **"Component-First"** mindset of React/Flutter to the styling layer.
