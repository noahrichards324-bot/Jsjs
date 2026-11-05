$ErrorActionPreference = "Stop"

function Read-Utf8([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Missing source: $Path" }
    $value = [System.IO.File]::ReadAllText($Path)
    return $value.Replace(([string][char]13 + [string][char]10), [string][char]10)
}
function Write-Utf8([string]$Path, [string]$Value) {
    [System.IO.File]::WriteAllText($Path, $Value, [System.Text.UTF8Encoding]::new($false))
}
function Replace-Required([string]$Text, [string]$Old, [string]$New, [string]$Label) {
    if ($Text.Contains($New)) { return $Text }
    if (-not $Text.Contains($Old)) { throw "Could not patch: $Label" }
    return $Text.Replace($Old, $New)
}

$gamePath = "HorizonEngine/crates/horizon-client/src/game.rs"
$game = Read-Utf8 $gamePath

$game = Replace-Required $game 'mod event_loop;' 'mod event_loop;' + [string][char]10 + 'pub mod uwp_host;' "register UWP host module"

$oldNewRenderer = @'
    fn new_renderer(&self, window: Arc<Window>) -> Result<Renderer> {
        self.renderer_for(window, &self.pack, &self.gui)
    }
'@
$newRenderer = @'
    fn new_renderer(&self, window: Arc<Window>) -> Result<Renderer> {
        self.renderer_for(window, &self.pack, &self.gui)
    }
    fn new_uwp_renderer(&self, panel: usize, size: [u32; 2]) -> Result<Renderer> {
        build_renderer_uwp(
            panel,
            size,
            &self.pack,
            &self.gui,
            self.renderer.as_ref(),
            None,
            horizon_client::graphics::GraphicsApi::Dx12,
            self.settings.horizon.translucent_batching,
            false,
            shader_packs::picked_pack_draws_flat(&self.settings),
            (
                self.settings.file.int("textureFiltering"),
                self.settings.file.int("maxAnisotropyBit"),
            ),
        )
    }
'@
$game = Replace-Required $game $oldNewRenderer $newRenderer "Game UWP renderer factory"

$buildStart = $game.IndexOf("fn build_renderer(")
$buildEnd = $game.IndexOf("/// The loading screen's renderer", $buildStart)
if ($buildStart -lt 0 -or $buildEnd -lt 0) { throw "Could not locate build_renderer helper for UWP clone." }
$build = $game.Substring($buildStart, $buildEnd - $buildStart)
$uwpBuild = $build.Replace("fn build_renderer(", "fn build_renderer_uwp(")
$uwpBuild = $uwpBuild.Replace("    window: Arc<Window>,", "    panel: usize," + [string][char]10 + "    size: [u32; 2],")
$uwpBuild = $uwpBuild.Replace("pollster::block_on(Renderer::new(" + [string][char]10 + "        window," + [string][char]10 + "        offscreen,", "pollster::block_on(Renderer::new_uwp(" + [string][char]10 + "        panel," + [string][char]10 + "        size,")
if ($uwpBuild -notlike '*Renderer::new_uwp(*') { throw "UWP renderer helper clone did not switch to Renderer::new_uwp." }
if (-not $game.Contains("fn build_renderer_uwp(")) {
    $game = $game.Insert($buildEnd, $uwpBuild)
}

$oldRunLoop = @'
fn run_event_loop(mut game: Game<'_>) -> Result<()> {
    if game.options.headless {
'@
$newRunLoop = @'
fn run_event_loop(mut game: Game<'_>) -> Result<()> {
    if let Some(host) = uwp_host::take_config() {
        return uwp_host::run(game, host);
    }
    if game.options.headless {
'@
$game = Replace-Required $game $oldRunLoop $newRunLoop "route configured UWP game to host loop"

Write-Utf8 $gamePath $game

$hostPath = "HorizonEngine/crates/horizon-client/src/game/uwp_host.rs"
$hostCode = @'
 //! UWP host loop: Horizon's own game state and renderer driven by a XAML SwapChainPanel.
 //!
 //! The launcher-facing native bridge configures the panel and pushes input events here.
 //! This module runs the normal Game::idle / RedrawRequested path without creating a winit
 //! desktop window or starting winit's desktop event loop.

use super::{event_loop::EventLoopCtl, Game};
use std::{
    cell::Cell,
    collections::{HashSet, VecDeque},
    sync::{
        atomic::{AtomicBool, Ordering},
        Mutex, OnceLock,
    },
    time::{Duration, Instant},
};
use winit::event_loop::ControlFlow;

const EVENT_CAPACITY: usize = 4096;
const FRAME: Duration = Duration::from_millis(16);

#[derive(Clone, Copy, Debug)]
pub struct HostConfig {
    pub panel: usize,
    pub size: [u32; 2],
}

#[derive(Clone, Copy, Debug, Default)]
struct HostInputEvent {
    kind: u32,
    code: u32,
    state: u32,
    x: f32,
    y: f32,
}

static CONFIG: OnceLock<Mutex<Option<HostConfig>>> = OnceLock::new();
static EVENTS: OnceLock<Mutex<VecDeque<HostInputEvent>>> = OnceLock::new();
static STOP: AtomicBool = AtomicBool::new(false);

fn config_slot() -> &'static Mutex<Option<HostConfig>> {
    CONFIG.get_or_init(|| Mutex::new(None))
}
fn events() -> &'static Mutex<VecDeque<HostInputEvent>> {
    EVENTS.get_or_init(|| Mutex::new(VecDeque::with_capacity(EVENT_CAPACITY)))
}

/// Called by the native bridge before Horizon's entrypoint starts.
pub fn configure(panel: usize, width: u32, height: u32) -> bool {
    if panel == 0 || width == 0 || height == 0 {
        return false;
    }
    let mut slot = config_slot().lock().unwrap_or_else(std::sync::PoisonError::into_inner);
    if slot.is_some() {
        return false;
    }
    *slot = Some(HostConfig { panel, size: [width, height] });
    STOP.store(false, Ordering::Release);
    events().lock().unwrap_or_else(std::sync::PoisonError::into_inner).clear();
    true
}

/// Push one keyboard/mouse/controller/resize event from the UWP host.
pub fn push_input(kind: u32, code: u32, state: u32, x: f32, y: f32) -> i32 {
    if !(1..=9).contains(&kind) {
        return -2;
    }
    let mut queue = events().lock().unwrap_or_else(std::sync::PoisonError::into_inner);
    if queue.len() >= EVENT_CAPACITY {
        return -1;
    }
    queue.push_back(HostInputEvent {
        kind,
        code,
        state,
        x: if x.is_finite() { x } else { 0.0 },
        y: if y.is_finite() { y } else { 0.0 },
    });
    0
}

/// Request a normal game shutdown. The host loop saves the world before returning.
pub fn request_stop() {
    STOP.store(true, Ordering::Release);
}

pub(super) fn take_config() -> Option<HostConfig> {
    config_slot().lock().unwrap_or_else(std::sync::PoisonError::into_inner).take()
}

struct UwpLoop {
    exit: Cell<bool>,
    flow: Cell<ControlFlow>,
}
impl EventLoopCtl for UwpLoop {
    fn exit(&self) {
        self.exit.set(true);
    }
    fn set_control_flow(&self, flow: ControlFlow) {
        self.flow.set(flow);
    }
}
impl UwpLoop {
    fn next_step(&self, started: Instant) -> Instant {
        match self.flow.get() {
            ControlFlow::WaitUntil(at) => at,
            ControlFlow::Poll | ControlFlow::Wait => started + FRAME,
        }
    }
}

#[derive(Default)]
struct InputState {
    stick_keys: HashSet<&'static str>,
    left_trigger: bool,
    right_trigger: bool,
}

fn key_name(vk: u32) -> Option<String> {
    if (0x41..=0x5a).contains(&vk) {
        return Some(format!("key.keyboard.{}", char::from_u32(vk).unwrap_or(' ' ).to_ascii_lowercase()));
    }
    if (0x30..=0x39).contains(&vk) {
        return Some(format!("key.keyboard.{}", char::from_u32(vk).unwrap_or(' ')));
    }
    if (0x70..=0x7b).contains(&vk) {
        return Some(format!("key.keyboard.f{}", vk - 0x70 + 1));
    }
    Some(match vk {
        0x08 => "key.keyboard.backspace",
        0x09 => "key.keyboard.tab",
        0x0d => "key.keyboard.enter",
        0x10 | 0xa0 => "key.keyboard.left.shift",
        0xa1 => "key.keyboard.right.shift",
        0x11 | 0xa2 => "key.keyboard.left.control",
        0xa3 => "key.keyboard.right.control",
        0x12 => "key.keyboard.left.alt",
        0x1b => "key.keyboard.escape",
        0x20 => "key.keyboard.space",
        0x21 => "key.keyboard.page.up",
        0x22 => "key.keyboard.page.down",
        0x23 => "key.keyboard.end",
        0x24 => "key.keyboard.home",
        0x25 => "key.keyboard.left",
        0x26 => "key.keyboard.up",
        0x27 => "key.keyboard.right",
        0x28 => "key.keyboard.down",
        0x2d => "key.keyboard.insert",
        0x2e => "key.keyboard.delete",
        0xba => "key.keyboard.semicolon",
        0xbb => "key.keyboard.equal",
        0xbc => "key.keyboard.comma",
        0xbd => "key.keyboard.minus",
        0xbe => "key.keyboard.period",
        0xbf => "key.keyboard.slash",
        0xc0 => "key.keyboard.grave.accent",
        0xdb => "key.keyboard.left.bracket",
        0xdc => "key.keyboard.backslash",
        0xdd => "key.keyboard.right.bracket",
        0xde => "key.keyboard.apostrophe",
        _ => return None,
    }.to_owned())
}

fn send_key(game: &mut Game<'_>, loop_ctl: &UwpLoop, name: &str, action: &str) {
    game.replay_line(&format!("key {name} {action}"), loop_ctl);
}

fn update_axis_key(
    game: &mut Game<'_>,
    loop_ctl: &UwpLoop,
    held: &mut HashSet<&'static str>,
    name: &'static str,
    down: bool,
) {
    let was_down = held.contains(name);
    if down == was_down {
        return;
    }
    if down {
        held.insert(name);
        send_key(game, loop_ctl, name, "down");
    } else {
        held.remove(name);
        send_key(game, loop_ctl, name, "up");
    }
}

fn drain_input(game: &mut Game<'_>, loop_ctl: &UwpLoop, state: &mut InputState, host: &mut HostConfig) {
    loop {
        let event = events().lock().unwrap_or_else(std::sync::PoisonError::into_inner).pop_front();
        let Some(event) = event else { break };
        match event.kind {
            1 => {
                if let Some(name) = key_name(event.code) {
                    send_key(game, loop_ctl, &name, match event.state { 0 => "up", 2 => "repeat", _ => "down" });
                }
            }
            2 => {
                let name = match event.code {
                    1 => "left",
                    2 => "right",
                    3 => "middle",
                    4 => "back",
                    5 => "forward",
                    _ => continue,
                };
                game.replay_line(&format!("mouse {name} {}", if event.state == 0 { "up" } else { "down" }), loop_ctl);
            }
            3 => {
                if event.x != 0.0 || event.y != 0.0 {
                    let yaw = -event.x * 0.12;
                    let pitch = event.y * 0.12;
                    game.replay_line(&format!("turn {yaw:.4} {pitch:.4}"), loop_ctl);
                }
            }
            4 => {
                if event.y != 0.0 {
                    let step = if event.y > 0.0 { 1isize } else { -1isize };
                    game.selected = (game.selected as isize + step).rem_euclid(9) as usize;
                }
            }
            5 => {
                let down = event.state != 0;
                let key = match event.code {
                    0x1000 => Some("key.keyboard.space"),       // A: jump/confirm
                    0x2000 => Some("key.keyboard.escape"),      // B: back
                    0x4000 => Some("key.keyboard.e"),           // X: inventory
                    0x8000 => Some("key.keyboard.q"),           // Y: drop
                    0x0100 => Some("key.keyboard.left.shift"),  // LB: sneak
                    0x0200 => Some("key.keyboard.left.control"),// RB: sprint
                    0x0010 => Some("key.keyboard.escape"),      // Menu
                    0x0020 => Some("key.keyboard.tab"),         // View
                    _ => None,
                };
                if let Some(name) = key {
                    send_key(game, loop_ctl, name, if down { "down" } else { "up" });
                }
            }
            6 => match event.code {
                0 => {
                    update_axis_key(game, loop_ctl, &mut state.stick_keys, "key.keyboard.a", event.x < -0.30);
                    update_axis_key(game, loop_ctl, &mut state.stick_keys, "key.keyboard.d", event.x > 0.30);
                    update_axis_key(game, loop_ctl, &mut state.stick_keys, "key.keyboard.w", event.y > 0.30);
                    update_axis_key(game, loop_ctl, &mut state.stick_keys, "key.keyboard.s", event.y < -0.30);
                }
                1 => {
                    let yaw = -event.x * 4.0;
                    let pitch = -event.y * 4.0;
                    if yaw.abs() > 0.01 || pitch.abs() > 0.01 {
                        game.replay_line(&format!("turn {yaw:.4} {pitch:.4}"), loop_ctl);
                    }
                }
                2 => {
                    let left = event.x > 0.20;
                    let right = event.y > 0.20;
                    if left != state.left_trigger {
                        game.replay_line(&format!("mouse left {}", if left { "down" } else { "up" }), loop_ctl);
                        state.left_trigger = left;
                    }
                    if right != state.right_trigger {
                        game.replay_line(&format!("mouse right {}", if right { "down" } else { "up" }), loop_ctl);
                        state.right_trigger = right;
                    }
                }
                _ => {}
            },
            7 if event.state == 0 => {
                game.replay_line("release", loop_ctl);
                state.stick_keys.clear();
                state.left_trigger = false;
                state.right_trigger = false;
            }
            8 => {
                host.size = [event.x.max(1.0) as u32, event.y.max(1.0) as u32];
                if let Some(renderer) = &mut game.renderer {
                    renderer.resize(host.size[0], host.size[1]);
                }
            }
            9 => {
                let scalar = char::from_u32(event.code).unwrap_or('\0');
                if !scalar.is_control() && scalar != '\0' {
                    game.replay_line(&format!("type {scalar}"), loop_ctl);
                }
            }
            _ => {}
        }
    }
}

pub(super) fn run(mut game: Game<'_>, mut host: HostConfig) -> super::Result<()> {
    game.options.headless = true;
    game.options.offscreen = false;
    let renderer = game.new_uwp_renderer(host.panel, host.size)?;
    game.renderer = Some(renderer);
    game.world_renderer_ready = true;
    game.previous = Instant::now();
    game.apply_settings();

    let loop_ctl = UwpLoop {
        exit: Cell::new(false),
        flow: Cell::new(ControlFlow::Poll),
    };
    let mut input_state = InputState::default();
    while !loop_ctl.exit.get() && !STOP.load(Ordering::Acquire) {
        let started = Instant::now();
        loop_ctl.flow.set(ControlFlow::Poll);
        drain_input(&mut game, &loop_ctl, &mut input_state, &mut host);
        game.idle(&loop_ctl);
        if let Some(wait) = loop_ctl.next_step(started).checked_duration_since(Instant::now()) {
            std::thread::sleep(wait);
        }
    }

    game.replay_line("release", &loop_ctl);
    game.shut_down();
    super::narrator::stop();
    super::horizon_client::discord_presence::stop();
    super::write_report(&game)?;
    game.error.take().map_or(Ok(()), Err)
}
'@
$hostCode = $hostCode.TrimStart()
Write-Utf8 $hostPath $hostCode

Write-Host "Applied Horizon UWP game host and input loop patch."
