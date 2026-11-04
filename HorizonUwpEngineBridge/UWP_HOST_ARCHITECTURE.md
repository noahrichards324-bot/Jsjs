# Xbox UWP host and renderer architecture

This note records the platform adapter design for the Horizon port. It is based on the pinned Horizon client and Microsoft's UWP DirectX guidance.

## Renderer host

The Horizon renderer is built on `wgpu 30.0.1`. The current version's `SurfaceTargetUnsafe` public API exposes a `RawHandle` target. Its DX12 HAL path creates a surface from a Win32 `HWND`. The existing game startup creates a `winit::Window`, then creates the renderer surface from that window.

A UWP XAML game should not be treated as a desktop HWND app. Microsoft's UWP guidance recommends `SwapChainPanel` and a DXGI composition swap chain for low-latency game rendering. The app obtains the native `ISwapChainPanelNative` interface and attaches the `IDXGISwapChain1` with `SetSwapChain`.

Therefore the renderer port has two possible engineering routes:

1. **Preferred investigation: extend the renderer surface boundary.** Add a UWP composition-surface target to the pinned wgpu/wgpu-hal source so Horizon can create/manage a surface backed by the UWP composition swap chain. This requires a deliberate patch across wgpu's surface target, core surface construction and the DX12 HAL. The target must retain the COM objects for the surface lifetime and follow resize/device-loss rules.
2. **Alternative investigation: use the UWP CoreWindow path.** Verify whether Xbox's UWP app model exposes a supported native surface handle usable by the existing DX12 HAL. Do not assume a desktop HWND can be created or used in an AppContainer.

The implementation must use the actual Xbox-supported D3D12 device/swapchain and validate format, frame latency, resize, suspend/resume and device removal. A normal desktop `HWND` surface is not an acceptable UWP proof.

## Input host

The UWP page already receives CoreWindow keyboard events, pointer events and Windows.Gaming.Input controller samples.

The native bridge now exposes a bounded event queue using a C ABI:
- `horizon_uwp_input_push(kind, code, state, x, y)`
- `horizon_uwp_input_pop(out_event)`
- `horizon_uwp_input_pending()`
- `horizon_uwp_input_clear()`

Header: `HorizonUwpEngineBridge/include/horizon_uwp_engine_bridge.h`.

The event kinds are stable ABI values in the header. The UWP host should translate virtual-key codes, mouse buttons/deltas/wheel, gamepad buttons/axes, focus, size and text into those messages. The Rust game loop still needs a host-input adapter that drains the queue and converts events into Horizon's existing keymapping, mouse-look and controller systems. Until that consumer is written, the queue does not affect gameplay.

For lower-latency input over a game render panel, Microsoft documents `SwapChainPanel.CreateCoreIndependentInputSource`. It must be created on a non-UI thread and only starts delivering input after a swap chain is attached and the panel has rendered at least once. That is an implementation detail for the eventual native host.

## App lifecycle

A host adapter must own:
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
| Native DLL link | Bridge crate and CI link job added; result pending |
| UWP input-to-Rust ABI | Bounded C ABI queue and header added; tests are in source, game-loop consumer not yet connected |
| UWP game renderer | Not ported; requires composition-surface/CoreWindow integration |
| UWP game lifecycle | Not ported |
| Save paths and asset download | Not ported |
| Multiplayer/network capabilities | Not verified |
| Microsoft sign-in and entitlements | Not integrated into this app |
| Xbox Series S runtime | Not tested on device |

This is a work plan and evidence ledger, not a claim of a playable Minecraft port.
