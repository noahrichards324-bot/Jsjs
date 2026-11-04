# Horizon UWP Engine Bridge

This crate is the next link stage after the Rust UWP smoke probe. It depends on the pinned upstream `horizon-client` package and builds as a Windows UWP-compatible native DLL.

The exported `horizon_uwp_engine_entrypoint_address` returns the address of Horizon's current native entrypoint only as a link anchor. It is **not an Xbox launch function**: the existing entrypoint starts Horizon's desktop `winit` event loop and must not be called by the UWP shell until a UWP host/window adapter replaces that desktop lifecycle.

The bridge currently proves native linking, not gameplay or UWP runtime compatibility. It does not yet package into the XAML app, initialize a UWP renderer surface, consume app input events, or load worlds.


## Input event ABI

A bounded 4096-event queue is exposed for a future UWP host:

- `horizon_uwp_input_push(kind, code, state, x, y)`
- `horizon_uwp_input_pop(out_event)`
- `horizon_uwp_input_pending()`
- `horizon_uwp_input_clear()`

The C layout and constants are in `include/horizon_uwp_engine_bridge.h`. Event kinds cover keyboard, mouse buttons/motion/wheel, gamepad buttons/axes, focus, resize and text. The queue is not yet consumed by Horizon's game loop, so these calls do not currently control gameplay.
