# Horizon Xbox UWP engine-port work

This is the separate engine-port workstream. The existing `UwpSource` app is still only the XAML front end; the goal here is to test and adapt the real upstream Rust client rather than fake gameplay in the shell.

## First milestone: compile the real client for x64 UWP

The Xbox package currently targets x64. Rust has an x64 UWP target, but it is Tier 3 and does not ship a prebuilt standard library. The CI workflow therefore uses nightly Rust plus `rust-src` and Cargo `-Z build-std`.

The first CI pass does two independent checks:

1. Builds a tiny Rust static library for `x86_64-uwp-windows-msvc` to verify the runner can produce a Rust UWP library artifact.
2. Runs `cargo check` against the actual upstream `horizon-client` package for that target, with optional desktop audio/controller features disabled.

Upstream snapshot under test: `horizon-rust/horizon` commit `83ad384f48d0742746d518737511882680aab5b7` (2026-11-04).

## What this first pass does not do

A successful Rust UWP toolchain smoke test is not proof that Horizon itself compiles. A successful `cargo check` is not proof the game can render on an Xbox. The client currently expects a desktop event loop/window via `winit`; UWP needs an app-owned window/lifecycle adapter and a renderer surface that can target a UWP/XAML host. Those need to be designed against actual compiler errors and the Xbox APIs.

No changes are made to the upstream project or its sign-in/entitlement logic in this milestone. The legitimate Minecraft account ownership checks remain required.

## Port stages

- [ ] UWP target smoke test and actual-client compile probe
- [ ] UWP host/window lifecycle and render-surface adapter
- [ ] Keyboard, mouse capture/relative look and controller bridge into Horizon input
- [ ] UWP app-storage path and file-picker adaptation
- [ ] UWP network capability and multiplayer validation
- [ ] Legitimate Microsoft/Minecraft sign-in configuration
- [ ] UWP package integration and repeatable CI build
- [ ] Install and test on a physical Xbox Series S

The checklist will only be marked complete when there is build or device evidence for it.
