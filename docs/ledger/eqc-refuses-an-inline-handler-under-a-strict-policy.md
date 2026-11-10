- **2026-11-10 · eqc refuses an inline handler under the app's policy**: the SDK's generator reads
  `UseContentSecurityPolicy` out of the app's source into `[assembly: ContentSecurityPolicy]`, and
  eqc then refuses with EQ2115 every `HtmlElement.OnClientClick` and `OnLoad` and every constant
  `DynamicElement.CustomAttributes` key that starts with `on`, which a strict policy refuses in the
  browser wherever the markup was written. It reads every source of the build, the projects the app
  references included, since a library never sees the app's `Program.cs`, and the message names the
  C# handler to write instead. The escape hatch keeps the properties, and an app without the policy
  builds as before ([#1269](https://github.com/eQuantic/equantic-ui/issues/1269)). Proposed and
  archived through OpenSpec, into `content-security`.
