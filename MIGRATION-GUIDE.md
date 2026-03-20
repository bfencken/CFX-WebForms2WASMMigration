# Migration Guide: WebForms → Blazor WASM

> **Complete walkthrough** for running this demo against the WingtipToys reference application — from prerequisites through a fully verified, running Blazor WebAssembly target.

---

## How to Read This Guide

Each step is tagged with who performs it:

- **`[HUMAN]`** — Action required in Visual Studio or a browser. Cannot be automated.
- **`[CLI]`** — Command-line action. Can be run by Claude Code or manually in a terminal.
- **`[GATE]`** — A mandatory pass/fail check. Do not proceed past a failing gate.

The phases must be completed in order. Attempting to run CFX automation before the source app is verified working produces confusing failures that are hard to diagnose.

---

## Table of Contents

- [Phase 0 — Prerequisites](#phase-0--prerequisites)
- [Phase 1 — Build the Source App](#phase-1--build-the-source-app)
- [Phase 2 — Build the Commands Library](#phase-2--build-the-commands-library-skip-if-using-pre-built-cfx)
- [Phase 3 — Deploy the .cfx Package](#phase-3--deploy-the-cfx-package)
- [Phase 4 — Run the CFX Automation](#phase-4--run-the-cfx-automation)
- [Phase 5 — Verify the Output](#phase-5--verify-the-output)
- [Final Verification Checklist](#final-verification-checklist)

---

## Phase 0 — Prerequisites

### Machine Requirements

| Requirement | Detail | Notes |
|---|---|---|
| Visual Studio 2022 (v17.x+) | Windows only | Mac is not supported — CFX runs inside the VS process |
| CFX Runtime extension | VS Marketplace → "CodeFactory for Windows" | The engine that executes commands. Requires a license key. |
| CFX License Key | [codefactory.software](https://codefactory.software) | 90-day free trial available |
| .NET Framework 4.8 targeting pack | VS Installer → Individual components | Required for the Commands Library project |
| .NET 8 SDK | [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download) | Required for the three generated WASM projects |
| SQL Server LocalDB | VS Installer → Data Storage and Processing workload | Required for WingtipToys EF6 database |

> **CFX Runtime vs. CFX SDK — two separate installs:**
> The **Runtime** extension runs commands inside VS (required to *use* the package). The **SDK** extension adds the Commands Library project template and the CFXPackager build tool (required only to *modify and rebuild* the package). If you're running the demo only, you need the Runtime. If you want to modify the automation, you need both.

### `[CLI]` Clone the Reference App

```bash
git clone https://github.com/corn-pivotal/wingtiptoys
```

Download `WebForms2BlazorWASMCommands.cfx` from [Releases](https://github.com/bfencken/CFX-WebForms2WASMMigration/releases). Keep it handy — you'll deploy it in Phase 3.

---

## Phase 1 — Build the Source App

The WingtipToys WebForms application must be running and verified before CFX can migrate it.

### `[HUMAN]` Open and Restore NuGet Packages

Open `WingtipToys.sln` in Visual Studio 2022. Right-click the solution → **Restore NuGet Packages**. The following packages will be restored automatically:

```
EntityFramework 6.4.4
Microsoft.AspNet.Web.Optimization 1.1.3
WebGrease 1.6.0
Newtonsoft.Json 13.x (pulled transitively)
```

### `[HUMAN]` Build and Verify the Connection String

Build the solution (`Ctrl+Shift+B`). Confirm zero errors. Verify `web.config` contains a LocalDB connection string:

```xml
<connectionStrings>
  <add name="WingtipToys"
       connectionString="Data Source=(LocalDb)\MSSQLLocalDB;Initial Catalog=WingtipToys;
                         Integrated Security=True;MultipleActiveResultSets=True"
       providerName="System.Data.SqlClient" />
</connectionStrings>
```

### `[HUMAN]` Run and Verify All Page Flows

Press `F5`. EF6 creates and seeds the database on first run. Walk through every flow:

- [ ] Home page shows 3 product categories
- [ ] Category → ProductList shows products (8 products total)
- [ ] ProductDetails shows a single product with "Add to Cart"
- [ ] Cart shows added items with update/remove controls
- [ ] Checkout accepts order form submission
- [ ] OrderConfirmation shows the order ID

### `[GATE]` Phase 1 Gate

> **F5 runs. All 7 page flows work. 8 products visible. Checkout completes and shows order confirmation.**
>
> If any flow is broken, diagnose and fix it before proceeding. A broken source app produces confusing migration failures.

---

## Phase 2 — Build the Commands Library *(Skip if using pre-built .cfx)*

> **If you only want to run the demo** (not modify the automation), skip this phase entirely. Use the pre-built `WebForms2BlazorWASMCommands.cfx` from [Releases](https://github.com/bfencken/CFX-WebForms2WASMMigration/releases) and go straight to Phase 3.

### `[HUMAN]` Install the CFX SDK Extension

If not already installed, search the VS Marketplace for **"CodeFactory for Windows SDK"**. This adds the Commands Library project template and the CFXPackager post-build tool. Do not confuse this with the Runtime extension — they are separate installs.

### `[HUMAN]` Open the Commands Library

Extract `files.zip` from this repo and open `WebForms2BlazorWASMCommands.sln` in Visual Studio 2022.

> **Expected: errors before first build.** The project references `CodeFactorySDK` via NuGet. These references won't resolve until the first build triggers NuGet restore. Red squiggles and initial build errors are normal and documented behavior. Build first — errors will clear.

### `[HUMAN]` Build

Press `Ctrl+Shift+B`. The build will:
1. Trigger NuGet restore for `CodeFactorySDK 1.23053.1` and `CodeFactory.Formatting.CSharp`
2. Compile all command, engine, dialog, and generator classes
3. Run `CFXPackager` as a post-build step

After a successful build, verify the output file exists:
```
bin/Debug/WebForms2BlazorWASMCommands.cfx
```

> If the packager step fails, verify the **CFX SDK extension** is installed (not just the Runtime). The packager is part of the SDK extension.

### `[GATE]` Phase 2 Gate

> **Build exits with zero errors. `WebForms2BlazorWASMCommands.cfx` exists in `bin/Debug/`. CFXPackager completed without error (check the Build Output window).**

---

## Phase 3 — Deploy the .cfx Package

### `[HUMAN]` Copy .cfx to the Solution Root

Copy `WebForms2BlazorWASMCommands.cfx` to the **root folder of the WingtipToys solution** — the same directory that contains `WingtipToys.sln`:

```
WingtipToys/
├── WingtipToys.sln
├── WebForms2BlazorWASMCommands.cfx   ← place it here
└── WingtipToys/
    └── (project files)
```

### `[HUMAN]` Close and Reopen the Solution

Close the WingtipToys solution completely. Reopen `WingtipToys.sln`. The CFX Runtime loads the package automatically on solution open.

Watch the **CodeFactory** tab of the Visual Studio Output window. You should see:
```
WebForms2BlazorWASM commands loaded.
```

> **Tip:** Pin the CodeFactory output pane to the bottom of VS before running the migration. It must be visible when you run the Setup command so you can watch the 7-step migration progress in real time.

### `[HUMAN]` Verify the Context Menu

Right-click the **WingtipToys project node** in Solution Explorer. The context menu should include **"Setup Blazor WASM Projects"**.

> **Command not appearing?** Check: (1) the `.cfx` file is in the solution root, not a subfolder; (2) the solution was fully closed and reopened after placing the file; (3) the CodeFactory output window shows the load confirmation. If the load message doesn't appear, the CFX Runtime extension may not be installed or activated.

### `[GATE]` Phase 3 Gate

> **Solution reopened. Output window shows "commands loaded." Right-clicking the WingtipToys project shows "Setup Blazor WASM Projects" in the context menu.**

---

## Phase 4 — Run the CFX Automation

> **This phase runs entirely inside Visual Studio. It cannot be automated by Claude Code or any CLI tool.** CFX commands execute inside the Visual Studio process via the CFX Runtime.

### `[HUMAN]` Run "Setup Blazor WASM Projects"

Right-click the **WingtipToys project node** → click **"Setup Blazor WASM Projects"**.

A dialog collects the three target project names. Accept the defaults:

| Field | Default |
|---|---|
| Client Project Name | `WingtipToys.Client` |
| Server Project Name | `WingtipToys.Server` |
| Shared Project Name | `WingtipToys.Shared` |

Click **Run**. The CodeFactory output window shows seven migration steps:

```
[CFX] Step 1 — Scaffolding target projects...
[CFX] Step 2 — Migrating configuration...
[CFX] Step 3 — Migrating logic classes...
[CFX] Step 4 — Migrating static files...
[CFX] Step 5 — Migrating .aspx files...
[CFX] Step 6 — Scaffolding API layer...
[CFX] Step 7 — Scaffolding client program...
[CFX] Migration complete.
```

Three new projects (`WingtipToys.Client`, `WingtipToys.Server`, `WingtipToys.Shared`) materialize in Solution Explorer in real time.

### `[HUMAN]` Migrate Individual Pages

Right-click each `.aspx` file → click **"Migrate WebForm to WASM"**:

```
Default.aspx          → Index.razor        (@page "/")
ProductList.aspx      → ProductList.razor  (@page "/products")
ProductDetails.aspx   → ProductDetails.razor
ShoppingCart.aspx     → ShoppingCart.razor
Checkout.aspx         → Checkout.razor
OrderConfirmation.aspx → OrderConfirmation.razor
```

Each run adds the corresponding `.razor` file to `WingtipToys.Client/Pages/`. Once a page is migrated, the command hides itself for that `.aspx` file — right-clicking it again will not show the command. This is the CFX idempotency guarantee.

> **Verify each generated file:** Open the `.razor` file immediately after migrating. Confirm it contains `@inject HttpClient Http`, `OnInitializedAsync`, a provenance comment referencing the source `.aspx` file, and **no `using CodeFactory` statements**.

### `[GATE]` Phase 4 Gate

> **Three new projects appear in Solution Explorer. Six `.razor` files exist in `WingtipToys.Client/Pages/`. The Setup command has hidden itself. Each Migrate command has hidden itself for its corresponding `.aspx` file.**

---

## Phase 5 — Verify the Output

### `[CLI]` Check Zero CodeFactory References

Run this from the solution root. The result must be **zero lines**:

```bash
grep -r "using CodeFactory" WingtipToys.Client/ WingtipToys.Server/ WingtipToys.Shared/
# Expected: (no output)
```

Any result here is a bug in a generation method. Generated output must be completely free of CodeFactory SDK references.

### `[CLI]` Build All Three Generated Projects

The generated projects target .NET 8 — use the `dotnet` CLI, not MSBuild:

```bash
# Build in dependency order: Shared first
dotnet build WingtipToys.Shared/WingtipToys.Shared.csproj
dotnet build WingtipToys.Server/WingtipToys.Server.csproj
dotnet build WingtipToys.Client/WingtipToys.Client.csproj
```

All three must exit with zero errors and zero warnings.

### `[CLI]` Run EF Core Migrations

```bash
cd WingtipToys.Server
dotnet ef migrations add InitialCreate
dotnet ef database update
```

This creates the `WingtipToysWASM` LocalDB database and seeds the product data.

> If `dotnet-ef` isn't installed: `dotnet tool install --global dotnet-ef`

### `[HUMAN]` Run the WASM App

Set `WingtipToys.Server` as the startup project. Press `F5`. The Server project hosts the WASM client and serves the Blazor WebAssembly app at `https://localhost:5001`.

> **First load is slow.** The browser downloads the .NET WebAssembly runtime (~10MB) on first run. Subsequent loads use the browser cache. If you're preparing for a demo recording, do a full load-and-navigate dry run first.

---

## Final Verification Checklist

All 12 items must pass before the migration is considered complete.

### Build verification
- [ ] `WingtipToys.Shared` compiles with zero errors or warnings
- [ ] `WingtipToys.Server` compiles with zero errors or warnings
- [ ] `WingtipToys.Client` compiles with zero errors or warnings
- [ ] `dotnet ef migrations add InitialCreate` + `dotnet ef database update` ran successfully
- [ ] Server runs and serves the WASM app at `https://localhost:5001`

### Page verification (browser)
- [ ] `Index.razor` renders the category list from the API
- [ ] `ProductList.razor` renders the product grid for a selected category
- [ ] `ProductDetails.razor` renders a single product with an Add to Cart button
- [ ] `ShoppingCart.razor` shows items added across component navigations (cart persists in session)
- [ ] `Checkout.razor` submits an order to the API and navigates to OrderConfirmation
- [ ] `OrderConfirmation.razor` shows the order ID and total

### Zero-dependency verification
- [ ] `grep -r "using CodeFactory"` on all three generated projects returns zero results

---

## Claude Code Permission Reference

If you're using Claude Code to automate the build and verification steps, here is the complete permissions and capabilities breakdown.

### What Claude Code can do

| Phase | Action | Permission Required |
|---|---|---|
| Phase 1 | Scaffold WingtipToys source files | File system read/write |
| Phase 1 | Run MSBuild | Shell/bash execution |
| Phase 1 | NuGet restore | `nuget.org` network access |
| Phase 2 | Scaffold Commands Library files | File system read/write |
| Phase 2 | Run MSBuild (triggers CFXPackager) | Shell/bash execution |
| Phase 5 | `grep -r "using CodeFactory"` | File system read, shell |
| Phase 5 | `dotnet build` on all 3 projects | Shell/bash, `nuget.org` network |
| Phase 5 | `dotnet ef migrations add` + `database update` | Shell/bash, SQL Server LocalDB |
| Phase 5 | `dotnet run` to start Server | Shell/bash, localhost network |

### What Claude Code cannot do

| Step | Why |
|---|---|
| Copy `.cfx` to solution root and verify context menu | Requires file system action + human to observe VS UI |
| Run "Setup Blazor WASM Projects" or "Migrate WebForm to WASM" | CFX commands execute inside the VS process via the Runtime — no CLI interface |
| Verify browser page rendering | Requires a human to observe the rendered output in a browser |
| Browser cache clear | Requires human browser interaction |

### Recommended Claude Code session structure

**Session 1:** Phases 1 + 2 — build source app and Commands Library. Pause and instruct the human to complete Phases 3 + 4 in Visual Studio.

**Session 2:** After human confirms Phase 4 complete — run Phase 5 verification: `grep` check, `dotnet build` all three projects, `dotnet ef` migrations, `dotnet run` to confirm the server starts.

---

## Related Resources

- [CFX-WebForms2WASMMigration GitHub repo](https://github.com/bfencken/CFX-WebForms2WASMMigration)
- [CodeFactory SDK GitHub](https://github.com/CodeFactoryLLC/CodeFactory)
- [CFX v2 Documentation](https://windows.codefactory.software)
- [WebForms2BlazorServer (source this was derived from)](https://github.com/CodeFactoryLLC/WebForms2BlazorServer) — authored by John Hannah
- [WingtipToys reference app](https://github.com/corn-pivotal/wingtiptoys)
- [codefactory.software](https://codefactory.software) — licensing and downloads

---

*Migration Guide · CFX-WebForms2WASMMigration · Built with the [CodeFactory SDK](https://github.com/CodeFactoryLLC/CodeFactory)*
