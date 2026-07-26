# Hierarchy Component Icons
Adds component icons to a separate column in the Unity Hierarchy.

<img width="1828" height="431" alt="{44490891-D304-4005-8725-F48D94F015FF}" src="https://github.com/user-attachments/assets/82592833-6265-429e-a4d4-73600cb182c3" />

## Features

- Displays icons for all GameObject components.
- Left, center, or right alignment.
- Component filtering via Settings and TypeCache.
- Case-insensitive multi-word search.
- Search match highlighting.
- Transform and RectTransform ignored by default.

## Compatibility

Uses reflection to access the internal Hierarchy API in early Unity 6.3 versions.

## Installation

Copy `HierarchyComponentIcons.cs` to:

```text
Assets/Editor/
```

Open settings via:

```text
Edit → Preferences → Hierarchy Component Icons
```
