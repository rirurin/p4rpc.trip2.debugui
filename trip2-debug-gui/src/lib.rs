pub(crate) mod color;
pub(crate) mod gui;
pub(crate) mod renderer;
pub(crate) mod version;

use std::error::Error;
use crate::gui::{EventState, GuiState};

#[unsafe(no_mangle)]
unsafe extern "C" fn fengineloop_tick() -> bool {
    // logln!(Debug, "TODO: FEngineLoop::Tick");
    GuiState::check_availability();
    EventState::tick();
    true
}

pub(crate) type Result<T> = std::result::Result<T, Box<dyn Error>>;