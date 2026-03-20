# CFX-WebForms2WASMMigration

> **A CodeFactory automation package that migrates an ASP.NET WebForms application to Blazor WebAssembly — one right-click at a time.**

This repository is an open-source reference implementation built on the [CodeFactory (CFX) SDK](https://github.com/CodeFactoryLLC/CodeFactory). It exists as a **learning resource and starting point** for experienced .NET developers who want to understand how CFX automation works in a real, non-trivial migration scenario. It is derived from and extends the [WebForms2BlazorServer](https://github.com/CodeFactoryLLC/WebForms2BlazorServer) package authored by John Hannah.

---

## What This Is

CFX is a Visual Studio extension that lets architects encode code transformation and generation patterns as C# commands. Those commands appear in the Solution Explorer right-click context menu and execute against a live, parsed model of your source code — no regex, no text manipulation, no build pipeline. The CFX Runtime runs inside the Visual Studio process and provides a structured AST of your solution that your commands can read, traverse, and write against.

This package implements two commands:

| Command | Trigger | What it does |
|---|---|---|
| **Setup Blazor WASM Projects** | Right-click the WebForms project | Scaffolds three new projects (`.Client`, `.Server`, `.Shared`), migrates config, static files, logic classes, and generates the API and client program scaffolding |
| **Migrate WebForm to WASM** | Right-click any `.aspx` file | Migrates a single page to a Blazor component in `.Client/Pages/`, with `@inject HttpClient` and `OnInitializedAsync` wired up |

The target reference app is **WingtipToys** — a classic ASP.NET 4.x WebForms e-commerce project that's freely available and representative of real legacy .NET code.

---

## Why This Exists as Open Source

The CFX SDK is open core. The **CFX Runtime** (the engine that runs inside Visual Studio) is closed-source commercial software. But the **automation commands** you author against the SDK — the `.cfx` package files and the Commands Library C# projects that produce them — are yours. You own the output entirely, and there is no runtime dependency on CodeFactory in any code this package generates.

This repo demonstrates what a non-trivial CFX automation package looks like in practice:

- Two commands with real Enable/Execute lifecycle logic
- A multi-step migration engine with seven discrete phases
- `SourceFormatter`-based code generation across four distinct output types (Razor components, Web API controllers, DTO classes, Program.cs)
- Idempotent commands that hide themselves once their work is done
- Zero CodeFactory references in any generated output

If you're evaluating CFX for your team or building your own automation package, this is intended to be the code you read.

---

## The CFX Mental Model (Read This First)

Before diving into the source, there are four concepts that make everything else click.

### 1. The Two-Method Contract

Every CFX command is a C# class with exactly two methods:

```csharp
// Called every time a context menu opens on the bound VS node.
// Return true = show the command. Return false = hide it.
// NEVER throw from this method — it will break the entire VS context menu.
public override async Task<bool> EnableCommandAsync(VsProject model)
{
    try { return /* real model evaluation */; }
    catch { return false; }
}

// Called when the developer clicks the command.
// All automation logic lives here.
public override async Task ExecuteCommandAsync(VsProject model)
{
    // ...
}
```

That's the entire framework surface. Everything else is ordinary C# and SDK API calls.

### 2. The Data Model Is a Live AST

When CFX calls your methods, it passes a fully parsed object graph of whatever was right-clicked. From a `VsCSharpSource` you can navigate to `.Classes`, `.Methods`, `.Properties`, `.InheritedInterfaces`, `.NameSpaces` — the complete syntax tree, resolved in real time from the live Visual Studio document model.

You never parse text. You navigate objects.

### 3. Data Models Are Immutable Snapshots

After you mutate a document (via `AddToEndAsync`, `ReplaceAsync`, `DeleteAsync`, etc.), the model reference you're holding is **stale**. The document has changed; the model hasn't. Always re-fetch with `GetCSharpSourceModelAsync()` after any write operation before reading from the model again.

### 4. SourceFormatter Is the Only Generation Mechanism

CFX v2.0 uses `SourceFormatter` exclusively for generated source output. Raw string concatenation is brittle and produces inconsistent whitespace. T4 templates are deprecated.

```csharp
var formatter = new SourceFormatter();
formatter.AppendCodeLine(2, "public async Task<bool> ChargeAsync(decimal amount)");
formatter.AppendCodeLine(2, "{");
formatter.AppendCodeLine(3, "    throw new NotImplementedException();");
formatter.AppendCodeLine(2, "}");
return formatter.ReturnSource();
```

`AppendCodeLine(indentLevel, text)` handles indentation (4 spaces per level) and line endings. `ReturnSource()` produces the final string ready for injection.

---

## Project Structure

```
CFX-WebForms2WASMMigration/
│
├── Commands/
│   ├── Project/
│   │   └── SetupBlazorProject.cs       ← Project Command — bulk migration
│   └── Document/
│       └── MigrateWebForm.cs           ← Document Command — single page migration
│
├── Dialog/
│   ├── SetupBlazorDialog.xaml/.cs      ← WPF dialog: collects project names
│   ├── MigrateWebFormDialog.xaml/.cs   ← WPF dialog: shows progress
│   └── DialogExtensions.cs
│
├── Migration/
│   ├── MigrationContext.cs             ← Holds resolved project references for a run
│   ├── MigrationEngine.cs              ← Orchestrates the 7-step sequence
│   ├── MigrationEngine.Config.cs       ← web.config → appsettings.json
│   ├── MigrationEngine.LogicClasses.cs ← C# class migration
│   ├── MigrationEngine.StaticFiles.cs  ← Images, scripts, CSS
│   ├── MigrationEngine.AspxFiles.cs    ← .aspx → Razor component
│   ├── MigrationEngine.ApiLayer.cs     ← Web API controller generation
│   └── MigrationEngine.ClientProgram.cs← .Client Program.cs generation
│
└── Generation/
    ├── RazorPageGenerator.cs           ← SourceFormatter: Blazor component scaffold
    ├── ApiControllerGenerator.cs       ← SourceFormatter: ASP.NET Core ApiController
    ├── SharedDtoGenerator.cs           ← SourceFormatter: WASM-safe DTO from EF entity
    └── ClientProgramGenerator.cs       ← SourceFormatter: .Client Program.cs
```

The Commands Library project itself targets **.NET Framework 4.7.2** — CFX runs inside the Visual Studio process, which is a .NET Framework host. The projects it *generates* target .NET 8.

---

## Prerequisites

| Requirement | Detail |
|---|---|
| Visual Studio 2022 (v17.x+) | Windows only. Mac is not supported by CFX. |
| [CodeFactory for Windows](https://marketplace.visualstudio.com/items?itemName=CodeFactory.CodeFactoryForWindows) | Install from VS Marketplace. Provides the Runtime and the Commands Library project template. |
| CFX license key | Required to activate the Runtime. Obtain from [codefactory.software](https://codefactory.software). A 90-day free trial is available. |
| .NET Framework 4.7.2 targeting pack | For the Commands Library project. Usually present in VS 2022; install via VS Installer if missing. |
| .NET 8 SDK | For the generated `.Client` / `.Server` / `.Shared` projects. |
| SQL Server LocalDB | For the WingtipToys EF6 database. Install via VS Installer → Data Storage and Processing workload. |

---

## Quick Start

**Step 1 — Get WingtipToys**

Clone the reference WebForms app:
```
git clone https://github.com/corn-pivotal/wingtiptoys
```
Open `WingtipToys.sln` in Visual Studio 2022. Build and verify it runs before proceeding.

**Step 2 — Deploy the .cfx package**

Download `WebForms2BlazorWASMCommands.cfx` from [Releases](https://github.com/bfencken/CFX-WebForms2WASMMigration/releases) and copy it to the root folder of the WingtipToys solution — the same directory that contains `WingtipToys.sln`.

**Step 3 — Open the solution**

Close and reopen `WingtipToys.sln`. The CFX Runtime loads the package automatically. You'll see activity in the CodeFactory output pane.

**Step 4 — Run Setup Blazor WASM Projects**

Right-click the `WingtipToys` project node in Solution Explorer. Click **"Setup Blazor WASM Projects"**. A dialog collects the three target project names (defaults: `WingtipToys.Client`, `WingtipToys.Server`, `WingtipToys.Shared`). Click Run.

Watch the Output window. Seven migration steps execute in sequence. Three new projects materialize in Solution Explorer in real time.

**Step 5 — Migrate individual pages**

Right-click any `.aspx` file. Click **"Migrate WebForm to WASM"**. The corresponding `.razor` component appears in `.Client/Pages/`.

> **Idempotency note:** Once all pages are migrated, both commands hide themselves from the context menu automatically. Running either command a second time on an already-processed target is safe — it does nothing.

---

## What Gets Generated

### Three-Project Solution Structure

```
WingtipToys.Client/         ← Blazor WASM, .NET 8, runs in browser
  Pages/
    Index.razor              ← migrated from Default.aspx
    ProductList.razor        ← migrated from ProductList.aspx
    ProductDetails.razor
    ShoppingCart.razor
    Checkout.razor
    OrderConfirmation.razor
  Program.cs                 ← HttpClient base address, scoped CartService

WingtipToys.Server/         ← ASP.NET Core host, .NET 8, EF Core + Web API
  Controllers/
    ProductsController.cs   ← generated ApiController per entity
    CategoriesController.cs
    OrdersController.cs
  Program.cs                 ← Hosts WASM client via UseBlazorFrameworkFiles

WingtipToys.Shared/         ← .NET Standard 2.1, referenced by both above
  Models/
    Product.cs               ← WASM-safe DTO (no EF attributes, no virtual nav)
    Category.cs
    Order.cs
    CartItem.cs
```

### What Generated Code Looks Like

A migrated Razor page from `ProductList.aspx`:

```razor
@page "/products"
@using WingtipToys.Shared.Models
@inject HttpClient Http

<PageTitle>ProductList</PageTitle>
<!-- Migrated from: ProductList.aspx -->
<!-- TODO: Implement component body using the API calls below -->

@code {
    private List<Product> _items;

    protected override async Task OnInitializedAsync()
    {
        // TODO: Replace with correct API endpoint
        _items = await Http.GetFromJsonAsync<List<Product>>("api/products");
    }
}
```

There are no CodeFactory references anywhere in the output. The generated code is indistinguishable from hand-written code.

---

## Reading the Source as a CFX Learning Resource

If your goal is to learn how CFX works by reading this codebase, here's the recommended reading order:

**Start here — the command entry points:**
1. `Commands/Project/SetupBlazorProject.cs` — read `EnableCommandAsync` first. Notice how it checks for the absence of a `.Client` project AND the presence of `.aspx` files before returning `true`. This is the Enable gate pattern.
2. `Commands/Document/MigrateWebForm.cs` — notice the three-layer Enable guard: `.aspx` extension check, `.Client` project existence check, and the idempotency check (does the target `.razor` already exist?).

**Then the engine:**
3. `Migration/MigrationEngine.cs` — the 7-step orchestration sequence. Each step is a separate partial class method, called in order. Read this to understand how large multi-file generation is structured.
4. `Migration/MigrationContext.cs` — shows how to hold resolved project references safely across a migration run without violating CFX's prohibition on caching data models between command invocations.

**Then generation:**
5. `Generation/RazorPageGenerator.cs` — the simplest `SourceFormatter` example. Walks through `AppendCodeLine` at each indent level and shows how the output string is handed to `AddDocumentAsync`.
6. `Generation/SharedDtoGenerator.cs` — the most interesting generation example: it reads the live EF entity model via `GetCSharpSourceModelAsync`, strips server-only attributes, then emits the sanitized DTO. This is the "read then write" pattern.

**Key things to look for in every command:**
- The `try { } catch { return false; }` wrapper in every `EnableCommandAsync` — mandatory per CFX compliance rules
- `await GetCSharpSourceModelAsync()` called after every document mutation — required because mutations make the previous model stale
- `VisualStudioActions.Environment.UserInterfaceActions.OutputWindowActions.WriteToOutputWindowAsync(...)` for all user-facing messages — not `Console.WriteLine`

---

## CFX Compliance Rules Applied in This Package

This package was authored against the CFX Automation Compliance Rules. The rules most visible in the source:

| Rule | Requirement | Where you see it |
|---|---|---|
| R-1 | Both lifecycle methods required | Every command class |
| R-2 | `EnableCommandAsync` must return `false` on any exception — never throw | Outer `try/catch` in every Enable method |
| R-3 | Enable must evaluate the real model, not always return `true` | Project + document checks in both Enable gates |
| R-9 | Always null-check models before traversal | `if (source?.SourceCode == null) return false;` pattern throughout |
| R-10 | Re-fetch the model after any mutation | `await source.GetCSharpSourceModelAsync()` after every `AddToEndAsync` / `AddDocumentAsync` |
| R-14 | Write to the CFX output window, not `Console` or `Debug` | All status messages use `OutputWindowActions` |
| R-15 | `SourceFormatter` for all generated output | All four generator classes |
| R-27 | Generated code must contain no CodeFactory references | Verified — no `using CodeFactory` in any generator output |
| R-29 | Commands must be idempotent | `SetupBlazorProject` hides when `.Client` exists; `MigrateWebForm` hides when `.razor` exists |

---

## Build the Package Yourself

If you want to modify the automation and produce your own `.cfx` file:

1. Install the **CodeFactory for Windows SDK** extension from the VS Marketplace (this is separate from the Runtime extension — it adds the Commands Library project template and the CFXPackager build tool).
2. Open `WebForms2BlazorWASMCommands.sln` from the extracted `files.zip`.
3. Build (`Ctrl+Shift+B`). The first build will trigger NuGet restore — expect errors until it completes. This is normal.
4. After a successful build, `WebForms2BlazorWASMCommands.cfx` appears in `bin/`. Copy it to your solution root to deploy.

Every build automatically runs the `CFXPackager` post-build step, which bundles the compiled command assembly, resources, and dialog definitions into the single `.cfx` deployment file.

---

## Ideas for Your Own CFX Automation

This migration package is one example of what CFX automation can do. The same SDK patterns — live model inspection, `SourceFormatter` generation, idempotent Enable gates — apply to a wide range of .NET transformation scenarios. Here are concrete starting points for commands you might build next.

---

### Repository Pattern Generator

**Scenario:** Your team has a rule that every EF `DbContext` entity should have a corresponding repository interface and implementation. Right now developers create these by hand — inconsistently.

**What the command does:** Right-click any C# class. If the class is registered in a `DbContext` (the Enable gate reads the DbContext model to check), the command generates `IProductRepository.cs` and `ProductRepository.cs` with standard CRUD method stubs, injects the `DbContext` via constructor, and wires the interface into `Program.cs` / `Startup.cs`.

**CFX patterns it would demonstrate:** `CSharpSourceCommandBase`, reading `CsClass.InheritedInterfaces` to find the `DbContext`, cross-file generation (writing to a different project folder than the source), `NamespaceManager` for using statement injection.

---

### Async/Await Upgrade Command

**Scenario:** A legacy codebase has hundreds of synchronous database and HTTP calls (`HttpClient.GetResult()`, `context.SaveChanges()`) that should be `await`-ified for .NET 6+ performance.

**What the command does:** Right-click a C# source file. The Enable gate checks whether any method contains a synchronous blocking call on a known list of types. Execute replaces each method signature and body using `ReplaceAsync` — adding the `async` keyword, swapping `SaveChanges()` for `await SaveChangesAsync()`, and updating the return type from `void` to `Task`.

**CFX patterns it would demonstrate:** `CsMethod.GetBodySyntaxAsync()` to read and transform method bodies, `ReplaceAsync` on methods, handling the `void` → `Task` return type change, multi-method iteration with model re-fetch between each mutation.

---

### Interface Extraction Command

**Scenario:** A service class has grown to 800 lines and has no interface. You want to extract `IOrderService` from `OrderService` automatically — pulling every `public` method signature into a new interface file and adding `: IOrderService` to the class declaration.

**What the command does:** Right-click the class. Enable returns `true` only if the class is `public`, has at least one `public` method, and does not already implement an interface with the matching naming convention. Execute generates the interface file in the same folder, updates the class declaration via `ReplaceAsync`, and injects the using statement via `NamespaceManager`.

**CFX patterns it would demonstrate:** `CsClass.Methods` filtering by access modifier, generating a new document in the same project folder via `AddDocumentAsync`, modifying the class declaration line using `ReplaceAsync` on the class model, the `CsManualUsingStatement` pattern.

---

### Null-Safety Audit Command

**Scenario:** Migrating from .NET Framework to .NET 6+ with nullable reference types enabled. The codebase has hundreds of parameters and return types that need explicit nullability annotations.

**What the command does:** Right-click a project. The command walks every C# source file, identifies public method parameters and return types that lack `?` annotations where the type is a reference type, and either annotates them automatically or generates a report document listing every location that needs manual review.

**CFX patterns it would demonstrate:** `ProjectCommandBase`, iterating the entire project via `GetChildrenAsync(recursive: true)`, reading `CsMethod.Parameters` and `CsParameter.ParameterType` across hundreds of files, the distinction between mutation (auto-annotate) and reporting (generate a summary document) modes.

---

### HttpClient Named Client Standardizer

**Scenario:** A microservices solution has eight services, each creating `HttpClient` instances inconsistently — some using `IHttpClientFactory`, some newing up instances directly, some using typed clients. The architecture standard is named clients registered in `Program.cs`.

**What the command does:** Right-click the solution. The command finds every class that holds a raw `HttpClient` field, replaces direct instantiation with `IHttpClientFactory` injection via constructor, and generates the corresponding `AddHttpClient("ServiceName", ...)` registration in `Program.cs`.

**CFX patterns it would demonstrate:** `SolutionCommandBase`, cross-project analysis via `GetProjectsAsync()`, the MigrationContext pattern for holding resolved project references, coordinated multi-file writes across projects within a single command execution.

---

### EF Entity → Minimal API Endpoint Generator

**Scenario:** Your team is modernizing a data layer and wants to expose each EF entity as a Minimal API endpoint in .NET 7+ — with GET (all), GET (by id), POST, PUT, and DELETE — following a consistent pattern across all entities.

**What the command does:** Right-click any class that inherits from a base entity or is registered in a `DbContext`. Generate a self-contained Minimal API endpoint file in a `Endpoints/` folder, with the five CRUD operations and a `MapGroup` extension method, ready to be wired into `Program.cs`.

**CFX patterns it would demonstrate:** Reading `CsClass.Properties` to infer the primary key, `SharedDtoGenerator`-style attribute stripping, generating a file that uses `MapGroup` (non-trivial `SourceFormatter` with nested method generation), and the Enable gate pattern checking DbContext registration.

---

## Related Resources

- [CodeFactory SDK — GitHub](https://github.com/CodeFactoryLLC/CodeFactory) — SDK source, release notes, and issue tracker
- [WebForms2BlazorServer](https://github.com/CodeFactoryLLC/WebForms2BlazorServer) — the Server-side Blazor counterpart this package was derived from, authored by John Hannah
- [WCF to gRPC migration package](https://github.com/CodeFactoryLLC/WCF-To-gRPC) — another reference migration package; good contrast in command structure
- [CodeFactory Extensions](https://github.com/CodeFactoryLLC/CodeFactoryExtensions) — open-source C# formatting helpers that extend `SourceFormatter`
- [codefactory.software](https://codefactory.software) — product documentation, SDK downloads, licensing
- [docs.codefactory.software](https://docs.codefactory.software) — CFX SDK API reference and getting started guides

---

## License

The Commands Library source in this repository is released under the MIT License. The **CFX Runtime** (the Visual Studio extension that executes commands) is commercial software and requires a separate license. Generated output is your code — no license restrictions apply to anything this package produces.

---

## Contributing

Issues and pull requests welcome. The most valuable contributions are:

- Expanded `.aspx` control-to-Blazor-component mapping in `MigrationEngine.AspxFiles.cs`
- Additional WingtipToys page coverage (Checkout and OrderConfirmation flows need fuller treatment)
- Improved EF entity-to-DTO fidelity for complex navigation graphs in `SharedDtoGenerator.cs`
- Documentation improvements and additional XML doc comments in the generation classes

If you build one of the extension scenarios described above and want to share it, open a PR or link to your repo in a Discussion.

---

*Built with the [CodeFactory SDK](https://github.com/CodeFactoryLLC/CodeFactory) · Derived from [WebForms2BlazorServer](https://github.com/CodeFactoryLLC/WebForms2BlazorServer) by John Hannah · Maintained by [@bfencken](https://github.com/bfencken)*
