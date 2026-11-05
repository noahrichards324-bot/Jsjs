# Horizon UWP Engine Bridge

This crate links the pinned upstream Horizon client and exposes C ABI entrypoints for the UWP host.

## Input event ABI

A bounded 4096-event queue is exposed for the UWP host:

- `horizon_uwp_input_push(kind, code, state, x, y)`
- `horizon_uwp_input_pop(out_event)`
- `horizon_uwp_input_pending()`
- `horizon_uwp_input_clear()`

The C layout and constants are in `include/horizon_uwp_engine_bridge.h`. Event kinds cover keyboard, mouse buttons/motion/wheel, gamepad buttons/axes, focus, resize and text. The UWP Input Test page sends diagnostic samples through this ABI and immediately drains them. The queue is not yet consumed by Horizon's gameplay loop.

## DX12 SwapChainPanel surface probe

The pinned wgpu 30.0.1 already includes a DX12 `SurfaceTargetUnsafe::SwapChainPanel` variant and corresponding DX12 HAL implementation. This crate now exposes:

- `horizon_uwp_surface_create(panel, width, height)`
- `horizon_uwp_surface_last_error()`
- `horizon_uwp_surface_resize(host, width, height)`
- `horizon_uwp_surface_present(host)`
- `horizon_uwp_surface_destroy(host)`

The create function accepts an `ISwapChainPanelNative` COM interface pointer, creates a DX12 wgpu instance/surface, obtains an adapter and device, configures a FIFO swap chain, clears a test frame and presents it. The UWP Engine Test page obtains that interface from its XAML `SwapChainPanel` and calls the native bridge.

This is a graphics-host probe only. It does not use Horizon's world renderer or load Minecraft. Horizon's renderer still assumes a desktop `winit::Window`; it needs to be refactored to accept the UWP panel target and host-driven size/lifecycle.

## Important entrypoint warning

The exported `horizon_uwp_engine_entrypoint_address` is a link anchor only. It must not be called: Horizon's existing entrypoint starts its desktop `winit` event loop. The game loop and window lifecycle have not yet been replaced with a UWP host.
