# UI Style Guide

Standards for the Registration System Blazor UI. Follow these patterns when adding or modifying pages.

---

## Status Badges

Use the shared `<StatusBadge>` component (`Components/Shared/StatusBadge.razor`). Never define status colors or display names inline.

### Usage

```razor
@* Default size (tables, lists) *@
<StatusBadge Status="registration.Status" />

@* Large size (detail panels, headers) *@
<StatusBadge Status="registration.Status" Size="StatusBadge.StatusBadgeSize.Large" />

@* Mini size (compact inline displays) *@
<StatusBadge Status="registration.Status" Size="StatusBadge.StatusBadgeSize.Mini" />
```

### Static Helpers

When you need just the color or name string (e.g., chart bars):

```csharp
StatusBadge.GetDisplayName(status)   // "Awaiting Review"
StatusBadge.GetBgColor(status)       // "bg-emerald-600"
StatusBadge.GetShortName(status)     // "Awaiting"
```

### Color Map

| Status         | Color        | Tailwind Class   |
|----------------|--------------|------------------|
| AwaitingReview | Emerald      | `bg-emerald-600` |
| Pending        | Amber        | `bg-amber-500`   |
| Reviewed       | Cyan         | `bg-cyan-600`    |
| Verified       | Violet       | `bg-violet-600`  |
| Withdrawn      | Dark Amber   | `bg-amber-700`   |
| Disqualified   | Gray         | `bg-gray-600`    |

---

## Page Headers

Every page uses the same header pattern. No back buttons.

```html
<div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 mb-6">
    <div>
        <h1 class="text-2xl font-bold text-slate-800">Page Title</h1>
        <p class="text-sm text-slate-500 mt-1">Subtitle or context</p>
    </div>
    <!-- Optional: right-side actions (buttons, badges, stats) -->
</div>
```

- Title: `text-2xl font-bold text-slate-800`
- Subtitle: `text-sm text-slate-500 mt-1`
- Bottom margin: `mb-6`
- No back buttons (users rely on browser navigation)

---

## Buttons

All interactive buttons use `rounded-xl`. Three variants:

### Primary (gradient)
```
rounded-xl bg-gradient-to-r from-cyan-600 to-cyan-700 hover:from-cyan-700 hover:to-cyan-800
text-white text-sm font-semibold transition-all shadow-sm
disabled:opacity-50
cursor-pointer
```

### Secondary (outlined)
```
rounded-xl border border-slate-200 bg-white hover:bg-slate-50
text-slate-600 text-sm font-medium transition-colors
cursor-pointer
```

### Danger
```
rounded-xl bg-red-600 hover:bg-red-700
text-white text-sm font-semibold transition-colors
cursor-pointer
```

---

## Form Inputs

All text inputs, selects, and textareas:

```
w-full rounded-xl border border-slate-300 px-4 py-2.5 text-sm
focus:outline-none focus:ring-2 focus:ring-cyan-500 focus:border-cyan-500
transition-shadow
```

Key rules:
- Border radius: `rounded-xl`
- Focus ring: `focus:ring-2` (not `ring-1`)
- Padding: `px-4 py-2.5`
- Add `cursor-pointer` on selects

---

## Cards & Panels

```
rounded-xl border border-slate-200 bg-white shadow-sm overflow-hidden
```

- Content padding: `p-5`
- Header: `px-5 py-4 border-b border-slate-200 bg-slate-50`
- Footer: `px-5 py-4 border-t border-slate-200 bg-slate-50`

---

## Empty States

When a page or section has no data:

```html
<div class="rounded-xl border border-slate-200 bg-gradient-to-br from-slate-50 to-white p-12 text-center shadow-sm">
    <div class="w-16 h-16 rounded-2xl bg-gradient-to-br from-cyan-500 to-cyan-700 flex items-center justify-center mx-auto mb-5 shadow-lg">
        @Icons.SomeIcon("h-7 w-7 text-white")
    </div>
    <h2 class="text-xl font-semibold text-slate-800 mb-2">Title</h2>
    <p class="text-slate-500 text-sm">Description</p>
</div>
```

---

## Loading States

```html
<div class="flex flex-col items-center justify-center min-h-[60vh]">
    <div class="w-16 h-16 rounded-2xl bg-cyan-100 flex items-center justify-center mb-4">
        @Icons.Spinner("h-8 w-8 text-cyan-600")
    </div>
    <h2 class="text-lg font-semibold text-slate-800 mb-1">Loading...</h2>
    <p class="text-sm text-slate-500">Loading message</p>
</div>
```

---

## Table Headers

Dark gradient header for data tables:

```
bg-gradient-to-r from-slate-700 to-slate-800 text-white text-xs font-semibold uppercase tracking-wider
```

---

## Modals

Use the shared `<Modal>` component (`Components/Shared/Modal.razor`) when possible. For custom modals:

```html
<div class="fixed inset-0 z-50 overflow-y-auto">
    <div class="flex min-h-full items-center justify-center p-4">
        <div class="fixed inset-0 bg-slate-900/50 transition-opacity" @onclick="Close"></div>
        <div class="relative w-full max-w-md rounded-2xl bg-white shadow-2xl">
            <!-- Content with px-6 py-5 -->
        </div>
    </div>
</div>
```

---

## Navigation Structure

Sidebar admin links are grouped into labeled sections:

```
Registrations    Dashboard, Registration Review, Registration List
Competition      Competition Settings, Competition Tracking
System           Niqab Bypasses, Audit Trail, Admin Tools
```

Section labels: `px-4 text-xs font-semibold tracking-wide uppercase text-slate-400 mb-2`

---

## Color Palette

| Role            | Color          | Tailwind          |
|-----------------|----------------|-------------------|
| Primary action  | Cyan gradient  | `cyan-600/700`    |
| Neutral text    | Slate          | `slate-500-800`   |
| Success         | Emerald        | `emerald-600`     |
| Warning         | Amber          | `amber-500`       |
| Error/Danger    | Red            | `red-600`         |
| Secondary       | Violet         | `violet-600`      |
| Brand primary   | Cyan-800       | `#155e75`         |
| Brand accent    | Amber-400      | `#fbbf24`         |

---

## Icons

Always use the `Icons.razor` component. Never inline SVGs for standard icons.

```razor
@Icons.CheckCircle("h-4 w-4")
@Icons.Spinner("h-4 w-4 animate-spin")
```

---

## Authorization

All admin pages must include:

```razor
@attribute [Authorize(Roles = "Admin")]
```
