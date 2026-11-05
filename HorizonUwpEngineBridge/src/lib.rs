#![deny(unsafe_op_in_unsafe_fn)]

//! UWP-facing native bridge around the upstream Horizon client.
//!
//! This crate currently provides a C ABI and an input-event queue. The queue
//! is the transport from the UWP host to Rust; Horizon's desktop event loop
//! has not yet been refactored to consume it. The entrypoint address is a link
//! anchor only and must not be called by the current UWP shell.

use std::collections::VecDeque;
use std::ffi::c_void;
use std::sync::{Mutex, OnceLock};

const INPUT_QUEUE_CAPACITY: usize = 4096;

/// ABI version. Increment when the public C layout or function contract changes.
#[unsafe(no_mangle)]
pub extern "C" fn horizon_uwp_engine_bridge_abi_version() -> u32 {
    2
}

/// Stable event kinds used by the UWP host.
///
/// 1 = key, 2 = mouse button, 3 = mouse movement, 4 = mouse wheel,
/// 5 = gamepad button, 6 = gamepad axis, 7 = focus, 8 = resize,
/// 9 = text input. Kind zero is reserved for an empty result.
pub mod event_kind {
    pub const NONE: u32 = 0;
    pub const KEY: u32 = 1;
    pub const MOUSE_BUTTON: u32 = 2;
    pub const MOUSE_MOVE: u32 = 3;
    pub const MOUSE_WHEEL: u32 = 4;
    pub const GAMEPAD_BUTTON: u32 = 5;
    pub const GAMEPAD_AXIS: u32 = 6;
    pub const FOCUS: u32 = 7;
    pub const RESIZE: u32 = 8;
    pub const TEXT: u32 = 9;

    pub const MAX: u32 = TEXT;
}

/// One event crossing the UWP-to-Rust C ABI.
///
/// For keys, `code` is the Windows Virtual-Key code. For mouse buttons,
/// use 1=left, 2=right, 3=middle, 4/5=auxiliary. For gamepad buttons and
/// axes, `code` is the bridge's documented pad control ID. The meaning
/// of `state`, `x` and `y` depends on `kind`.
#[repr(C)]
#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct UwpInputEvent {
    pub kind: u32,
    pub code: u32,
    pub state: u32,
    pub x: f32,
    pub y: f32,
}

static INPUT_QUEUE: OnceLock<Mutex<VecDeque<UwpInputEvent>>> = OnceLock::new();

fn queue() -> &'static Mutex<VecDeque<UwpInputEvent>> {
    INPUT_QUEUE.get_or_init(|| Mutex::new(VecDeque::with_capacity(INPUT_QUEUE_CAPACITY)))
}

fn with_queue<T>(f: impl FnOnce(&mut VecDeque<UwpInputEvent>) -> T) -> T {
    let mut guard = queue()
        .lock()
        .unwrap_or_else(std::sync::PoisonError::into_inner);
    f(&mut guard)
}

/// Add one event from the UWP host.
///
/// Returns 0 on success, -1 if the queue is full, and -2 for an invalid event
/// kind. The queue is bounded so a stalled game loop cannot grow memory without
/// limit. On overflow, the caller can retry after the next drain.
#[unsafe(no_mangle)]
pub extern "C" fn horizon_uwp_input_push(
    kind: u32,
    code: u32,
    state: u32,
    x: f32,
    y: f32,
) -> i32 {
    if !(event_kind::KEY..=event_kind::MAX).contains(&kind) {
        return -2;
    }

    with_queue(|events| {
        if events.len() >= INPUT_QUEUE_CAPACITY {
            return -1;
        }

        events.push_back(UwpInputEvent {
            kind,
            code,
            state,
            x: if x.is_finite() { x } else { 0.0 },
            y: if y.is_finite() { y } else { 0.0 },
        });
        0
    })
}

/// Remove the next event into the caller-provided structure.
///
/// Returns 1 when an event was written, 0 when the queue is empty, and -1
/// when the output pointer is null.
///
/// # Safety
/// When non-null, `out_event` must point to writable, properly aligned
/// storage for one `UwpInputEvent` for the duration of this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn horizon_uwp_input_pop(out_event: *mut UwpInputEvent) -> i32 {
    if out_event.is_null() {
        return -1;
    }

    let event = with_queue(|events| events.pop_front());
    match event {
        Some(event) => {
            // The C# P/Invoke caller supplies a writable out structure.
            unsafe { out_event.write(event) };
            1
        }
        None => {
            unsafe { out_event.write(UwpInputEvent::default()) };
            0
        }
    }
}

/// Clear queued events, for focus loss or host shutdown.
#[unsafe(no_mangle)]
pub extern "C" fn horizon_uwp_input_clear() {
    with_queue(|events| events.clear());
}

/// Size of the C ABI input structure, used to verify the host's P/Invoke layout.
#[unsafe(no_mangle)]
pub extern "C" fn horizon_uwp_input_event_size() -> u32 {
    std::mem::size_of::<UwpInputEvent>() as u32
}

/// Number of queued input events (diagnostics only).
#[unsafe(no_mangle)]
pub extern "C" fn horizon_uwp_input_pending() -> u32 {
    with_queue(|events| events.len().min(u32::MAX as usize) as u32)
}

/// Link anchor for Horizon's existing entrypoint.
///
/// This is an address, not a callable UWP entrypoint. The current function
/// starts Horizon's desktop winit event loop and is not Xbox-ready.
#[unsafe(no_mangle)]
pub extern "C" fn horizon_uwp_engine_entrypoint_address() -> usize {
    horizon_client::entry::main as usize
}


/// A native DX12 surface attached to a XAML SwapChainPanel.
///
/// This is a renderer-host smoke test. It does not yet execute Horizon's
/// world renderer; it proves that the UWP panel can own a wgpu swap chain
/// and receive presented frames.
struct UwpSurfaceHost {
    _instance: wgpu::Instance,
    surface: wgpu::Surface<'static>,
    adapter: wgpu::Adapter,
    device: wgpu::Device,
    queue: wgpu::Queue,
    config: wgpu::SurfaceConfiguration,
}

impl UwpSurfaceHost {
    fn create(panel: *mut c_void, width: u32, height: u32) -> Result<Self, i32> {
        if panel.is_null() || width == 0 || height == 0 {
            return Err(-1);
        }

        let instance = wgpu::Instance::new(&wgpu::InstanceDescriptor {
            backends: wgpu::Backends::DX12,
            ..Default::default()
        });

        // The XAML host passes an ISwapChainPanelNative interface pointer.
        // wgpu's DX12 HAL AddRefs and retains that COM interface for the
        // lifetime of the created surface.
        let surface = unsafe {
            instance.create_surface_unsafe(wgpu::SurfaceTargetUnsafe::SwapChainPanel(panel))
        }
        .map_err(|_| -2)?;

        let adapter = pollster::block_on(instance.request_adapter(
            &wgpu::RequestAdapterOptions {
                power_preference: wgpu::PowerPreference::HighPerformance,
                compatible_surface: Some(&surface),
                force_fallback_adapter: false,
            },
        ))
        .map_err(|_| -3)?;

        let descriptor = wgpu::DeviceDescriptor {
            label: Some("Horizon Xbox UWP surface probe"),
            required_features: wgpu::Features::empty(),
            required_limits: wgpu::Limits::default(),
            memory_hints: wgpu::MemoryHints::MemoryUsage,
            ..Default::default()
        };

        let (device, queue) = pollster::block_on(adapter.request_device(&descriptor))
            .map_err(|_| -4)?;

        let mut config = surface
            .get_default_config(&adapter, width.max(1), height.max(1))
            .ok_or(-5)?;
        config.present_mode = wgpu::PresentMode::Fifo;
        config.desired_maximum_frame_latency = 2;
        surface.configure(&device, &config);

        let host = Self {
            _instance: instance,
            surface,
            adapter,
            device,
            queue,
            config,
        };

        host.present_test_frame()?;
        Ok(host)
    }

    fn present_test_frame(&self) -> Result<(), i32> {
        let frame = self.surface.get_current_texture().map_err(|_| -6)?;
        let view = frame.texture.create_view(&wgpu::TextureViewDescriptor::default());
        let mut encoder = self.device.create_command_encoder(&wgpu::CommandEncoderDescriptor {
            label: Some("Horizon UWP surface probe"),
        });

        {
            let _pass = encoder.begin_render_pass(&wgpu::RenderPassDescriptor {
                label: Some("Horizon UWP surface probe clear"),
                color_attachments: &[Some(wgpu::RenderPassColorAttachment {
                    view: &view,
                    resolve_target: None,
                    depth_slice: None,
                    ops: wgpu::Operations {
                        load: wgpu::LoadOp::Clear(wgpu::Color {
                            r: 0.035,
                            g: 0.09,
                            b: 0.07,
                            a: 1.0,
                        }),
                        store: wgpu::StoreOp::Store,
                    },
                })],
                ..Default::default()
            });
        }

        self.queue.submit(Some(encoder.finish()));
        frame.present();
        Ok(())
    }

    fn resize(&mut self, width: u32, height: u32) -> Result<(), i32> {
        if width == 0 || height == 0 {
            return Ok(());
        }
        self.config.width = width;
        self.config.height = height;
        self.surface.configure(&self.device, &self.config);
        self.present_test_frame()
    }
}

/// Create and present a DX12 composition swap chain on a UWP SwapChainPanel.
///
/// Returns an opaque host handle, or null on failure. The host owns the wgpu
/// instance, surface, adapter, device and queue. The input pointer must be an
/// ISwapChainPanelNative COM interface pointer obtained from the XAML panel.
///
/// # Safety
/// `panel` must be a live ISwapChainPanelNative interface pointer for the
/// duration of the call. wgpu retains its own COM reference for the surface.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn horizon_uwp_surface_create(
    panel: *mut c_void,
    width: u32,
    height: u32,
) -> *mut c_void {
    match UwpSurfaceHost::create(panel, width, height) {
        Ok(host) => Box::into_raw(Box::new(host)).cast(),
        Err(code) => {
            LAST_SURFACE_ERROR.store(code, std::sync::atomic::Ordering::Relaxed);
            std::ptr::null_mut()
        }
    }
}

/// Last negative surface creation/error code for diagnostics.
static LAST_SURFACE_ERROR: std::sync::atomic::AtomicI32 =
    std::sync::atomic::AtomicI32::new(0);

#[unsafe(no_mangle)]
pub extern "C" fn horizon_uwp_surface_last_error() -> i32 {
    LAST_SURFACE_ERROR.load(std::sync::atomic::Ordering::Relaxed)
}

/// Resize the UWP composition swap chain and present a new diagnostic frame.
///
/// # Safety
/// `host` must be a live handle returned by `horizon_uwp_surface_create`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn horizon_uwp_surface_resize(
    host: *mut c_void,
    width: u32,
    height: u32,
) -> i32 {
    if host.is_null() {
        return -1;
    }
    let host = unsafe { &mut *host.cast::<UwpSurfaceHost>() };
    host.resize(width, height).map_or_else(
        |code| {
            LAST_SURFACE_ERROR.store(code, std::sync::atomic::Ordering::Relaxed);
            code
        },
        |_| 0,
    )
}

/// Present another diagnostic frame on the UWP composition swap chain.
///
/// # Safety
/// `host` must be a live handle returned by `horizon_uwp_surface_create`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn horizon_uwp_surface_present(host: *mut c_void) -> i32 {
    if host.is_null() {
        return -1;
    }
    let host = unsafe { &mut *host.cast::<UwpSurfaceHost>() };
    host.present_test_frame().map_or_else(
        |code| {
            LAST_SURFACE_ERROR.store(code, std::sync::atomic::Ordering::Relaxed);
            code
        },
        |_| 0,
    )
}

/// Destroy a UWP swap-chain host created by `horizon_uwp_surface_create`.
///
/// # Safety
/// `host` must be null or a live handle returned by the create function.
/// A non-null handle must be destroyed exactly once.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn horizon_uwp_surface_destroy(host: *mut c_void) {
    if !host.is_null() {
        drop(unsafe { Box::from_raw(host.cast::<UwpSurfaceHost>()) });
    }
}


#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn abi_event_round_trips_through_queue() {
        horizon_uwp_input_clear();
        let pushed = horizon_uwp_input_push(event_kind::KEY, 0x57, 1, 0.0, 0.0);
        assert_eq!(pushed, 0);
        assert_eq!(horizon_uwp_input_pending(), 1);

        let received = with_queue(|events| events.pop_front().unwrap());
        assert_eq!(
            received,
            UwpInputEvent {
                kind: event_kind::KEY,
                code: 0x57,
                state: 1,
                x: 0.0,
                y: 0.0,
            }
        );
    }

    #[test]
    fn invalid_event_kind_is_rejected() {
        assert_eq!(horizon_uwp_input_push(event_kind::NONE, 0, 0, 0.0, 0.0), -2);
        assert_eq!(horizon_uwp_input_push(event_kind::MAX + 1, 0, 0, 0.0, 0.0), -2);
    }

    #[test]
    fn non_finite_motion_is_sanitized() {
        horizon_uwp_input_clear();
        assert_eq!(
            horizon_uwp_input_push(event_kind::MOUSE_MOVE, 0, 0, f32::NAN, f32::INFINITY),
            0
        );
        let received = with_queue(|events| events.pop_front().unwrap());
        assert_eq!(received.x, 0.0);
        assert_eq!(received.y, 0.0);
    }

    #[test]
    fn clear_discards_all_pending_input() {
        horizon_uwp_input_clear();
        assert_eq!(horizon_uwp_input_push(event_kind::KEY, 0x57, 1, 0.0, 0.0), 0);
        horizon_uwp_input_clear();
        assert_eq!(horizon_uwp_input_pending(), 0);
    }

    #[test]
    fn abi_version_is_current() {
        assert_eq!(horizon_uwp_engine_bridge_abi_version(), 2);
    }
}
