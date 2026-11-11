# Changelog

All notable changes to Sticky Monitor. Versions follow the installer's version.

## 1.1.0 - 2026-11-11

Fixes the crash reports, the shutdown-popup and the missed pins caused by the external helper process.

### Changed

- **Virtual desktop access is now in-process.** The app no longer spawns
  `VirtualDesktop11-24H2.exe` (Windows 10: `VirtualDesktop.exe`, earlier Windows 11:
  `VirtualDesktop11.exe`) once per call. The shell COM interfaces live in
  `src/StickyMonitor.App/Interop/VirtualDesktopInterop.cs`, a trimmed copy of
  [Markus Scholtes' VirtualDesktop](https://github.com/MScholtes/VirtualDesktop) V1.21 (MIT).
  - Removes up to dozens of process launches per evaluation, and the 3 s
    `WaitForExit` + `Kill(entireProcessTree: true)` that every call went through.
  - Removes the `Application Error` dialog that could appear during shutdown: the helper
    failed to start with `0xc0000142` when spawned into a session that was already being
    torn down, and that modal box held the shutdown open.
- **`IApplicationView` is passed as a raw interface pointer.** The vendored interface was
  declared `ComInterfaceType.InterfaceIsIInspectable`, which the .NET 5+ runtime refuses to
  marshal (`PlatformNotSupportedException: Marshalling as IInspectable is not supported in
  the .NET runtime`). Pinning works because the pointer is passed through untouched, and
  the application user model id now comes from the documented `GetApplicationUserModelId`.
- **Windows that cannot be pinned back off.** A window that yields no application view
  (elevated, shell-owned) used to be retried every evaluation, which is what produced 231
  WER reports in four minutes on 2026-10-30. Failures now back off 2, 4, 8, then 15 minutes,
  and only one warning per 5 minutes is logged.
- **The shell COM objects are rebuilt after an Explorer restart.** They were cached in a
  static constructor, so a stale pointer broke every later call until the app restarted.

### Removed

- The bundled `VirtualDesktop*.exe` helper binaries, the unused `VirtualDesktop` NuGet
  package (Grabacr07's wrapper, which does not cover current builds), and the
  helper-timeout code path.
- Windows 10 and Windows 11 pre-24H2 support: only the 24H2+ COM layout is shipped. The
  original helper sources stay in `vendor/` for anyone who wants to restore that path.

### Notes

- Verified on Windows 11 26H2, build 26300.9818: pinning Notepad and Discord on the sticky
  display reports them pinned to every desktop, working set dropped from ~155 MB to ~82 MB,
  and no new WER reports are produced.
