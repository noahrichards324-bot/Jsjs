# Horizon game-engine port status for Xbox UWP

## Current state

The UWP project in this repository is a C# / XAML starter shell. It is not the Horizon game client and it does not currently contain the Rust game engine. The Controls page stores configurable bindings and the Input Test page receives app-level keyboard and pointer events, but there is no gameplay renderer or engine input consumer connected.

Do not describe this package as playable Minecraft.

## Upstream engine facts

The upstream Horizon project is a Rust implementation of Minecraft: Java Edition. Its native client is a multi-crate Rust workspace. The client depends on `winit` for its window/event loop and `wgpu` for graphics. The current `winit` Windows backend is the Win32 backend; upstream does not provide a documented UWP/WinRT window backend.

References:
- Horizon source: https://github.com/horizon-rust/horizon
- Client dependencies: https://github.com/horizon-rust/horizon/blob/main/crates/horizon-client/Cargo.toml
- Input/window crate: https://github.com/horizon-rust/horizon/tree/main/crates/horizon-client-input
- winit Windows platform support: https://docs.rs/winit/0.30.13/winit/platform/windows/index.html
- Rust UWP target notes: https://doc.rust-lang.org/rustc/platform-support/uwp-windows-msvc.html

Rust's `*-uwp-windows-msvc` targets are Tier 3. The Rust documentation says prebuilt standard-library artifacts are not shipped for these targets, so a UWP build needs a custom standard library build. It also warns that Win32 APIs can be restricted or unavailable in UWP AppContainer.

## Work required before gameplay can run

1. **Window and lifecycle adapter:** replace or add a UWP/WinRT window/event-loop backend. The existing `winit` Win32 window is not a UWP host.
2. **Graphics surface:** connect Horizon's renderer to a UWP-compatible Direct3D 12 / swap-chain surface and handle Xbox device lifecycle, focus, resize and suspend/resume.
3. **Input bridge:** feed UWP keyboard events, mouse buttons, relative mouse-look deltas, wheel events and controller state into Horizon's game input system. Capturing pointer events in a XAML diagnostics panel is not the same as game camera capture.
4. **Storage port:** adapt the engine/launcher paths to UWP app storage and approved file-picker access. UWP apps run with an app-container file-access model.
5. **Networking and multiplayer:** add the appropriate UWP network capabilities and verify TCP/UDP, DNS, TLS and LAN discovery/hosting against Xbox restrictions. A manifest capability alone does not implement multiplayer.
6. **Legitimate account flow:** integrate Horizon's own supported Microsoft/Minecraft sign-in and entitlement checks. Do not reuse another application's client ID or skip ownership checks. Horizon's upstream documentation says its launcher uses its own registered client ID and an account that owns Java Edition.
7. **Xbox validation:** build, package, install and test on an actual Xbox Series S; a Windows desktop CI build alone cannot verify Xbox runtime compatibility.

## Safe, honest milestone tracking

| Area | Status |
|---|---|
| UWP shell and navigation | Implemented in this repository |
| Editable keyboard bindings and F1-F12 options | Implemented in the shell |
| Mouse button mapping and sensitivity settings | Implemented in the shell |
| App-level input diagnostics | Implemented in the shell |
| Horizon renderer on Xbox | Not ported |
| Minecraft gameplay and world loading | Not connected |
| Game camera capture / mouse-look | Not connected to a game |
| Online and LAN multiplayer | Not connected |
| Microsoft sign-in / ownership verification | Not integrated |
| Physical Xbox verification | Not done |

This document is a porting plan, not a claim that the listed engine work has been completed.
