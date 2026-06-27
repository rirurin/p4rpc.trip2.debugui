pub(crate) mod color;
pub(crate) mod gui;
pub(crate) mod renderer;
pub(crate) mod version;
pub(crate) mod themes;

use std::error::Error;
use crate::gui::{EventState, GuiState, GuiThread, GUI_THREAD, SYNC_SIGNAL, SYNC_VALUE};

#[unsafe(no_mangle)]
unsafe extern "C" fn new_frame_ui() -> bool {
    let thread = GUI_THREAD.get_or_init(GuiThread::spawn);
    let mut lock_thread = SYNC_VALUE.lock().unwrap();
    *lock_thread = true;
    thread.thread().unpark();
    while *lock_thread {
        lock_thread = SYNC_SIGNAL.wait(lock_thread).unwrap();
    }
    true
}

#[unsafe(no_mangle)]
unsafe extern "C" fn check_imgui_running() -> bool {
    GuiState::check_imgui()
}

pub(crate) type Result<T> = std::result::Result<T, Box<dyn Error>>;