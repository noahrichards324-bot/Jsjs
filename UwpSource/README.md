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

## Input mapping reference
The default movement, inventory, gameplay and function-key assignments follow the open-source Horizon input mapping table:
https://github.com/horizon-rust/horizon/blob/main/crates/horizon-client-input/src/keybinds.rs

## Engine port status
See [ENGINE_PORT_STATUS.md](ENGINE_PORT_STATUS.md) for the verified technical blockers and work required to port Horizon's Rust game engine to Xbox UWP.

## Not included
This is not the Horizon game engine or an Android-to-UWP conversion. The Horizon Rust engine has not been ported or linked to this UWP project. The controls page and diagnostics prove that the UWP shell can receive and configure input; they do not yet make Minecraft playable. A Windows/Xbox engine port still requires separate renderer, input, filesystem, networking and platform integration work.
