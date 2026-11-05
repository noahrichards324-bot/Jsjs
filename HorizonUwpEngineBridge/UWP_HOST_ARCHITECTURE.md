# Xbox UWP host and renderer architecture

This note records the platform adapter design for the Horizon port, based on the pinned Horizon client and Microsoft's UWP DirectX guidance.

## Renderer host: the pinned wgpu already supports SwapChainPanel

An important source check found that the pinned `wgpu 30.0.1` release already includes `SurfaceTargetUnsafe::SwapChainPanel` under its DX12 feature. Its wgpu-core and DX12 HAL implementations create a composition swap chain and attach it with `ISwapChainPanelNative::SetSwapChain`. Horizon's local `third_party/wgpu-hal` source also contains the same implementation.

Relevant source:
- wgpu 30.0.1 surface target: https://github.com/gfx-rs/wgpu/blob/v30.0.1/wgpu/src/api/surface.rs
- wgpu 30.0.1 DX12 HAL: https://github.com/gfx-rs/wgpu/blob/v30.0.1/wgpu-hal/src/dx12/mod.rs
- Horizon renderer startup: https://github.com/horizon-rust/horizon/blob/main/crates/horizon-client-render/src/gfx.rs

So a graphics-library upgrade is not the first requirement. The native host can pass an `ISwapChainPanelNative` interface pointer to wgpu's unsafe surface constructor:

`Instance::create_surface_unsafe(SurfaceTargetUnsafe::SwapChainPanel(panel_ptr))`

The new bridge includes a first surface probe:
- `horizon_uwp_surface_create(panel, width, height)`
- `horizon_uwp_surface_resize(host, width, height)`
- `horizon_uwp_surface_present(host)`
- `horizon_uwp_surface_destroy(host)`

It creates a DX12-only wgpu instance, obtains an adapter compatible with the panel surface, creates a device/queue, configures a FIFO swap chain, clears a test frame and presents it. The UWP Engine Test page obtains the `ISwapChainPanelNative` interface with COM `QueryInterface`, calls the native bridge and releases its temporary COM references. The wgpu HAL retains its own interface reference.

This is a platform graphics smoke test, not Horizon's world renderer. GitHub CI can compile and package the code, but only a real UWP/Xbox run can establish that the Xbox driver/device permits the full surface lifecycle.

## Remaining Horizon renderer refactor

Horizon’s `Renderer` currently stores `Arc<winit::window::Window>`, reads the window size, creates/recreates surfaces from the window, and watches desktop display/HDR state. The game startup creates the `winit` window and uses its event loop.

The next refactor should introduce a platform-neutral render target, for example:

- desktop target: existing `Arc<Window>` path;
- UWP target: retained `ISwapChainPanelNative` pointer plus host-supplied pixel size.

Then update renderer startup, surface recreation, resize, present and shutdown to use that target. UWP should disable/replace desktop-only monitor/HDR/fullscreen behavior as appropriate. A test clear frame is not a substitute for that renderer refactor.

## Input host

The UWP page receives CoreWindow keyboard events, pointer events and Windows.Gaming.Input controller samples.

The native bridge exposes a bounded 4096-event C ABI queue:
- `horizon_uwp_input_push(kind, code, state, x, y)`
- `horizon_uwp_input_pop(out_event)`
- `horizon_uwp_input_pending()`
- `horizon_uwp_input_clear()`

Header: `HorizonUwpEngineBridge/include/horizon_uwp_engine_bridge.h`.

The current Input Test page round-trips sample key, mouse and controller events through Rust, which confirms the app/native ABI wiring when the DLL is packaged. The queue is not yet drained by Horizon's actual gameplay loop. The next step is to write a game-host input adapter which drains it and calls Horizon's own keybind, mouse-look and controller paths. Pointer capture/relative motion, focus-loss release, keyboard text and gamepad dead zones also need careful handling.

For low-latency input over a game render panel, Microsoft documents `SwapChainPanel.CreateCoreIndependentInputSource`. It must be created on a non-UI thread and starts delivering input only after the swap chain is attached and the panel has rendered at least once.

## App lifecycle

A UWP host adapter must own:
- startup and shutdown without calling the desktop `winit` event loop;
- frame scheduling while active;
- pause/resume, focus loss and release of held keys/buttons;
- renderer surface resize and recreation after device loss;
- orderly world save and engine shutdown on suspend/termination.

The existing `horizon_client::entry::main` function is only a link anchor in the current DLL. It starts the desktop event-loop path and must not be invoked from UWP.

## Storage, networking and identity

- Map game-home, options, logs, worlds, caches, resource packs and downloaded assets onto UWP app storage. Use app-owned paths by default and explicit file/folder pickers for user-selected locations.
- Verify DNS, TLS, outbound TCP, UDP, server discovery and LAN hosting against Xbox's UWP app-container restrictions. Network capabilities in the manifest are necessary but do not prove every transport works.
- Keep Horizon's Microsoft/Minecraft sign-in and entitlement checks. A UWP registration must use an application/client ID authorized for the required Minecraft services; never borrow another launcher's client ID or skip entitlements.
- Include privacy notices and platform permissions where upstream telemetry or voice features would otherwise require consent or unavailable system APIs.

## Current evidence and remaining work

| Area | Evidence / state |
|---|---|
| Rust x64 UWP target | CI smoke static library passed |
| Actual Horizon client | Pinned `horizon-client` passed `cargo check` for x64 UWP |
| wgpu DX12 SwapChainPanel | Already present in pinned wgpu 30.0.1 and local wgpu-hal |
| Native input ABI | Bounded C ABI queue; Input Test page round-trips diagnostic samples |
| UWP SwapChainPanel host | Native create/configure/clear/present/resize/destroy probe added |
| C# COM interface hookup | Engine Test page queries `ISwapChainPanelNative` and calls the Rust probe |
| Native DLL link and app package | CI validation pending |
| Horizon game renderer | Not yet using the UWP panel surface |
| Horizon event loop / gameplay input | Not yet using the UWP host/input queue |
| Save paths and asset download | Not ported |
| Multiplayer/network capabilities | Not verified |
| Microsoft sign-in and entitlements | Not integrated into this app |
| Xbox Series S runtime | Not tested on device |

This is a work plan and evidence ledger, not a claim of a playable Minecraft port.
