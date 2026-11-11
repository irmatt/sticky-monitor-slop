# Sticky Monitor

Sticky Monitor is a native Windows desktop utility that lets you mark specific displays as "sticky" so windows on those displays stay visible across virtual desktop switches.

Windows does not support true per-monitor virtual desktops, so this app simulates the behavior by pinning windows and reapplying sticky state when needed.

## Who this is for

Sticky Monitor is useful if you:

- keep chat, dashboards, docs, or media on one display
- switch virtual desktops often
- want one monitor to stay constant while other monitors follow normal desktop behavior

## What it does

- shows your monitor layout in the same relative arrangement as Windows
- lets you mark monitors as pinned/sticky
- keeps eligible windows on those monitors visible across desktops
- supports automatic mode and manual apply mode
- runs as a tray app with optional launch at login

## Requirements

- Windows 10 version 2004 or newer
- multiple monitors recommended
- works best with normal desktop applications

## Build requirements

To compile from source on another Windows PC, install:

- .NET 8 SDK

Then make sure this command works in a normal terminal:

- `dotnet --version`

Node.js is only needed if you want to regenerate the icon assets locally.

## Compatibility

- Windows 11 **24H2 or newer** (verified on Windows 11 26H2, build 26300)
- Windows 10 is **not** supported by 1.1.0 and later

Virtual desktop APIs are undocumented and change between Windows builds. Sticky Monitor talks to them **in-process** (see [How it works](#how-it-works)); the older builds' interop is no longer shipped. Virtual desktop behavior still depends on Windows internals and some apps resist manipulation - elevated windows and some shell windows cannot be pinned, and those are skipped rather than retried.

## How it works

Sticky Monitor reads and pins windows through the shell's virtual desktop COM interfaces directly, inside the app process:

- `Interop/VirtualDesktopInterop.cs` - a trimmed copy of [Markus Scholtes' VirtualDesktop](https://github.com/MScholtes/VirtualDesktop) V1.21 COM plumbing (MIT), with the original `IInspectable`-based `IApplicationView` replaced by raw interface pointers because the .NET 5+ runtime cannot marshal `IInspectable`. The application user model id comes from the documented `GetApplicationUserModelId` instead.
- Windows that refuse to provide an application view (elevated windows, shell-owned windows) throw, and the throw is caught per window with a back-off (2, 4, 8, then 15 minutes) instead of being retried every few seconds.

Versions up to 1.0.0 shelled out to a `VirtualDesktop*.exe` helper once per call. That produced up to dozens of processes per evaluation, and a window that could not be pinned crashed the helper with an unhandled exception, which Windows Error Reporting surfaced as an "Application Error" dialog that could block shutdown.

## Quick Start

### Option 1: Portable

Use this if you want to run it without installing:

- open `packages\StickyMonitor-Portable\`
- run `StickyMonitor.App.exe`

Or share:

- `packages\StickyMonitor-Portable.zip`

### Option 2: Per-user install

Use this if you want a Start Menu shortcut and a normal installed location:

- open `packages\StickyMonitor-Installer\`
- right-click `install-package.ps1`
- run with PowerShell

This installs to:

- `%LocalAppData%\Programs\StickyMonitor`

And creates:

- a Start Menu shortcut for `Sticky Monitor`

No admin rights are required.

## How to use

1. Start the app.
2. Select a monitor in the layout.
3. Turn the monitor on as pinned/sticky.
4. Use either:
   - `Auto` mode to keep sticky monitors enforced automatically
   - `Manual` mode to apply sticky behavior only when you press `Apply Now`

## Launch At Login

If enabled, Sticky Monitor starts minimized to the tray when you sign in.

## What counts as a sticky monitor

When a monitor is marked sticky:

- windows on that monitor are pinned across virtual desktops when possible
- windows moved onto that monitor inherit sticky behavior
- windows moved off that monitor return to normal behavior

## Known limitations

- Windows does not expose true per-monitor virtual desktops
- elevated apps may not be controllable from a non-elevated app
- fullscreen, cloaked, shell-managed, or protected windows may be ignored
- some applications behave differently from normal desktop windows
- behavior can vary across Windows 10 and Windows 11 builds

## If something looks wrong

Try this first:

- close and reopen the app
- toggle the affected monitor off and back on
- switch between `Auto` and `Manual`
- in `Manual` mode, press `Apply Now`

If the app starts but appears slow right after sign-in, wait a moment and reopen it from the tray. Monitor and desktop state are now loaded in a more startup-safe way, but Windows sign-in timing can still vary by machine.

## Build from source

```powershell
cd sticky-monitor
dotnet publish src/StickyMonitor.App/StickyMonitor.App.csproj -c Release -r win-x64 --self-contained true -o dist/app
```

Or, if you prefer the npm wrapper:

```powershell
cd sticky-monitor
npm run build
```

## Create packages

```powershell
cd sticky-monitor
npm run package
```

This creates:

- `packages\StickyMonitor-Portable\`
- `packages\StickyMonitor-Portable.zip`
- `packages\StickyMonitor-Installer\`

## Regenerate icons

Only needed if you want to rebuild the packaged icon assets:

```powershell
cd sticky-monitor
npm install
npm run build:with-icons
```

## Source layout

- `src\StickyMonitor.App\` - native WPF app
- `vendor\` - virtual desktop helper binaries
- `scripts\` - build and packaging scripts
- `packages\` - generated distributable outputs
