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

$libPath = "HorizonEngine/crates/horizon-client-render/src/lib.rs"
$gfxPath = "HorizonEngine/crates/horizon-client-render/src/gfx.rs"
$lib = Read-Utf8 $libPath
$lib = Replace-Required $lib '#![forbid(unsafe_code)]' '#![deny(unsafe_code)]' "renderer isolated unsafe DX12 surface helper"
Write-Utf8 $libPath $lib

$gfx = Read-Utf8 $gfxPath
$gfx = Replace-Required $gfx 'use std::{collections::HashMap, path::PathBuf, sync::Arc, time::Instant};' 'use std::{collections::HashMap, ffi::c_void, path::PathBuf, sync::Arc, time::Instant};' "C pointer import"

$targetDef = @'
#[derive(Clone)]
enum RenderTarget {
    Window(Arc<Window>),
    SwapChainPanel { panel: usize, size: [u32; 2] },
}

impl RenderTarget {
    fn window(&self) -> Option<&Arc<Window>> {
        match self {
            Self::Window(window) => Some(window),
            Self::SwapChainPanel { .. } => None,
        }
    }

    fn size(&self) -> [u32; 2] {
        match self {
            Self::Window(window) => {
                let size = window.inner_size();
                [size.width.max(1), size.height.max(1)]
            }
            Self::SwapChainPanel { size, .. } => [size[0].max(1), size[1].max(1)],
        }
    }

    fn set_size(&mut self, width: u32, height: u32) {
        if let Self::SwapChainPanel { size, .. } = self {
            *size = [width.max(1), height.max(1)];
        }
    }

    #[allow(unsafe_code)]
    fn create_surface(&self, instance: &wgpu::Instance) -> Result<wgpu::Surface<'static>> {
        match self {
            Self::Window(window) => Ok(instance.create_surface(window.clone())?),
            Self::SwapChainPanel { panel, .. } => {
                #[cfg(target_os = "windows")]
                {
                    let pointer = *panel as *mut c_void;
                    Ok(unsafe {
                        instance.create_surface_unsafe(
                            wgpu::SurfaceTargetUnsafe::SwapChainPanel(pointer),
                        )?
                    })
                }
                #[cfg(not(target_os = "windows"))]
                {
                    let _ = panel;
                    Err("SwapChainPanel rendering is only available on Windows DX12".into())
                }
            }
        }
    }
}

'@
if (-not $gfx.Contains('enum RenderTarget')) {
    $gfx = Replace-Required $gfx 'pub struct Renderer {' ($targetDef + 'pub struct Renderer {') "renderer target type"
}
$gfx = Replace-Required $gfx ('    display_fresh: bool,' + [string][char]10 + '    window: Arc<Window>,') ('    display_fresh: bool,' + [string][char]10 + '    target: RenderTarget,') "renderer target field"

$gfx = Replace-Required $gfx ('async fn open_device(' + [string][char]10 + '    window: &Arc<Window>,') ('async fn open_device(' + [string][char]10 + '    target: &RenderTarget,') "open_device target argument"
$gfx = Replace-Required $gfx 'open_device_with(window, offscreen, &order, layers, allow_cpu)' 'open_device_with(target, offscreen, &order, layers, allow_cpu)' "open_device delegation"
$gfx = Replace-Required $gfx ('async fn open_device_with(' + [string][char]10 + '    window: &Arc<Window>,') ('async fn open_device_with(' + [string][char]10 + '    target: &RenderTarget,') "open_device_with target argument"
$gfx = Replace-Required $gfx 'open_with_retry(backends, window, offscreen, layers, allow_cpu)' 'open_with_retry(backends, target, offscreen, layers, allow_cpu)' "open_with_retry delegation"
$gfx = Replace-Required $gfx ('async fn open_with_retry(' + [string][char]10 + '    backends: wgpu::Backends,' + [string][char]10 + '    window: &Arc<Window>,') ('async fn open_with_retry(' + [string][char]10 + '    backends: wgpu::Backends,' + [string][char]10 + '    target: &RenderTarget,') "open_with_retry target argument"
$gfx = Replace-Required $gfx 'open_backend(backends, window, offscreen, false, layers, allow_cpu)' 'open_backend(backends, target, offscreen, false, layers, allow_cpu)' "open_backend first attempt"
$gfx = Replace-Required $gfx 'open_backend(backends, window, offscreen, true, layers, allow_cpu)' 'open_backend(backends, target, offscreen, true, layers, allow_cpu)' "open_backend retry"
$gfx = Replace-Required $gfx ('async fn open_backend(' + [string][char]10 + '    backends: wgpu::Backends,' + [string][char]10 + '    window: &Arc<Window>,') ('async fn open_backend(' + [string][char]10 + '    backends: wgpu::Backends,' + [string][char]10 + '    target: &RenderTarget,') "open_backend target argument"
$gfx = Replace-Required $gfx '        Some(window),' '        target.window(),' "instance descriptor display"
$gfx = Replace-Required $gfx 'match instance.create_surface(window.clone()) {' 'match target.create_surface(&instance) {' "surface creation"

$methodStart = $gfx.IndexOf('    pub async fn new(')
if ($methodStart -lt 0) { throw "Could not find Renderer::new." }
if (-not $gfx.Contains('async fn new_for_target(')) {
    $bodyStart = $gfx.IndexOf('        let Opened {', $methodStart)
    if ($bodyStart -lt 0) { throw "Could not locate the Renderer::new method body." }
    $head = $gfx.Substring($methodStart, $bodyStart - $methodStart)
    $signatureClose = $head.LastIndexOf('    ) -> Result<Self> {')
    if ($signatureClose -lt 0) { throw "Could not locate the end of Renderer::new signature." }
    $signatureLength = $signatureClose + '    ) -> Result<Self> {'.Length
    $originalSignature = $head.Substring(0, $signatureLength)

    $wrapper = $originalSignature + @'
        Self::new_for_target(
            RenderTarget::Window(window), offscreen, sprite_pixels, sprite_size,
            sprite_minimum_size, layers, sprite_mips, animations, hud_atlas,
            glint, glint_armor, forcefield, panorama_faces, prepared, previous,
            api, translucent_batching, wide_frame, shader_pack,
        ).await
    }

    pub async fn new_uwp(
        panel: usize,
        size: [u32; 2],
        sprite_pixels: &[u8],
        sprite_size: u32,
        sprite_minimum_size: u32,
        layers: u32,
        sprite_mips: &[crate::mipmap::Settings],
        animations: &[SpriteAnimation],
        hud_atlas: (&[u8], u32, u32),
        glint: (&[u8], u32, u32),
        glint_armor: (&[u8], u32, u32),
        forcefield: (&[u8], u32),
        panorama_faces: Arc<panorama::Faces>,
        prepared: Option<PreparedTextures>,
        previous: Option<&Self>,
        api: GraphicsApi,
        translucent_batching: bool,
        wide_frame: bool,
        shader_pack: bool,
    ) -> Result<Self> {
        Self::new_for_target(
            RenderTarget::SwapChainPanel { panel, size }, false, sprite_pixels,
            sprite_size, sprite_minimum_size, layers, sprite_mips, animations,
            hud_atlas, glint, glint_armor, forcefield, panorama_faces, prepared,
            previous, api, translucent_batching, wide_frame, shader_pack,
        ).await
    }

    async fn new_for_target(
        target: RenderTarget,
        offscreen: bool,
        sprite_pixels: &[u8],
        sprite_size: u32,
        sprite_minimum_size: u32,
        layers: u32,
        sprite_mips: &[crate::mipmap::Settings],
        animations: &[SpriteAnimation],
        hud_atlas: (&[u8], u32, u32),
        glint: (&[u8], u32, u32),
        glint_armor: (&[u8], u32, u32),
        forcefield: (&[u8], u32),
        panorama_faces: Arc<panorama::Faces>,
        prepared: Option<PreparedTextures>,
        previous: Option<&Self>,
        api: GraphicsApi,
        translucent_batching: bool,
        wide_frame: bool,
        shader_pack: bool,
    ) -> Result<Self> {
'@
    $gfx = $gfx.Substring(0, $methodStart) + $wrapper + $gfx.Substring($bodyStart)
}
$gfx = Replace-Required $gfx 'None => renderer.instance.create_surface(window.clone())?,' 'None => target.create_surface(&renderer.instance)?,' "renderer reuse surface"
$gfx = Replace-Required $gfx 'open_device(&window, offscreen, api, layers).await?' 'open_device(&target, offscreen, api, layers).await?' "open device in renderer new"
$gfx = Replace-Required $gfx '        let size = window.inner_size();' '        let size = target.size();' "target initial size"
$gfx = Replace-Required $gfx 'size.width.clamp(1, max_side)' 'size[0].clamp(1, max_side)' "target width"
$gfx = Replace-Required $gfx 'size.height.clamp(1, max_side)' 'size[1].clamp(1, max_side)' "target height"

$displayInit = $gfx.IndexOf('            display: DisplayWatch::default(),')
if ($displayInit -lt 0) { throw "Could not locate Renderer struct initializer." }
$windowField = $gfx.IndexOf('            window,', $displayInit)
if ($windowField -lt 0) { throw "Could not locate Renderer window field initializer." }
$gfx = $gfx.Remove($windowField, '            window,'.Length).Insert($windowField, '            target,')

$gfx = Replace-Required $gfx '            let hdr_output = hdr::output(&caps);' '            let hdr_output = if target.window().is_some() { hdr::output(&caps) } else { None };' "disable HDR for UWP panel"
$gfx = Replace-Required $gfx '                    DisplayWatch::new(&self.window, self.surface.as_deref(), &self.adapter)' '                    if let Some(window) = self.target.window() { DisplayWatch::new(window, self.surface.as_deref(), &self.adapter) } else { (DisplayWatch::default(), None) }' "display watch for UWP"
$gfx = Replace-Required $gfx '            && !self.frame_gen.paused' ('            && !self.frame_gen.paused' + [string][char]10 + '            && self.target.window().is_some()') "disable frame generation for UWP"
$gfx = Replace-Required $gfx '                    &self.window,' '                    self.target.window().expect("frame generation is disabled for UWP panels"),' "frame generation window"
$gfx = Replace-Required $gfx '            None => self.instance.create_surface(self.window.clone())?,' '            None => self.target.create_surface(&self.instance)?,' "recreate UWP surface"
$gfx = Replace-Required $gfx '                self.window.pre_present_notify();' '                if let Some(window) = self.target.window() { window.pre_present_notify(); }' "pre-present notify"

# Ensure UWP resize remains current if the swap chain has to be recreated later.
$resizeNeedle = '    pub fn resize(&mut self, width: u32, height: u32) {'
if ($gfx.Contains($resizeNeedle)) {
    $gfx = Replace-Required $gfx $resizeNeedle ($resizeNeedle + [string][char]10 + '        self.target.set_size(width, height);') "resize target dimensions"
}
if ($gfx.Contains('self.window')) { throw "Renderer still contains direct self.window references." }
Write-Utf8 $gfxPath $gfx
Write-Host "Applied Horizon UWP SwapChainPanel renderer host patch."
