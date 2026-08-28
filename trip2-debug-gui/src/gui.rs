use std::collections::HashMap;
use std::ops::{Deref, DerefMut, Index};
use std::path::{Path, PathBuf};
use std::ptr::NonNull;
use crate::Result;
use std::sync::{Arc, Condvar, Mutex, OnceLock};
use std::thread::JoinHandle;
use std::time::{Duration, Instant};
use gilrs_imgui_support::state::{GamepadBuilder, GamepadState};
use glam::{UVec2, Vec2};
use imgui::{BackendFlags, Condition, ConfigFlags, Context as ImContext, FontGlyphRanges, FontId, ImColor32, Ui};
use imgui::internal::{RawCast, RawWrapper};
use imgui_sys::ImWchar;
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

    window_name: String,
    gamepad: GamepadState,
    last_frame: Instant,
    time_elapsed: f32,
    count: usize,
    show_metrics: bool,
    noto_sans_glyph_ranges: Vec<ImWchar>,

    update_size_for_config: bool,
    last_size_update: f32,
    update_pos_for_config: bool,
    last_pos_update: f32,
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

                let mut interop = INTEROP_STATE.lock().unwrap();
                *interop = InteropStatePointer::null();
                drop(interop);

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
pub(crate) static SURFACE_SIZE: Mutex<Vec2> = Mutex::new(Vec2::ZERO);

// Resource ID as defined in FWindowsPlatformApplicationMisc::CreateApplication (UE4/UE5)
const PROGRAM_ICON_UE5: usize = 0x7b;
const POS_SIZE_UPDATE_DELAY_TIME: f32 = 0.5;

impl Gui {
    pub fn get_name(&self) -> &str {
        &self.window_name
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
            window_name: String::new(),
            gamepad: GamepadBuilder::new()
                .set_axis_to_btn(0.5, 0.4)
                // Invert the inverse_y setting to account for flipped y axis clip space.
                // This is not necessary with OpenGL or DirectX
                .invert_y(true)
                .build()?,
            last_frame: Instant::now(),
            time_elapsed: 0.,
            count: 0,
            show_metrics: false,
            noto_sans_glyph_ranges: vec![],
            update_size_for_config: false,
            last_size_update: 0.,
            update_pos_for_config: false,
            last_pos_update: 0.,
        })
    }

    pub fn add_font(&mut self, name: &str, range: FontGlyphRanges, size: f32) -> Result<()> {
        let font_path = PathBuf::from(
            Into::<String>::into(get_directory_for_mod())).join("data");
        let key =  name.rsplit_once(".")
            .map_or_else(|| name, |(name, _)| name).to_owned();
        self.add_font_inner(key, font_path.join(name), range, size)
    }

    pub fn add_font_from_path<P: AsRef<Path>>(&mut self, path: P, range: FontGlyphRanges, size: f32) -> Result<()> {
        let key = path.as_ref().file_stem().map_or(
            "Unnamed".to_string(), |v| v.to_str().unwrap().to_string());
        self.add_font_inner(key, path, range, size)
    }

    fn add_font_inner<P: AsRef<Path>>(&mut self, key: String, path: P, range: FontGlyphRanges, size: f32) -> Result<()> {
        self.fonts.insert(
            key,
            riri_inspector_components::font::load_font(
                self.imgui.as_mut().unwrap(),
                path.as_ref(),
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

    fn draw_ui(
        ui: &mut Ui,
        fonts: &HashMap<String, FontId>,
        themes: &ThemeRegistry,
        window: Arc<Box<dyn Window>>,
        show_metrics: &mut bool
    ) -> Option<String> {
        let config_style: String = unsafe { get_theme_name().into() };
        let mut theme_update_lock = THEME_UPDATED_EXTERNALLY.lock().unwrap();
        let mut style_to_apply = match *theme_update_lock {
            true => {
                *theme_update_lock = false;
                match themes.contains(&config_style) {
                    true => Some(config_style.clone()),
                    false => {
                        if config_style != "Default" {
                            logln!(Warning, "Could not find the style named \"{}\"", config_style);
                        }
                        None
                    }
                }
            },
            false => None
        };
        drop(theme_update_lock);
        // let mut style_to_apply = None;
        let external_lock = INTEROP_STATE.lock().unwrap();
        let external = external_lock.0
            .map(|v| unsafe { v.as_ref() });
        if let Some(main) = ui.begin_main_menu_bar() {
            if let Some(_menu) = ui.begin_menu_with_enabled("Apps", true) {
                if let Some(external) = external {
                    Self::main_menu_draw_apps(ui, external);
                }
                if ui.menu_item("Metrics") {
                    *show_metrics = true;
                }
            }
            if let Some(_menu) = ui.begin_menu_with_enabled("Themes", true) {
                for theme in themes.iter() {
                    if ui.menu_item_config(&theme.name)
                        .selected(&theme.name == &config_style)
                        .build() {
                        style_to_apply = Some(theme.name.clone());
                        let theme_name_ffi = format!("{}\0", theme.name);
                        unsafe { set_theme_name(theme_name_ffi.as_ptr()) };
                    }
                }
            }
        }
        if let Some(external) = external {
            for win_state in &external.windows {
                let name: String = (&win_state.title).into();
                let window = ui.window(name);
                let window = match unsafe { get_window_initial_size(win_state.hash) } {
                    Vec2::ZERO => window, v => window.size([v.x, v.y], Condition::FirstUseEver)
                };
                let window = match unsafe { get_window_initial_pos(win_state.hash) } {
                    Vec2::ZERO => window, v => window.position([v.x, v.y], Condition::FirstUseEver)
                };
                let mut opened = true;
                let window = match win_state.can_close {
                    false => window, true => window.opened(&mut opened)
                };
                window.build(|| unsafe { draw_window(win_state.hash) });
                if !opened {
                    unsafe { remove_window(win_state.hash) };
                }
            }
        }
        drop(external_lock);
        AppDebugInfo::new(&fonts, ui, window.clone()).draw();
        if *show_metrics {
            ui.show_metrics_window(show_metrics);
        }
        style_to_apply
    }

    fn main_menu_draw_apps(ui: &Ui, external: &InteropState) {
        for app in &external.apps {
            let app_name: String = (&app.title).into();
            let buttons: Vec<_> = (&external.buttons).into_iter()
                .filter(|btn| (btn.hash >> 0x20) as u32 == app.hash).collect();
            if buttons.len() > 0 {
                if let Some(app_menu) = ui.begin_menu_with_enabled(app_name, true) {
                    for button in buttons {
                        let btn_name: String = (&button.name).into();
                        if ui.menu_item(btn_name) {
                            unsafe { button_action(button.hash) };
                        }
                    }
                }
            } else {
                ui.menu_item(app_name);
            }
        }
    }

    fn get_noto_sans_glyph_range(&mut self) -> FontGlyphRanges {
        if self.noto_sans_glyph_ranges.len() == 0 {
            let fonts = unsafe {&raw const *self.get_imgui_mut().fonts().raw() as *mut _ };
            // imgui 1.91.3:  full_ranges for ImFontAtlas::GetGlyphRangesJapanese is 6009 * sizeof(ImWchar) (incl null terminator)
            let glyph_ranges_jp = unsafe { imgui_sys::ImFontAtlas_GetGlyphRangesJapanese(fonts) };
            self.noto_sans_glyph_ranges.reserve(6011);
            unsafe { std::ptr::copy_nonoverlapping(
                glyph_ranges_jp,
                self.noto_sans_glyph_ranges.as_mut_ptr(),
                6008
            ) };
            unsafe { self.noto_sans_glyph_ranges.set_len(6008) };
            // Box-drawing characters: https://unicode.org/charts/PDF/U2500.pdf
            // U+2500 - U+257F
            // Used in Persona 3 Reload to draw Tartarus floor layout in Debug
            self.noto_sans_glyph_ranges.push(0x2500);
            self.noto_sans_glyph_ranges.push(0x257F);
            self.noto_sans_glyph_ranges.push(0);
        }
        unsafe { FontGlyphRanges::from_ptr(self.noto_sans_glyph_ranges.as_ptr()) }
    }
}

impl ApplicationHandler for Gui {
    fn can_create_surfaces(&mut self, event_loop: &dyn ActiveEventLoop) {
        let branch_version: String = unsafe { get_branch_version().into() };
        self.window_name = format!("trip2 Debug GUI ({})", branch_version);
        let attr = WindowAttributes::default()
            .with_visible(false)
            .with_title(self.get_name())
            .with_surface_size(Size::Physical(get_window_size()))
            .with_position(Position::Physical(get_window_pos()));
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
        let noto_sans_glyph_range = self.get_noto_sans_glyph_range();
        self.add_font("NotoSansCJKjp-Medium.otf", noto_sans_glyph_range, 15.).unwrap();
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
                *SURFACE_SIZE.lock().unwrap() = Vec2::from_array(window.as_ref().surface_size().into());
                // Start draw UI
                let ui = imgui.new_frame();
                let style_to_apply = Self::draw_ui(
                    ui, &self.fonts, &self.themes, window.clone(), &mut self.show_metrics);
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
                if *WINDOW_SIZE_POS_UPDATED_EXTERNALLY.lock().unwrap() {
                    let _ = window.request_surface_size(Size::Physical(get_window_size()));
                    window.set_outer_position(Position::Physical(get_window_pos()));
                    *WINDOW_SIZE_POS_UPDATED_EXTERNALLY.lock().unwrap() = false;
                    self.update_size_for_config = false;
                    self.update_pos_for_config = false;
                }
                let mut save_to_config = false;
                if self.update_size_for_config && self.time_elapsed - self.last_size_update > POS_SIZE_UPDATE_DELAY_TIME {
                    set_window_size(window.surface_size());
                    self.update_size_for_config = false;
                    save_to_config = true;
                }
                if self.update_pos_for_config && self.time_elapsed - self.last_pos_update > POS_SIZE_UPDATE_DELAY_TIME {
                    if let Ok(pos) = window.outer_position() {
                        set_window_pos(pos);
                        self.update_pos_for_config = false;
                        save_to_config = true;
                    }
                }
                if save_to_config {
                    save_config();
                }
            },
            WindowEvent::SurfaceResized(_) => {
                let io = imgui.io_mut();
                platform.handle_window_event(io, window.as_ref().as_ref(), &event);
                renderer.refresh(window.clone()).unwrap();
                if self.time_elapsed > POS_SIZE_UPDATE_DELAY_TIME {
                    self.update_size_for_config = true;
                }
                self.last_size_update = self.time_elapsed;
            },
            WindowEvent::Moved(_) => {
                let io = imgui.io_mut();
                platform.handle_window_event(io, window.as_ref().as_ref(), &event);
                if self.time_elapsed > POS_SIZE_UPDATE_DELAY_TIME {
                    self.update_pos_for_config = true;
                }
                self.last_pos_update = self.time_elapsed;
            },
            v => {
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
        let title_pos = [window_dims.x - (title_length + 20.), window_dims.y - (title_font.font_size * 1.1)];
        let info_length = debug_info.chars().map(|c| main_font.get_glyph(c).advance_x).sum::<f32>();
        let info_pos = [window_dims.x - (info_length + 20.), 0.];

        let title_color = ImColor32::from_rgba(255, 255, 255, 127);
        let title_token = self.ui.push_font(tf_id);
        self.ui.get_background_draw_list().add_text(title_pos, title_color, debug_title);
        title_token.pop();
        let debug_color = ImColor32::from_rgba(255, 255, 255, 255);
        let body_token = self.ui.push_font(m_id);
        self.ui.get_foreground_draw_list().add_text(info_pos, debug_color, debug_info);
        body_token.pop();
    }
}

type SetImguiContextFn = unsafe extern "C" fn(
    *const imgui::sys::ImGuiContext,
    imgui::sys::ImGuiMemAllocFunc,
    imgui::sys::ImGuiMemFreeFunc,
    *mut core::ffi::c_void
);

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

#[repr(C)]
pub struct InteropState {
    windows: Array<WindowState>,
    apps: Array<AppState>,
    buttons: Array<ButtonState>,
}

unsafe impl Send for InteropState {}
unsafe impl Sync for InteropState {}

type InteropStatePointerInner = Option<NonNull<InteropState>>;

impl Deref for InteropStatePointer {
    type Target = InteropStatePointerInner;

    fn deref(&self) -> &Self::Target {
        &self.0
    }
}

impl DerefMut for InteropStatePointer {
    fn deref_mut(&mut self) -> &mut Self::Target {
        &mut self.0
    }
}

unsafe impl Send for InteropStatePointer {}
unsafe impl Sync for InteropStatePointer {}

impl InteropStatePointer {
    pub const fn null() -> Self {
        Self(None)
    }
}

pub static INTEROP_STATE: Mutex<InteropStatePointer> = Mutex::new(InteropStatePointer::null());

#[unsafe(no_mangle)]
pub unsafe extern "C" fn set_interop_state(entries: InteropStatePointer) {
    *INTEROP_STATE.lock().unwrap() = entries;
}

#[repr(C)]
pub struct InteropStatePointer(InteropStatePointerInner);

#[repr(C)]
pub struct WindowState {
    title: CSharpString,
    hash: u64,
    size: Vec2,
    position: Vec2,
    can_close: bool,
}

unsafe impl Send for WindowState {}
unsafe impl Sync for WindowState {}

#[repr(C)]
pub struct AppState {
    title: CSharpString,
    hash: u32,
}

unsafe impl Send for AppState {}
unsafe impl Sync for AppState {}

#[repr(C)]
pub struct ButtonState {
    name: CSharpString,
    hash: u64,
}

unsafe impl Send for ButtonState {}
unsafe impl Sync for ButtonState {}

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

impl<T: Send + Sync> Drop for Array<T> {
    fn drop(&mut self) {
        for i in 0..self.length {
            unsafe { std::ptr::drop_in_place(&self[i] as *const T as *mut T) };
        }
    }
}

impl<'a, T: Send + Sync> IntoIterator for &'a Array<T> {
    type Item = &'a T;
    type IntoIter = ArrayIterator<'a, T>;
    fn into_iter(self) -> Self::IntoIter {
        Self::IntoIter {
            inner: self,
            current: 0
        }
    }
}

pub struct ArrayIterator<'a, T: Send + Sync> {
    inner: &'a Array<T>,
    current: usize
}

impl<'a, T: Send + Sync + 'a> Iterator for ArrayIterator<'a, T> {
    type Item = &'a T;

    fn next(&mut self) -> Option<Self::Item> {
        let out = if self.current < self.inner.length {
            Some(unsafe { &*self.inner.entries.add(self.current) })
        } else {
            None
        };
        self.current += 1;
        out
    }
}

type DrawWindowFn = unsafe extern "C" fn(u64);

pub static DRAW_WINDOW: OnceLock<DrawWindowFn> = OnceLock::new();

#[unsafe(no_mangle)]
pub unsafe extern "C" fn set_draw_window(cb: DrawWindowFn) {
    DRAW_WINDOW.set(cb).unwrap();
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn draw_window(id: u64) {
    unsafe { DRAW_WINDOW.get().unwrap()(id) };
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn get_surface_size() -> Vec2 {
    *SURFACE_SIZE.lock().unwrap()
}

type GetWindowInitialSizeFn = unsafe extern "C" fn(u64) -> Vec2;

pub static GET_WINDOW_INITIAL_SIZE: OnceLock<GetWindowInitialSizeFn> = OnceLock::new();

#[unsafe(no_mangle)]
pub unsafe extern "C" fn set_get_window_initial_size(cb: GetWindowInitialSizeFn) {
    GET_WINDOW_INITIAL_SIZE.set(cb).unwrap();
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn get_window_initial_size(id: u64) -> Vec2 {
    unsafe { GET_WINDOW_INITIAL_SIZE.get().unwrap()(id) }
}

type GetWindowInitialPosFn = unsafe extern "C" fn(u64) -> Vec2;

pub static GET_WINDOW_INITIAL_POS: OnceLock<GetWindowInitialPosFn> = OnceLock::new();

#[unsafe(no_mangle)]
pub unsafe extern "C" fn set_get_window_initial_pos(cb: GetWindowInitialPosFn) {
    GET_WINDOW_INITIAL_POS.set(cb).unwrap();
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn get_window_initial_pos(id: u64) -> Vec2 {
    unsafe { GET_WINDOW_INITIAL_POS.get().unwrap()(id) }
}

type RemoveWindowFn = unsafe extern "C" fn(u64);

pub static REMOVE_WINDOW: OnceLock<RemoveWindowFn> = OnceLock::new();

#[unsafe(no_mangle)]
pub unsafe extern "C" fn set_remove_window(cb: RemoveWindowFn) {
    REMOVE_WINDOW.set(cb).unwrap();
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn remove_window(id: u64) {
    unsafe { REMOVE_WINDOW.get().unwrap()(id) };
}

type GetBranchVersionFn = unsafe extern "C" fn() -> CSharpString;

pub static GET_BRANCH_VERSION: OnceLock<GetBranchVersionFn> = OnceLock::new();

#[unsafe(no_mangle)]
pub unsafe extern "C" fn set_get_branch_version(cb: GetBranchVersionFn) {
    GET_BRANCH_VERSION.set(cb).unwrap();
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn get_branch_version() -> CSharpString {
    unsafe { GET_BRANCH_VERSION.get().unwrap()() }
}

type ButtonActionFn = unsafe extern "C" fn(u64);

pub static BUTTON_ACTION: OnceLock<ButtonActionFn> = OnceLock::new();

#[unsafe(no_mangle)]
pub unsafe extern "C" fn set_button_action(cb: ButtonActionFn) {
    BUTTON_ACTION.set(cb).unwrap();
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn button_action(hash: u64) {
    unsafe { BUTTON_ACTION.get().unwrap()(hash) };
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn add_font_from_path(path: CSharpString, /*glyph_range: *const u32,*/ font_size: f32) -> FontId {
    let mut gui_state = GUI_STATE.lock().unwrap();
    let null_font = unsafe { std::mem::transmute::<*const imgui::Font, FontId>(std::ptr::null())};
    let Some(gui) = gui_state.as_mut() else { return null_font; };
    let path = PathBuf::from(Into::<String>::into(path));
    let font_name = path.file_stem().map_or("Unknown".to_string(), |v| v.to_str().unwrap().to_string());
    let Ok(()) = gui.add_font_from_path(
        path, FontGlyphRanges::default(), // unsafe { FontGlyphRanges::from_ptr(glyph_range) },
        font_size) else { return null_font; };
    riri_mod_tools_rt::logln!(Debug, "Added font");
    gui.fonts.get(&font_name).map_or(null_font, |v| *v)
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn get_font(name: CSharpString) -> FontId {
    let mut gui_state = GUI_STATE.lock().unwrap();
    let null_font = unsafe { std::mem::transmute::<*const imgui::Font, FontId>(std::ptr::null())};
    let Some(gui) = gui_state.as_mut() else { return null_font; };
    gui.fonts.get(&Into::<String>::into(name)).map_or(null_font, |v| *v)
}

// Configuration: Theme Name

type SetGetThemeNameFn = unsafe extern "C" fn() -> CSharpString;

pub static GET_THEME_NAME: OnceLock<SetGetThemeNameFn> = OnceLock::new();
#[unsafe(no_mangle)]
pub unsafe extern "C" fn set_get_theme_name(cb: SetGetThemeNameFn) {
    GET_THEME_NAME.set(cb).unwrap();
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn get_theme_name() -> CSharpString {
    unsafe { GET_THEME_NAME.get().unwrap()() }
}

type SetSetThemeNameFn = unsafe extern "C" fn(*const u8);

pub static SET_THEME_NAME: OnceLock<SetSetThemeNameFn> = OnceLock::new();
#[unsafe(no_mangle)]
pub unsafe extern "C" fn set_set_theme_name(cb: SetSetThemeNameFn) {
    SET_THEME_NAME.set(cb).unwrap();
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn set_theme_name(name: *const u8) {
    unsafe { SET_THEME_NAME.get().unwrap()(name) }
}

static THEME_UPDATED_EXTERNALLY: Mutex<bool> = Mutex::new(true);

#[unsafe(no_mangle)]
pub unsafe extern "C" fn theme_updated_externally() {
    *THEME_UPDATED_EXTERNALLY.lock().unwrap() = true;
}

// Configuration: Window Size

type SetGetWindowSizeFn = unsafe extern "C" fn() -> u32;

pub static GET_WINDOW_SIZE: OnceLock<SetGetWindowSizeFn> = OnceLock::new();
#[unsafe(no_mangle)]
pub unsafe extern "C" fn set_get_window_size(cb: SetGetWindowSizeFn) {
    GET_WINDOW_SIZE.set(cb).unwrap();
}

pub fn get_window_size() -> PhysicalSize<u32> {
    let raw = unsafe { GET_WINDOW_SIZE.get().unwrap()() };
    PhysicalSize::new(raw & 0xffff, raw >> 0x10)
}

type SetSetWindowSizeFn = unsafe extern "C" fn(u32);

pub static SET_WINDOW_SIZE: OnceLock<SetSetWindowSizeFn> = OnceLock::new();
#[unsafe(no_mangle)]
pub unsafe extern "C" fn set_set_window_size(cb: SetSetWindowSizeFn) {
    SET_WINDOW_SIZE.set(cb).unwrap();
}

pub fn set_window_size(value: PhysicalSize<u32>) {
    let raw = value.width | (value.height << 0x10);
    unsafe { SET_WINDOW_SIZE.get().unwrap()(raw) }
}

// Configuration: Window Pos

type SetGetWindowPosFn = unsafe extern "C" fn() -> u32;

pub static GET_WINDOW_POS: OnceLock<SetGetWindowPosFn> = OnceLock::new();
#[unsafe(no_mangle)]
pub unsafe extern "C" fn set_get_window_pos(cb: SetGetWindowPosFn) {
    GET_WINDOW_POS.set(cb).unwrap();
}

pub fn get_window_pos() -> PhysicalPosition<i32> {
    let raw = unsafe { GET_WINDOW_POS.get().unwrap()() };
    PhysicalPosition::new((raw & 0xffff) as i32, (raw >> 0x10) as i32)
}

type SetSetWindowPosFn = unsafe extern "C" fn(u32);

pub static SET_WINDOW_POS: OnceLock<SetSetWindowPosFn> = OnceLock::new();
#[unsafe(no_mangle)]
pub unsafe extern "C" fn set_set_window_pos(cb: SetSetWindowPosFn) {
    SET_WINDOW_POS.set(cb).unwrap();
}

pub fn set_window_pos(value: PhysicalPosition<i32>) {
    let raw = (value.x as u32) | ((value.y as u32) << 0x10);
    unsafe { SET_WINDOW_POS.get().unwrap()(raw) }
}

type SetSaveConfigFn = unsafe extern "C" fn() -> u32;

pub static SAVE_CONFIG: OnceLock<SetSaveConfigFn> = OnceLock::new();
#[unsafe(no_mangle)]
pub unsafe extern "C" fn set_save_config(cb: SetSaveConfigFn) {
    SAVE_CONFIG.set(cb).unwrap();
}

static WINDOW_SIZE_POS_UPDATED_EXTERNALLY: Mutex<bool> = Mutex::new(false);

#[unsafe(no_mangle)]
pub unsafe extern "C" fn window_size_position_updated_externally() {
    *WINDOW_SIZE_POS_UPDATED_EXTERNALLY.lock().unwrap() = true;
}

pub fn save_config() {
    unsafe { SAVE_CONFIG.get().unwrap()() };
}