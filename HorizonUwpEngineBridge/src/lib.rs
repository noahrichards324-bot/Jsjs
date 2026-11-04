#![forbid(unsafe_code)]

//! UWP-facing native library shell around the upstream Horizon client.
//!
//! The entrypoint address is exported only to force the client and its transitive
//! gameplay/render dependencies into the DLL for the link test. It must not be
//! called by the current UWP shell: it starts Horizon's desktop winit event loop,
//! which has not yet been replaced with a UWP host.

/// Native bridge ABI version. Increment when the C ABI changes.
#[unsafe(no_mangle)]
pub extern "C" fn horizon_uwp_engine_bridge_abi_version() -> u32 {
    1
}

/// Link anchor for the real Horizon client entrypoint.
///
/// This is an address, not a callable UWP entrypoint. The current function
/// behind it starts the desktop window/event loop and is not Xbox-ready.
#[unsafe(no_mangle)]
pub extern "C" fn horizon_uwp_engine_entrypoint_address() -> usize {
    horizon_client::entry::main as usize
}
