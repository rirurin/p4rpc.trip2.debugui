use std::collections::HashMap;
use std::mem::offset_of;
use std::ops::{Deref, DerefMut, Index};
use std::path::PathBuf;
use std::ptr::NonNull;
use crate::Result;
use std::sync::{Arc, Condvar, Mutex, OnceLock};
use std::thread::JoinHandle;
use std::time::{Duration, Instant};
use gilrs_imgui_support::state::{GamepadBuilder, GamepadState};
use glam::Vec2;
use imgui::{BackendFlags, ConfigFlags, Context as ImContext, FontGlyphRanges, FontId, ImColor32};
use imgui::internal::RawWrapper;
use imgui_winit_support::{HiDpiMode, WinitPlatform};
use riri_imgui_vulkano::context::RendererContext;
use riri_inspector_components::clipboard::ClipboardSupport;
use riri_mod_tools_rt::address::ProcessInfo;
use riri_mod_tools_rt::logln;
use riri_mod_tools_rt::mod_loader_data::{get_directory_for_mod, CSharpString};
use vulkano::format::ClearValue;
use windows::core::PCWSTR;
use windows::Win32::Foundation::{HINSTANCE, HWND, LPARAM, WPARAM};
use windows::Win32::UI::WindowsAndMessaging::{LoadIconW, SendMessageW, ICON_SMALL, MSG, WM_SETICON};
use winit::application::ApplicationHandler;
use winit::dpi::{PhysicalPosition, PhysicalSize, Position, Size};
use winit::event::WindowEvent;
use winit::event_loop::{ActiveEventLoop, ControlFlow, EventLoop, EventLoopBuilder};
use winit::event_loop::pump_events::{EventLoopExtPumpEvents, PumpStatus};
use winit::platform::windows::EventLoopBuilderExtWindows;
use winit::raw_window_handle::{HasWindowHandle, RawWindowHandle};
use winit::window::{Window, WindowAttributes, WindowId};
use crate::color::ColorConverter;
use crate::renderer::context::VulkanContext;
use crate::themes::ThemeRegistry;

pub struct Gui {
    window: Option<Arc<Box<dyn Window>>>,
    platform: Option<WinitPlatform>,
    imgui: Option<ImContext>,
    renderer: Option<VulkanContext>,
    fonts: HashMap<String, FontId>,
    themes: ThemeRegistry,

    gamepad: GamepadState,
    last_frame: Instant,
    time_elapsed: f32,
    count: usize,
}

pub type EventStateInner = Option<EventLoop>;

#[repr(transparent)]
pub struct EventState(EventStateInner);

impl Deref for EventState {
    type Target = EventStateInner;
    fn deref(&self) -> &Self::Target {
        &self.0
    }
}

impl DerefMut for EventState {
    fn deref_mut(&mut self) -> &mut Self::Target {
        &mut self.0
    }
}

impl Default for EventState {
    fn default() -> Self {
        Self::default_const()
    }
}

impl EventState {
    const fn default_const() -> Self {
        Self(None)
    }
}

unsafe impl Send for EventState {}
unsafe impl Sync for EventState {}

impl EventState {
    pub fn tick() {
        let mut events = EVENT_STATE.lock().unwrap();
        if events.is_none() {
            let mut builder = EventLoopBuilder::default();
            builder.with_any_thread(true);
            builder.with_msg_hook(|msg| {
                let _msg = msg as *const MSG;
                // let msg_id = unsafe { (*msg).message };
                // logln!(Debug, "Calling message ID {}", msg_id);
                false // true disables winit's message dispatcher
            });
            *events = EventState(Some(builder.build().unwrap()));
            events.as_ref().unwrap().set_control_flow(ControlFlow::Poll);
        } else {
            let mut gui = GUI_STATE.lock().unwrap();
            if let Some(window) = gui.as_ref().unwrap().window.as_ref() {
                window.request_redraw();
            }
            if let PumpStatus::Exit(code) = events.as_mut().unwrap().pump_app_events(
                Some(Duration::ZERO), gui.as_mut().unwrap()) {
                *gui = GuiState(None);
                *events = EventState(None);
            }
            drop(gui);
        }
        drop(events);
    }
}

pub type GuiStateInner = Option<Gui>;

#[repr(transparent)]
pub struct GuiState(GuiStateInner);

impl Deref for GuiState {
    type Target = GuiStateInner;
    fn deref(&self) -> &Self::Target {
        &self.0
    }
}

impl DerefMut for GuiState {
    fn deref_mut(&mut self) -> &mut Self::Target {
        &mut self.0
    }
}

impl Default for GuiState {
    fn default() -> Self {
        Self::default_const()
    }
}

impl GuiState {
    const fn default_const() -> Self {
        Self(None)
    }
}

unsafe impl Send for GuiState {}
unsafe impl Sync for GuiState {}

impl GuiState {
    pub fn ensure_running() {
        let mut gui = GUI_STATE.lock().unwrap();
        if gui.is_none() {
            *gui = GuiState(Some(Gui::new().unwrap()));
        }
        drop(gui);
    }

    pub fn check_imgui() -> bool {
        let gui = GUI_STATE.lock().unwrap();
        gui.as_ref().map_or(false, |g| g.imgui.is_some())
    }
}

pub struct GuiThread;
impl GuiThread {
    pub fn spawn() -> JoinHandle<()> {
        std::thread::spawn(move || {
            loop {
                GuiState::ensure_running();
                EventState::tick();
                *SYNC_VALUE.lock().unwrap() = false;
                SYNC_SIGNAL.notify_all();
                std::thread::park();
            }
        })
    }
}

pub(crate) static EVENT_STATE: Mutex<EventState> = Mutex::new(EventState::default_const());
pub(crate) static GUI_STATE: Mutex<GuiState> = Mutex::new(GuiState::default_const());
pub(crate) static GUI_THREAD: OnceLock<JoinHandle<()>> = OnceLock::new();
pub(crate) static SYNC_VALUE: Mutex<bool> = Mutex::new(false);
pub(crate) static SYNC_SIGNAL: Condvar = Condvar::new();

// Resource ID as defined in FWindowsPlatformApplicationMisc::CreateApplication (UE4/UE5)
const PROGRAM_ICON_UE5: usize = 0x7b;

impl Gui {
    pub fn get_name(&self) -> &str {
        "trip2 Debug GUI"
    }

    pub fn get_window(&self) -> Arc<Box<dyn Window>> {
        self.window.as_ref().unwrap().clone()
    }

    #[allow(dead_code)]
    pub fn get_platform(&self) -> &WinitPlatform {
        self.platform.as_ref().unwrap()
    }

    #[allow(dead_code)]
    pub fn get_platform_mut(&mut self) -> &mut WinitPlatform {
        self.platform.as_mut().unwrap()
    }

    #[allow(dead_code)]
    pub fn get_imgui(&self) -> &ImContext {
        self.imgui.as_ref().unwrap()
    }

    pub fn get_imgui_mut(&mut self) -> &mut ImContext {
        self.imgui.as_mut().unwrap()
    }

    fn new() -> Result<Self> {
        Ok(Self {
            window: None,
            platform: None,
            imgui: None,
            renderer: None,
            fonts: HashMap::new(),
            themes: ThemeRegistry::default(),
            gamepad: GamepadBuilder::new()
                .set_axis_to_btn(0.5, 0.4)
                // Invert the inverse_y setting to account for flipped y axis clip space.
                // This is not necessary with OpenGL or DirectX
                .invert_y(true)
                .build()?,
            last_frame: Instant::now(),
            time_elapsed: 0.,
            count: 0,
        })
    }

    pub fn add_font(&mut self, name: &str, range: FontGlyphRanges, size: f32) -> Result<()> {
        let font_path = PathBuf::from(
            Into::<String>::into(get_directory_for_mod())).join("data");
        let key =  name.rsplit_once(".")
            .map_or_else(|| name, |(name, _)| name).to_owned();
        self.fonts.insert(
            key,
            riri_inspector_components::font::load_font(
                self.imgui.as_mut().unwrap(),
                font_path.join(name),
                range,
                size
            )?
        );
        Ok(())
    }

    fn set_window_icon(&self) {
        let RawWindowHandle::Win32(handle) =
            self.get_window().window_handle().unwrap().as_raw() else { return; };
        // From Metaphor Multiplayer
        let proc = ProcessInfo::get_current_process().unwrap();
        let main_icon = unsafe { std::mem::transmute::<usize, PCWSTR>(PROGRAM_ICON_UE5) };
        unsafe {
            let Ok(main_icon) = LoadIconW(
                Some(HINSTANCE(proc.get_main_module().as_raw().0)), main_icon) else { return; };
            SendMessageW(
                HWND(handle.hwnd.get() as _),
                WM_SETICON,
                Some(WPARAM(ICON_SMALL as usize)),
                Some(LPARAM(main_icon.0 as isize)));
        }
    }
}

impl ApplicationHandler for Gui {
    fn can_create_surfaces(&mut self, event_loop: &dyn ActiveEventLoop) {
        let attr = WindowAttributes::default()
            .with_visible(false)
            .with_title(self.get_name())
            .with_surface_size(Size::Physical(PhysicalSize::new(1920, 1080)))
            .with_position(Position::Physical(PhysicalPosition::new(100, 100)));
        self.window = Some(Arc::new(event_loop.create_window(attr).unwrap()));
        self.set_window_icon();
        self.imgui = Some(ImContext::create());
        self.get_imgui_mut().io_mut().config_flags |= ConfigFlags::DOCKING_ENABLE;
        self.get_imgui_mut().set_ini_filename(None);
        // self.get_imgui_mut().set_log_filename(None);
        self.get_imgui_mut().set_clipboard_backend(ClipboardSupport::new().unwrap());
        self.platform = Some(WinitPlatform::new(self.get_imgui_mut()));
        self.platform.as_mut().unwrap().attach_window(
            self.imgui.as_mut().unwrap().io_mut(),
            self.window.as_ref().unwrap().as_ref().as_ref(),
            HiDpiMode::Rounded
        );
        self.get_imgui_mut().io_mut().mouse_pos = [0., 0.];
        let hidpi_factor = self.platform.as_ref().unwrap().hidpi_factor();
        self.add_font("NotoSansCJKjp-Medium.otf", FontGlyphRanges::japanese(), 15.).unwrap();
        self.add_font("QwitcherGrypen-Bold.ttf", FontGlyphRanges::default(), 90.).unwrap();
        self.get_imgui_mut().io_mut().font_global_scale = (1.0 / hidpi_factor) as f32;
        self.renderer = Some(VulkanContext::new(
            RendererContext::new(event_loop, self.get_window(), Some(self.get_name().to_string())).unwrap(),
            self.get_window(),
            self.get_imgui_mut()
        ).unwrap());
        // We can honor the ImDrawCmd::VtxOffset field, allowing for large meshes.
        self.get_imgui_mut().io_mut().backend_flags |= BackendFlags::RENDERER_HAS_VTX_OFFSET;
        self.window.as_ref().unwrap().set_visible(true);

        // Insert themes
        self.themes.extend_from_path(PathBuf::from(
            Into::<String>::into(get_directory_for_mod())).join("data/themes.toml")).unwrap();

        let (alloc_fn, free_fn, user_data)
            = ImContext::get_allocator_functions();

        unsafe { set_imgui_context(self.get_imgui().raw(), alloc_fn, free_fn, user_data) };
    }

    fn window_event(&mut self, event_loop: &dyn ActiveEventLoop, window_id: WindowId, event: WindowEvent) {
        let window = self.window.as_ref().unwrap();
        let renderer = self.renderer.as_mut().unwrap();
        let platform = self.platform.as_mut().unwrap();
        let imgui = self.imgui.as_mut().unwrap();
        let mut style_to_apply = None;
        match event {
            WindowEvent::CloseRequested => { event_loop.exit(); },
            WindowEvent::RedrawRequested => {
                let now = Instant::now();
                imgui.io_mut().update_delta_time(now - self.last_frame);
                let delta_time = imgui.io().delta_time;
                self.time_elapsed += delta_time;
                self.last_frame = now;
                self.count = self.count.overflowing_add(1).0;
                self.gamepad.update(imgui);
                // Start draw UI
                let imgui_context = unsafe { imgui.raw() as *const _ as usize };
                let ui = imgui.new_frame();
                if let Some(main) = ui.begin_main_menu_bar() {
                    if let Some(_menu) = ui.begin_menu_with_enabled("Menu", true) {
                        if let Some(_menu) = ui.begin_menu_with_enabled("Themes", true) {
                            for theme in self.themes.iter() {
                                if ui.menu_item(&theme.name) {
                                    style_to_apply = Some(theme.name.clone());
                                }
                            }
                        }
                    }
                    let _ = ui.begin_menu_with_enabled(format!("ImGui context: 0x{:x}", imgui_context), true);
                }

                let mut windows = WINDOW_STATES.lock().unwrap();
                /*
                if let Some(windows) = windows.0 {
                    let windows = unsafe { windows.as_ref() };
                    for i in 0..windows.length {
                        let window = &windows[i];
                        ui.window(format!("Window {:x}", window.hash))
                            .build(|| {
                                unsafe { draw_window(window.hash) };
                            });
                    }
                }
                */
                *windows = WindowStatePointer::null();
                AppDebugInfo::new(&self.fonts, ui, window.clone()).draw();
                ui.show_demo_window(&mut true);

                let draw_data = imgui.render();
                let clear_color = ColorConverter::hsv_to_rgb(
                    (self.count as f32 / 300.) % 1., 0.25, 0.3);
                if let ClearValue::Float(v) = &mut renderer.clear_color {
                    *v = [clear_color.x, clear_color.y, clear_color.z, 1.];
                }
                renderer.render(draw_data, self.time_elapsed).unwrap();
                renderer.refresh(window.clone()).unwrap();

                if let Some(style_to_apply) = style_to_apply &&
                    let Some(style) = self.themes.iter().find(|th| th.name == style_to_apply) {
                    style.apply(imgui.style_mut());
                }
            },
            WindowEvent::SurfaceResized(_) => {
                let io = imgui.io_mut();
                platform.handle_window_event(io, window.as_ref().as_ref(), &event);
                renderer.refresh(window.clone()).unwrap();
            },
            v => {
                // logln!(Debug, "Unhandled window event: {:?}", v);
                let io = imgui.io_mut();
                platform.handle_window_event(io, window.as_ref().as_ref(), &v)
            }
        }
    }
}

#[derive(Debug)]
pub struct AppDebugInfo<'a> {
    fonts: &'a HashMap<String, FontId>,
    ui: &'a mut imgui::Ui,
    window: Arc<Box<dyn Window>>
}

impl<'a> AppDebugInfo<'a> {
    pub(crate) fn new(
        fonts: &'a HashMap<String, FontId>,
        ui: &'a mut imgui::Ui,
        window: Arc<Box<dyn Window>>
    ) -> Self {
        Self { fonts, ui, window }
    }

    pub(crate) fn draw(&self) {
        let tf_id = *self.fonts.get("QwitcherGrypen-Bold").unwrap();
        let title_font = self.ui.fonts().get_font(tf_id).unwrap();
        let m_id = *self.fonts.get("NotoSansCJKjp-Medium").unwrap();
        let main_font = self.ui.fonts().get_font(m_id).unwrap();

        let debug_title = "The Rirurin Project 2";
        let debug_info = format!(
            "Reloaded Version {}, Git Commit {} (#{}), Build Date {}",
            crate::version::RELOADED_VERSION,
            crate::version::COMMIT_HASH,
            crate::version::COMMIT_COUNT,
            crate::version::COMPILE_DATE
        );

        let window_dims = Vec2::from_array(self.window.surface_size().into());
        let title_length = debug_title.chars().map(|c| title_font.get_glyph(c).advance_x).sum::<f32>();
        let title_pos = [window_dims.x - (title_length + 20.), window_dims.y - (title_font.font_size + main_font.font_size * 2.)];
        let info_length = debug_info.chars().map(|c| main_font.get_glyph(c).advance_x).sum::<f32>();
        let info_pos = [ window_dims.x - (info_length + 20.), window_dims.y - (main_font.font_size + main_font.font_size / 2.)];

        let debug_subtitle = ImColor32::from_rgba(255, 255, 255, 127);
        let title_token = self.ui.push_font(tf_id);
        self.ui.get_background_draw_list().add_text(title_pos, debug_subtitle, debug_title);
        title_token.pop();
        let body_token = self.ui.push_font(m_id);
        self.ui.get_background_draw_list().add_text(info_pos, debug_subtitle, debug_info);
        body_token.pop();
    }
}

type SetImguiContextFn = unsafe extern "C" fn(
    *const imgui::sys::ImGuiContext,
    imgui::sys::ImGuiMemAllocFunc,
    imgui::sys::ImGuiMemFreeFunc,
    *mut core::ffi::c_void
);

#[unsafe(no_mangle)]
pub static SET_IMGUI_CONTEXT: OnceLock<SetImguiContextFn> = OnceLock::new();

#[unsafe(no_mangle)]
pub unsafe extern "C" fn set_set_imgui_context(cb: SetImguiContextFn) {
    SET_IMGUI_CONTEXT.set(cb).unwrap();
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn set_imgui_context(
    raw: *const imgui::sys::ImGuiContext,
    alloc_fn: imgui::sys::ImGuiMemAllocFunc,
    free_fn: imgui::sys::ImGuiMemFreeFunc,
    user_data: *mut core::ffi::c_void) {
    unsafe { SET_IMGUI_CONTEXT.get().unwrap()(raw, alloc_fn, free_fn, user_data) };
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn get_deltatime() -> f32 {
    GUI_STATE.lock().unwrap()
        .as_ref().unwrap()
        .imgui.as_ref().unwrap()
        .io()
        .delta_time
}

type WindowStatePointerInner = Option<NonNull<Array<WindowState>>>;

#[repr(C)]
pub struct WindowStatePointer(WindowStatePointerInner);

impl Deref for WindowStatePointer {
    type Target = WindowStatePointerInner;

    fn deref(&self) -> &Self::Target {
        &self.0
    }
}

impl DerefMut for WindowStatePointer {
    fn deref_mut(&mut self) -> &mut Self::Target {
        &mut self.0
    }
}

unsafe impl Send for WindowStatePointer {}
unsafe impl Sync for WindowStatePointer {}

impl WindowStatePointer {
    pub const fn null() -> Self {
        Self(None)
    }
}

pub static WINDOW_STATES: Mutex<WindowStatePointer> = Mutex::new(WindowStatePointer::null());

#[unsafe(no_mangle)]
pub unsafe extern "C" fn set_window_states(entries: WindowStatePointer) {
    *WINDOW_STATES.lock().unwrap() = entries;
}

#[repr(C)]
pub struct Array<T> where T: Send + Sync {
    entries: *const T,
    length: usize
}

unsafe impl<T: Send + Sync> Send for Array<T> {}
unsafe impl<T: Send + Sync> Sync for Array<T> {}

impl<T: Send + Sync> Array<T> {
    pub const fn new() -> Self {
        Self {
            entries: std::ptr::null(),
            length: 0
        }
    }
}

impl<T: Send + Sync> Index<usize> for Array<T> {
    type Output = T;
    fn index(&self, index: usize) -> &Self::Output {
        unsafe { &*self.entries.add(index) }
    }
}

#[repr(C)]
pub struct WindowState {
    title: CSharpString,
    hash: u32,
}

unsafe impl Send for WindowState {}
unsafe impl Sync for WindowState {}

type DrawWindowFn = unsafe extern "C" fn(u32);

#[unsafe(no_mangle)]
pub static DRAW_WINDOW: OnceLock<DrawWindowFn> = OnceLock::new();

#[unsafe(no_mangle)]
pub unsafe extern "C" fn set_draw_window(cb: DrawWindowFn) {
    DRAW_WINDOW.set(cb).unwrap();
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn draw_window(id: u32) {
    unsafe { DRAW_WINDOW.get().unwrap()(id) };
}