# Linting Guide

## 1. Overview

Linting is the process of analyzing source code to identify potential errors, enforce coding standards, and improve overall code quality. In this project, linting is used to maintain consistency, reduce bugs, and ensure readability across the codebase.

This document describes the tools, rules, and workflows used for linting.

---

## 2. Goals

* Enforce consistent coding style
* Detect common programming errors early
* Improve code readability and maintainability
* Ensure adherence to best practices
* Automate code quality checks in development and CI

---

## 3. Tools Used

Depending on the project stack, the following tools are used:

### Backend (.NET)

* **StyleCop Analyzers**
* **Roslyn Analyzers (built-in)**
* **dotnet format**

### Frontend (if applicable)

* **ESLint**
* **Prettier**

---

## 4. Configuration

### 4.1 .NET Configuration

Linting rules for .NET are defined in:

* `.editorconfig`
* `stylecop.json` (if present)

Example `.editorconfig`:

```ini
root = true

[*.cs]
indent_style = space
indent_size = 4

dotnet_sort_system_directives_first = true

dotnet_diagnostic.IDE0005.severity = warning
dotnet_diagnostic.SA1200.severity = warning
```



## 5. Running Linters

### Backend (.NET)

Run analyzers and formatting:

```bash
dotnet build
dotnet format
```

---

### Frontend

```bash
npm run lint
```

---

## 6. Common Rules

### .NET

* Remove unused `using` statements
* Place `using` directives inside namespace
* Keep consistent formatting (spacing, indentation)
* Avoid unused variables
* Follow naming conventions:

  * PascalCase for classes and methods
  * camelCase for local variables

---

## 7. CI Integration

Linting is executed automatically in the CI pipeline.

Example (GitHub Actions):

```yaml
- name: Run .NET lint
  run: dotnet build --no-restore

- name: Format check
  run: dotnet format --verify-no-changes
```

---

## 8. Fixing Issues

### Automatic fixes

```bash
dotnet format
```

or:

```bash
npm run lint -- --fix
```

---

### Manual fixes

* Add missing properties or types
* Remove unused imports
* Refactor code according to warnings

---

## 9. Rule Severity

| Level   | Description                           |
| ------- | ------------------------------------- |
| Error   | Must be fixed, blocks build           |
| Warning | Should be fixed, does not block build |
| Info    | Recommendation only                   |

---
