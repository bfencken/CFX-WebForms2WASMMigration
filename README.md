# CFX WebForms2BlazorWASM

**CFX Commands Library** — migrates ASP.NET WebForms applications to Blazor WebAssembly.

Part of the CFX Runtime demo: a live Visual Studio 2022 migration of WingtipToys (ASP.NET 4.5.2) to a Blazor WASM 3-project solution.

(See **[MIGRATION-GUIDE.md](https://github.com/bfencken/CFX-WebForms2WASMMigration/blob/main/MIGRATION-GUIDE.md)** for step by step instructions with the option of using Claude Code for assistance)

New to CodeFactory? Check out the [CF Wiki](https://github.com/bfencken/CFX-WebForms2WASMMigration/wiki) to understand how the overall platform works!

---

## What This Is

A CodeFactory Commands Library (`.NET Framework 4.7.2`) that builds to a single `.cfx` file. Drop it into any WebForms solution root and two new commands appear in the Visual Studio context menu:

| Command | Trigger | What It Does |
|---|---|---|
| **Setup Blazor WASM Projects** | Right-click WebForms project | Scaffolds `.Client` / `.Server` / `.Shared`, migrates config, logic, static files, pages, and API layer |
| **Migrate WebForm to WASM** | Right-click any `.aspx` file | Migrates a single page to a `.razor` component on demand |

---

## Project Structure

```
/Commands/
  /Project/          SetupBlazorProject.cs
  /Document/         MigrateWebForm.cs
/Dialog/
  SetupBlazorDialog.xaml(.cs)       — collects target project names
  MigrateWebFormDialog.xaml(.cs)    — progress display
/Migration/
  MigrationContext.cs               — data carrier POCO
  MigrationEngine.cs                — orchestration + shared helpers
  MigrationEngine.AspxFiles.cs      — .aspx → .razor migration
  MigrationEngine.Config.cs         — web.config → appsettings.json
  MigrationEngine.Logic.cs          — Logic/ class migration
  MigrationEngine.StaticFiles.cs    — images + CSS → wwwroot
  MigrationEngine.ApiLayer.cs       — scaffolds API controllers
```

---

## Prerequisites

- Visual Studio 2022 (Windows)
- CodeFactory for Visual Studio extension installed + license activated
- CodeFactory SDK extension installed
- .NET Framework 4.7.2 SDK

---

## Build & Deploy

```
1. Open WebForms2BlazorWASM.csproj in Visual Studio
2. Ctrl+Shift+B — NuGet restore + compile + CFXPackager runs automatically
3. Output: bin\Debug\WebForms2BlazorWASM.cfx
4. Copy WebForms2BlazorWASM.cfx to the ROOT of your target solution folder
   (same directory as the .sln file)
5. Open (or reopen) the target solution — CFX loads automatically
```

---

## Generated Output

The commands produce a 3-project Blazor WASM solution:

| Project | Type | Contents |
|---|---|---|
| `WingtipToys.Client` | Blazor WASM, .NET 8 | `.razor` pages, `Program.cs`, `wwwroot/` |
| `WingtipToys.Server` | ASP.NET Core Web API, .NET 8 | API controllers, EF Core context, `appsettings.json` |
| `WingtipToys.Shared` | Class Library, .NET Standard 2.1 | Clean model DTOs, shared logic |

Generated output is clean — zero `using CodeFactory.*` references anywhere in `.Client`, `.Server`, or `.Shared`.

---

## CFX Compliance

This project follows the [CFX Automation Compliance Rules](CFX_Automation_Rules.pdf):

- All source generation uses `SourceFormatter` exclusively — no T4 templates
- `EnableCommandAsync` never throws (Rule 2)
- All commands are idempotent — safe to re-run (Rule 29)
- Structural decisions (routes, DI patterns, async discipline) are encoded in `MigrationEngine` — never left to runtime reasoning (Rule 30)
- Generated output contains zero CodeFactory SDK references (Rule 27)

---

## Demo Flow

See `CFX_ClaudeCode_ProjectInstructions.docx` for the full 9-beat recording script and phase gates.

---

*CFX Runtime · WebForms2BlazorWASM · Proprietary IP · Not for Distribution*
