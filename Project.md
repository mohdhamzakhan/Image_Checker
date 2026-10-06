# Image Checker — Project Context

## 1. Project Overview

**Project name:** Image_Checker

**Repository:** `mohdhamzakhan/Image_Checker`

**Primary technology:** C# / .NET 8 / Windows Forms

**Solution:** `Image_Checker.sln`

This repository contains a multi-project .NET solution for an image-checking/processing desktop application.

The solution is divided into:

1. `Image_Checker` — Core application and processing logic
2. `Image_Checker.Forms` — WinForms UI/application layer
3. `Image_Checker.WinForm` — WinForms entry/UI project
4. `Model` — Shared model/data-related files
5. `Images` / `images` — Image resources/test/sample data

---

# 2. Solution Architecture

```text
Image_Checker.sln
│
├── Image_Checker
│   ├── Core processing logic
│   ├── Image processing
│   ├── ML/AI functionality
│   ├── Data processing
│   └── Supporting services/utilities
│
├── Image_Checker.Forms
│   ├── Windows Forms UI
│   ├── Charts
│   └── User interface components
│
├── Image_Checker.WinForm
│   └── Windows Forms application/entry point
│
├── Model
│   └── Shared model/data classes
│
├── Images
│   └── Image resources/data
│
└── images
    └── Image resources/data
```

The Visual Studio solution currently contains these three projects:

- `Image_Checker`
- `Image_Checker.WinForm`
- `Image_Checker.Forms`

Both WinForms projects reference the core `Image_Checker` project.

Do not assume that the similarly named `Image_Checker.Forms` and `Image_Checker.WinForm` projects are duplicates. Inspect their actual source code before changing architecture.

---

# 3. Project Dependencies

Current dependency direction:

```text
Image_Checker.WinForm
        │
        └──────────────┐
                       ↓
                 Image_Checker
                       ↑
        ┌──────────────┘
        │
Image_Checker.Forms
```

Both UI projects reference:

```text
Image_Checker/Image_Checker.csproj
```

The core project should remain independent of UI-specific implementation wherever possible.

### Important architectural rule

Prefer:

```text
UI
 ↓
Service / Application Logic
 ↓
Core Processing
 ↓
Models / Data
```

Avoid:

```text
UI
 ↓
Directly performs complex processing
 ↓
Database/file/model logic
```

Business and processing logic should not unnecessarily be placed inside Forms classes.

---

# 4. Target Framework

## Core

`Image_Checker`

```text
.NET 8
TargetFramework: net8.0
OutputType: Exe
```

## WinForms

`Image_Checker.WinForm`

```text
.NET 8 Windows
TargetFramework: net8.0-windows
UseWindowsForms: true
ApplicationHighDpiMode: PerMonitorV2
```

## Forms

`Image_Checker.Forms`

```text
.NET 8 Windows
TargetFramework: net8.0-windows
UseWindowsForms: true
ApplicationHighDpiMode: PerMonitorV2
ForceDesignerDpiUnaware: true
```

---

# 5. Core Technology Stack

The core project currently uses several Microsoft ML, ONNX, TorchSharp, TensorFlow, image-processing, and data-processing packages.

Important dependencies include:

- ExcelDataReader
- ExcelDataReader.DataSet
- libtorch-cpu
- Microsoft.Data.Analysis
- Microsoft.ML
- Microsoft.ML.ImageAnalytics
- Microsoft.ML.LightGbm
- Microsoft.ML.OnnxRuntime
- Microsoft.ML.OnnxTransformer
- Microsoft.ML.TensorFlow.Redist
- Microsoft.ML.TimeSeries
- Microsoft.ML.Vision
- System.Drawing.Common
- System.IO.Ports
- System.Management
- TorchSharp

Treat these dependencies as part of the existing architecture.

Do not replace an ML/image-processing library without first checking how it is currently used.

---

# 6. Application Domain

The project is an image-checking/processing application.

The application should be understood in terms of the following conceptual areas:

```text
Image Input
    ↓
Image Loading
    ↓
Image Validation / Processing
    ↓
Image Analysis
    ↓
Optional ML / AI Processing
    ↓
Result Generation
    ↓
WinForms Presentation
```

When investigating a feature, identify where it belongs in this pipeline before modifying code.

---

# 7. AI/ML Components

The core project contains dependencies for multiple machine-learning technologies.

Potential processing technologies include:

### ML.NET

Used for machine-learning functionality.

Relevant packages include:

```text
Microsoft.ML
Microsoft.ML.ImageAnalytics
Microsoft.ML.LightGbm
Microsoft.ML.TimeSeries
Microsoft.ML.Vision
```

### ONNX

The project uses:

```text
Microsoft.ML.OnnxRuntime
Microsoft.ML.OnnxTransformer
```

ONNX-related functionality should be treated separately from ordinary image-processing code.

### TorchSharp

The project uses:

```text
TorchSharp
libtorch-cpu
```

TorchSharp functionality may involve tensor/model processing.

### TensorFlow

The project contains:

```text
Microsoft.ML.TensorFlow.Redist
```

Do not assume TensorFlow, TorchSharp, ONNX, and ML.NET perform the same function. Inspect the actual implementation before refactoring.

---

# 8. Data Processing

The project includes:

```text
ExcelDataReader
ExcelDataReader.DataSet
Microsoft.Data.Analysis
```

These dependencies indicate that spreadsheet/tabular data may participate in the application workflow.

When modifying Excel/data-processing functionality:

1. Preserve existing column mappings.
2. Preserve existing data types.
3. Validate missing/invalid values.
4. Avoid loading unnecessarily large datasets into memory.
5. Do not silently change Excel parsing behavior.

---

# 9. Image Processing

The project uses:

```text
System.Drawing.Common
```

Image processing may involve:

- Loading images
- Bitmap manipulation
- Pixel operations
- Image validation
- Image comparison
- Image analysis
- Image conversion
- Image metadata

Before modifying image-processing code, identify:

```text
Input image
    ↓
Image format
    ↓
Bitmap/Image representation
    ↓
Processing
    ↓
Output/result
```

Be careful with resource management.

Objects such as:

```csharp
Bitmap
Image
Graphics
Stream
```

must be disposed appropriately.

Prefer:

```csharp
using
```

or:

```csharp
using var
```

where ownership permits.

---

# 10. Serial Port / Hardware

The project references:

```text
System.IO.Ports
```

This means serial communication may be part of the application.

When modifying serial communication:

- Do not block the UI thread.
- Handle connection failures.
- Handle device disconnection.
- Dispose serial resources correctly.
- Avoid unbounded background threads.
- Keep hardware communication separate from presentation logic where possible.

---

# 11. Windows/System Integration

The project references:

```text
System.Management
```

This may be used for Windows/system information or hardware-related functionality.

Do not assume the exact usage without inspecting the implementation.

---

# 12. WinForms Architecture

The existing project places significant importance on structured WinForms layouts.

Preferred hierarchy:

```text
Form
│
└── Main Panel
    │
    ├── Header Panel
    │
    ├── Content Panel
    │   │
    │   └── TableLayoutPanel
    │
    └── Footer Panel
```

Prefer layout containers over manually calculated coordinates.

Preferred controls:

```text
TableLayoutPanel
FlowLayoutPanel
Panel
GroupBox
```

Use:

```csharp
Dock = DockStyle.Fill
```

where appropriate.

Use `Anchor` only when it provides a clear layout benefit.

---

# 13. WinForms Layout Rules

These rules are mandatory unless there is a strong technical reason otherwise.

### Do

- Use `TableLayoutPanel` for structured forms.
- Use `FlowLayoutPanel` for dynamic horizontal/vertical groups.
- Use `Panel` for logical sections.
- Use `GroupBox` for related controls.
- Use `Dock`.
- Use `AutoSize` where appropriate.
- Maintain consistent margins and padding.
- Use `SuspendLayout()` / `ResumeLayout()` when making multiple layout changes.

### Avoid

- Absolute positioning everywhere.
- Random X/Y coordinates.
- Overlapping controls.
- Excessive fixed widths.
- Huge forms containing unrelated sections.
- Mixing incompatible layout strategies.

---

# 14. UI Spacing

Existing UI guidelines:

```text
Outer margin:       16px
Inner padding:       8px
Vertical spacing:   10–12px
Label/Input gap:      6px
```

Maintain consistent spacing when creating or redesigning forms.

---

# 15. UI Naming Convention

Follow the existing naming convention where practical.

Examples:

```text
txtName
cmbType
btnSubmit

pnlMain
tblLayout
grpDetails
```

Use descriptive names.

Avoid:

```text
button1
panel2
textBox7
```

for newly created controls.

When modifying existing controls, do not rename them unnecessarily because existing event handlers or code may depend on them.

---

# 16. UI Design Principles

Every major UI should have clear sections.

For example:

```text
┌─────────────────────────────────────────┐
│ Header                                  │
├─────────────────────────────────────────┤
│                                         │
│ Input / Configuration                   │
│                                         │
├─────────────────────────────────────────┤
│ Results                                 │
│                                         │
├─────────────────────────────────────────┤
│ Status / Messages                       │
├─────────────────────────────────────────┤
│                         [Cancel] [Save]  │
└─────────────────────────────────────────┘
```

Buttons should normally be placed in a bottom/footer area and aligned consistently.

---

# 17. UI Thread Rule

Never perform long-running work synchronously on the UI thread.

Avoid:

```csharp
private void btnProcess_Click(object sender, EventArgs e)
{
    ProcessThousandsOfImages();
}
```

Prefer an asynchronous/background approach where appropriate.

For example:

```csharp
private async void btnProcess_Click(object sender, EventArgs e)
{
    btnProcess.Enabled = false;

    try
    {
        await ProcessImagesAsync();
    }
    finally
    {
        btnProcess.Enabled = true;
    }
}
```

The exact implementation should follow the existing application's architecture.

Do not introduce `Task.Run()` blindly. First determine whether the operation is CPU-bound, I/O-bound, or already asynchronous.

---

# 18. Error Handling

User-facing errors should be understandable.

Avoid exposing raw technical exceptions directly to users unless appropriate.

Prefer:

```text
Unable to process the selected image.

Reason:
The image format is not supported.

Please select a valid image and try again.
```

while logging the underlying exception for diagnostics.

Never silently swallow exceptions:

```csharp
catch
{
}
```

If an exception is intentionally ignored, document why.

---

# 19. Resource Management

Image processing can consume significant memory.

Pay special attention to:

```text
Bitmap
Image
Graphics
Stream
FileStream
SerialPort
ML models
Tensor objects
```

Dispose resources when ownership ends.

Avoid retaining large bitmaps unnecessarily.

When processing many images:

```text
Load
 ↓
Process
 ↓
Release
 ↓
Next image
```

rather than retaining every image in memory.

---

# 20. Performance

For image processing and ML workloads:

- Avoid unnecessary image copies.
- Avoid repeatedly loading the same model.
- Avoid repeatedly allocating large buffers.
- Dispose images promptly.
- Avoid blocking the UI thread.
- Avoid unnecessary redraws.
- Use batching where the existing implementation supports it.
- Profile before introducing complex optimizations.

Do not optimize based solely on assumptions.

---

# 21. Existing Documentation

The repository already contains:

```text
CLAUDE.md
prompts.md
rules.md
winforms-ui.md
```

These files contain project-specific instructions and should be read before modifying the project.

Priority:

```text
PROJECT.md
    ↓
CLAUDE.md
    ↓
rules.md
    ↓
winforms-ui.md
    ↓
prompts.md
    ↓
Actual source code
```

If two documents conflict, prefer the more specific rule for the affected component, while preserving existing working behavior.

---

# 22. How an AI Agent Should Analyze This Repository

When asked to modify this project, do NOT immediately start changing code.

First perform:

### Step 1 — Understand the solution

Read:

```text
Image_Checker.sln
```

Identify all projects and project references.

### Step 2 — Understand project files

Read:

```text
Image_Checker/Image_Checker.csproj
Image_Checker.Forms/Image_Checker.Forms.csproj
Image_Checker.WinForm/Image_Checker.WinForm.csproj
```

### Step 3 — Find entry points

Locate:

```text
Program.cs
MainForm.cs
```

and other startup/application classes.

### Step 4 — Identify UI

Find:

```text
Form
UserControl
Panel
TableLayoutPanel
```

classes.

### Step 5 — Identify core logic

Find:

```text
services
processors
image-processing classes
ML classes
utility classes
```

### Step 6 — Trace the workflow

Determine:

```text
User action
    ↓
Form event
    ↓
Application/service logic
    ↓
Image processing
    ↓
ML/analysis
    ↓
Result
    ↓
UI
```

### Step 7 — Only then modify code

Before making changes, identify:

- files that need modification
- existing methods that should be reused
- dependencies affected
- potential UI impact
- potential performance impact

---

# 23. Important Rule for Claude / AI Coding Agents

Do not assume a class belongs to a particular architectural layer based only on its filename.

For example:

```text
ImageProcessor.cs
```

does not automatically mean it is the primary image-processing service.

Read the implementation and references first.

Similarly:

```text
MainForm.cs
```

does not automatically mean all application logic belongs there.

Trace actual call relationships.

---

# 24. Change Safety

When modifying existing functionality:

### Preserve

- Existing public APIs
- Existing event behavior
- Existing file formats
- Existing image-processing behavior
- Existing model behavior
- Existing configuration
- Existing user workflows

unless the requested change explicitly requires modification.

### Avoid

- Unrelated refactoring
- Large architectural rewrites
- Package upgrades without justification
- Renaming many classes
- Moving files unnecessarily
- Changing project target frameworks
- Replacing ML libraries without a requirement

Keep changes focused.

---

# 25. Dependency Change Rules

Before adding a NuGet package:

1. Check whether an existing package already provides the functionality.
2. Check compatibility with .NET 8.
3. Check whether the package is required at runtime.
4. Check whether it introduces native dependencies.
5. Check whether it affects deployment.
6. Explain why it is required.

Do not add dependencies merely for convenience.

---

# 26. Testing Requirements

After modifying code:

### Minimum

```text
Build solution
↓
Fix compilation errors
↓
Run affected functionality
↓
Check UI layout
↓
Check error handling
```

For image-processing changes:

```text
Valid image
Invalid image
Large image
Unsupported image
Multiple images
```

should be considered where applicable.

For UI changes:

```text
Normal window
Maximized window
Resized window
Different DPI
Long text
Missing/invalid input
```

should be considered.

---

# 27. Git Hygiene

Do not commit generated files.

Avoid committing:

```text
bin/
obj/
.vs/
user-specific settings
temporary files
large generated outputs
secrets
credentials
machine-specific configuration
```

Do not commit API keys, passwords, connection strings containing credentials, or other secrets.

---

# 28. AI Response Requirements

When asked to change code, provide:

### 1. Understanding

Briefly explain what the existing implementation does.

### 2. Change plan

List the files that need to change.

### 3. Implementation

Make the smallest appropriate change.

### 4. Validation

Explain how the change was validated.

### 5. Side effects

Mention any important behavioral, dependency, UI, or performance impact.

Do not claim that code was tested if it was not actually built or executed.

---

# 29. Preferred Development Style

Use modern C# where appropriate:

```csharp
nullable reference types
using declarations
async/await
pattern matching
target-typed new
file-scoped namespaces
```

But do not modernize existing code purely for stylistic reasons.

Consistency with the surrounding code is more important than introducing new syntax everywhere.

---

# 30. Core Principle

The project prioritizes:

1. Correctness
2. Maintainability
3. Stable existing behavior
4. Clean WinForms layout
5. Separation of UI and processing logic
6. Resource safety
7. Performance for image/ML workloads
8. Small, focused changes

When in doubt:

```text
Understand existing code
        ↓
Reuse existing architecture
        ↓
Make the smallest safe change
        ↓
Build
        ↓
Validate
```

Do not rewrite working code unnecessarily.