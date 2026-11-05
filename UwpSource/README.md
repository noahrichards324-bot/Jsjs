# Horizon Xbox UWP shell preview

This folder contains the UWP page resources copied into the starter project during CI.

## Included in this milestone
- TV-sized Home, Input Test, Controls and About screens.
- Keyboard navigation for app menus without using WASD for menu focus.
- Editable Horizon-style key bindings, including F1-F12, saved in UWP local settings.
- Remappable attack, use/place and pick-block mouse buttons.
- Saved mouse sensitivity and inverted-Y setting.
- Live keyboard, mouse-button, wheel and relative-delta diagnostics.
- Live Windows.Gaming.Input readings for controller buttons, sticks and triggers.
- Engine Test page that calls the native Rust bridge to create a DX12 SwapChainPanel surface and present a test frame (requires the bridge DLL).

## Input mapping reference
The default movement, inventory, gameplay and function-key assignments follow the open-source Horizon input mapping table:
https://github.com/horizon-rust/horizon/blob/main/crates/horizon-client-input/src/keybinds.rs

## Engine port status
See [ENGINE_PORT_STATUS.md](ENGINE_PORT_STATUS.md) for the port status. The Engine Test page validates a DX12 composition-surface path; it is not the Horizon world renderer.

## Not included
This is not the Horizon game engine or an Android-to-UWP conversion. The Horizon Rust engine is linked into a native bridge for diagnostics, but the Horizon game loop and world renderer are not yet connected to the UWP host. The controls page and diagnostics prove that the UWP shell can receive and configure input; they do not yet make Minecraft playable. A Windows/Xbox engine port still requires separate renderer, input, filesystem, networking and platform integration work.
