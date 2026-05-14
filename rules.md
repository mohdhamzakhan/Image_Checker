# Advanced WinForms Rules

## Layout Engine

Always follow this hierarchy:

Form
 └── Main Panel (Dock: Fill)
      ├── Header Panel (Top)
      ├── Content Panel (Fill)
      │     └── TableLayoutPanel
      └── Footer Panel (Bottom)

---

## TableLayoutPanel Rules

- Use column structure:
  - Label (30%)
  - Input (70%)

- AutoSize = true where possible
- Use consistent row heights

---

## Control Placement

- Never manually calculate X/Y unless absolutely necessary
- Prefer layout containers over coordinates

---

## Responsiveness

- Use Dock and AutoSize
- Avoid fixed widths where possible
- Ensure resizing doesn’t break layout

---

## Naming Convention

- txtName, cmbType, btnSubmit
- pnlMain, tblLayout, grpDetails

---

## Error Handling

- Show user-friendly messages
- Avoid crashes in UI thread

---

## Performance

- Avoid unnecessary redraws
- Use SuspendLayout / ResumeLayout when needed