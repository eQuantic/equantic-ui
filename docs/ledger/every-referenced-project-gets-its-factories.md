- **2026-11-22 · Every referenced project gets its components' factories**: only the first project of an
  app's references got them since #818, and a page composing a component of any other failed with CS0103.
  The SDK joined the names with `;` in a compiler-visible property, and an analyzer config ends a value
  at `;` or `#`, so the generator read the first name alone; joined with `,`, a name holding `#` still
  lost its factories. The names now go one a line in a file the compiler is handed with the additional
  files, which a design-time build hands it too. A real build of the SDK's shipped target, with the
  project holding the components second in the list, failed before the fix with the issue's CS0103 and
  builds after it ([#1803](https://github.com/eQuantic/equantic-ui/issues/1803)). Proposed and archived
  through OpenSpec (`openspec/specs/component-factories`).
