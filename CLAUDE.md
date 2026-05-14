# Project: Image_Checker

## Architecture
- Multi-project solution:
  - Image_Checker (core logic)
  - Image_Checker.Forms (UI logic)
  - Image_Checker.WinForm (entry/UI)

## Tech Stack
- Language: C#
- Framework: WinForms (.NET)

---

## PRIMARY OBJECTIVE
Generate clean, modern, non-overlapping WinForms UI with structured layouts.

---

## WinForms Layout System (STRICT)

### Layout Rules (MANDATORY)
- NEVER place controls using absolute positioning only
- ALWAYS use:
  - TableLayoutPanel (preferred)
  - FlowLayoutPanel (for dynamic rows)
  - Panel (for grouping)

### Spacing System
- Outer margin: 16px
- Inner padding: 8px
- Vertical spacing: 10–12px
- Label → Input gap: 6px

### Docking Rules
- Use `Dock = Fill` for main containers
- Avoid excessive Anchor combinations
- Use consistent alignment

---

## UI Design Rules

### Grouping
- Related fields MUST be inside GroupBox or Panel
- Each section must have a clear title

### Inputs
- Uniform height for TextBox, ComboBox, Button
- Width alignment across rows

### Buttons
- Place inside bottom panel
- Right-aligned
- Consistent spacing

---

## Code Rules

- Separate UI logic from business logic
- Do not mix data processing inside UI code
- Always validate user inputs
- Handle exceptions properly

---

## Anti-Patterns (STRICTLY FORBIDDEN)

- ❌ Overlapping controls
- ❌ Random pixel positioning everywhere
- ❌ Mixing multiple layout strategies incorrectly
- ❌ Huge Form with no sections
- ❌ Inline magic numbers without consistency

---

## Expected Output Style

When generating UI:
1. Create layout panels first
2. Then add controls inside them
3. Maintain spacing and alignment
4. Ensure readability and maintainability

---

## Notes
This project prioritizes:
- Clean UI
- Maintainability
- Structured layout over quick hacks