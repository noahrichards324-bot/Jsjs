//! Minimal static-library smoke test for the x64 UWP Rust target.
//!
//! This does not contain the Horizon game engine. It verifies that CI can
//! produce a Rust static library for the target that the UWP host can later
//! link when the real platform bridge is ready.

/// ABI version for the future native host/engine bridge.
#[unsafe(no_mangle)]
pub extern "C" fn horizon_uwp_port_probe_abi_version() -> u32 {
    1
}

/// Small deterministic function so the static library contains an exported
/// C ABI symbol that can be inspected by the package/link step.
#[unsafe(no_mangle)]
pub extern "C" fn horizon_uwp_port_probe_add(left: u32, right: u32) -> u32 {
    left.saturating_add(right)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn exported_probe_functions_have_expected_values() {
        assert_eq!(horizon_uwp_port_probe_abi_version(), 1);
        assert_eq!(horizon_uwp_port_probe_add(20, 22), 42);
        assert_eq!(horizon_uwp_port_probe_add(u32::MAX, 1), u32::MAX);
    }
}
