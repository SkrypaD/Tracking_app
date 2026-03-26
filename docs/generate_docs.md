# Generating API Documentation

This project uses **DocFX** to generate HTML documentation from XML doc comments
embedded in the C# source code.

---

## Tools Overview

| Tool | Purpose | Standard |
|------|---------|----------|
| **DocFX** | HTML doc site from XML comments + Markdown | Microsoft standard |
| **XML Doc Comments** | Inline code documentation | C# / .NET standard |
| **dotnet-warnings** | Build-time missing-doc warnings | `<GenerateDocumentationFile>` |

### Why DocFX?

- Official Microsoft tool, integrates with .NET build system natively
- Reads the same XML comments that appear in IDE tooltips (IntelliSense)
- Combines API reference with hand-written Markdown guides in one site
- Outputs static HTML — deployable to GitHub Pages with no server

### Alternatives considered

| Tool | Notes |
|------|-------|
| Sandcastle | Legacy, Windows-only |
| Doxygen | Works for C# but better suited to C/C++ |
| Swagger/OpenAPI | For REST API endpoints only — not full code docs |

---

## Prerequisites

```bash
# Install DocFX as a global .NET tool
dotnet tool install -g docfx

# Verify installation
docfx --version
```

---

## Generate Documentation

```bash
# From the repository root:
cd docfx

# Step 1: Extract XML metadata from the .NET project
docfx metadata docfx.json

# Step 2: Build the HTML site
docfx build docfx.json

# Step 3: Preview locally (opens browser at http://localhost:8080)
docfx serve _site
```

Or run all steps in one command:
```bash
docfx docfx.json --serve
```

Output is written to `docfx/_site/`. Open `_site/index.html` in any browser.

---

## Enable Build-Time Documentation Warnings

Add this to `CartridgeApp.csproj` to get compiler warnings for undocumented
public members:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <!-- Treat missing XML comments as warnings -->
  <NoWarn>$(NoWarn);1591</NoWarn>
  <!-- To treat them as errors instead (strict mode): -->
  <!-- <TreatWarningsAsErrors>true</TreatWarningsAsErrors> -->
</PropertyGroup>
```

---

## Documentation Quality Linting

Use `dotnet-doc-detective` or review compiler output for warning CS1591
(missing XML comment for publicly visible type or member):

```bash
# Build and capture documentation warnings only
dotnet build 2>&1 | grep "CS1591"
```

Aim for zero CS1591 warnings before merging to `main`.

---

## What to Document (Standards)

### Required for all public members

```csharp
/// <summary>One-line description of what this does.</summary>
```

### Required for methods with parameters

```csharp
/// <param name="id">The unique identifier of the entity.</param>
/// <returns>Description of the return value.</returns>
/// <exception cref="KeyNotFoundException">When the entity does not exist.</exception>
```

### Required for complex classes / business logic

```csharp
/// <remarks>
/// Explain WHY, not just WHAT. Include:
/// - Non-obvious design decisions
/// - State transition diagrams (use <code> blocks)
/// - Cross-component dependencies
/// - Performance considerations
/// </remarks>
```

### Useful optional tags

| Tag | Use for |
|-----|---------|
| `<example>` | Usage examples |
| `<code>` | Code snippets inside remarks |
| `<see cref="..."/>` | Links to related types |
| `<inheritdoc/>` | Inherit docs from interface |

---

## Publish to GitHub Pages

```bash
# Build the docs
docfx docfx.json

# Copy output to docs/ folder (GitHub Pages source)
cp -r docfx/_site/* docs/

# Commit and push
git add docs/
git commit -m "docs: regenerate API documentation"
git push
```

Then enable GitHub Pages in repository Settings → Pages → Source: `docs/` folder.

---

## Keeping Documentation Up-to-Date

Documentation is **not a one-time task**. Follow these rules:

1. **Every new public method** must have at minimum a `<summary>` tag before the PR is merged.
2. **Every modified method signature** must have its doc comment updated in the same commit.
3. **Run `docfx` locally** and review the output before pushing documentation changes.
4. The CI pipeline checks for CS1591 warnings and **fails the build** if any are found.