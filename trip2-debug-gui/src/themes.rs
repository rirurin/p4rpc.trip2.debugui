use std::collections::HashMap;
use std::fmt::Formatter;
use std::ops::{Deref, DerefMut};
use std::path::Path;
use std::str::FromStr;
use imgui::{Direction, Style, StyleColor};
use riri_mod_tools_rt::logln;
use serde::{Deserialize, Deserializer, Serialize};
use serde::de::{Error, Visitor};
use toml::Table;

struct DirectionVisitor;

impl<'de> Visitor<'de> for DirectionVisitor {
    type Value = Direction;

    fn expecting(&self, formatter: &mut Formatter) -> std::fmt::Result {
        formatter.write_str("A cardinal direction (Left, Right, Up, Down) or None.")
    }

    fn visit_str<E>(self, v: &str) -> Result<Self::Value, E>
    where
        E: Error,
    {
        match v {
            "Left" => Ok(Direction::Left),
            "Right" => Ok(Direction::Right),
            "Up" => Ok(Direction::Up),
            "Down" => Ok(Direction::Down),
            "None" => Ok(Direction::None),
            _ => Err(E::custom("Unknown direction"))
        }
    }

    fn visit_string<E>(self, v: String) -> Result<Self::Value, E>
    where
        E: Error,
    {
        self.visit_str(v.as_str())
    }
}


#[derive(Copy, Clone, Debug, Eq, PartialEq)]
#[repr(transparent)]
pub struct DirectionSerializable(Direction);

impl<'de> Deserialize<'de> for DirectionSerializable {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        deserializer.deserialize_string(DirectionVisitor)
            .map(|t| DirectionSerializable(t))
    }
}

// #[derive(Debug, Serialize, Deserialize)]
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ThemeStyle {
    alpha: f32,
    disabled_alpha: f32,
    window_padding: [f32; 2],
    window_rounding: f32,
    window_border_size: f32,
    window_min_size: [f32; 2],
    window_title_align: [f32; 2],
    window_menu_button_position: DirectionSerializable,
    child_rounding: f32,
    child_border_size: f32,
    popup_rounding: f32,
    popup_border_size: f32,
    frame_padding: [f32; 2],
    frame_rounding: f32,
    frame_border_size: f32,
    item_spacing: [f32; 2],
    item_inner_spacing: [f32; 2],
    cell_padding: [f32; 2],
    indent_spacing: f32,
    columns_min_spacing: f32,
    scrollbar_size: f32,
    scrollbar_rounding: f32,
    grab_min_size: f32,
    grab_rounding: f32,
    tab_rounding: f32,
    tab_border_size: f32,
    tab_min_width_for_close_button: f32,
    color_button_position: DirectionSerializable,
    button_text_align: [f32; 2],
    selectable_text_align: [f32; 2],
    colors: ThemeColors,
}

pub struct RgbaVisitor;

const COLOR_START: &'static str = "rgba(";
const COLOR_END: &'static str = ")";
const RGB_ERROR: &'static str = "Expecteded RGB color component to be a i32 from 0-255 or a f32 from 0-1";
const ALPHA_ERROR: &'static str = "Expected alpha component to be a f32 from 0-1";

impl <'de> Visitor<'de> for RgbaVisitor {
    type Value = [f32; 4];

    fn expecting(&self, formatter: &mut Formatter) -> std::fmt::Result {
        formatter.write_str("A style color: rgba(R: u8, G: u8, B: u8, A: f32)")
    }

    fn visit_str<E>(self, v: &str) -> Result<Self::Value, E>
    where
        E: Error,
    {
        if !v.starts_with(COLOR_START) ||
            !v.ends_with(COLOR_END) ||
            v.len() < COLOR_START.len() + COLOR_END.len() + 3 {
            Err(E::custom("Format for color declaration incorrect"))?;
        }
        let main: &str = &v[COLOR_START.len()..v.len() - 1];
        let parts: Vec<_> = main.split(",").collect();
        if parts.len() != 4 {
            Err(E::custom("Expected 4 color components"))?;
        }
        let mut out = [0.; 4];
        for (i, part) in parts[..3].iter().enumerate() {
            let part = part.trim();
            if part.contains(".") {
                // treat as a f32
                let value = f32::from_str(part)
                    .map_err(|_| E::custom(RGB_ERROR))?;
                if value < 0. || value > 1. {
                    Err(E::custom(RGB_ERROR))?;
                }
                out[i] = value;
            } else {
                // treat as a i32
                let value = i32::from_str_radix(part, 10)
                    .map_err(|_| E::custom(RGB_ERROR))?;
                if value < 0 || value > 255 {
                    Err(E::custom(RGB_ERROR))?;
                }
                out[i] = value as f32 / 255.;
            }
        }
        let alpha = f32::from_str(parts[3].trim())
            .map_err(|_| E::custom(ALPHA_ERROR))?;
        if alpha < 0. || alpha > 1. {
            Err(E::custom(ALPHA_ERROR))?;
        }
        out[3] = alpha;
        Ok(out)
    }

    fn visit_string<E>(self, v: String) -> Result<Self::Value, E>
    where
        E: Error,
    {
        self.visit_str(v.as_str())
    }
}

#[derive(Clone, Debug, PartialEq)]
#[repr(transparent)]
pub struct ThemeColor([f32; 4]);

impl<'de> Deserialize<'de> for ThemeColor {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        deserializer.deserialize_string(RgbaVisitor)
            .map(|t| ThemeColor(t))
    }
}

// #[derive(Debug, Serialize, Deserialize)]
#[derive(Debug, Deserialize)]
#[allow(non_snake_case)]
pub struct ThemeColors {
    Text: Option<ThemeColor>,
    TextDisabled: Option<ThemeColor>,
    WindowBg: Option<ThemeColor>,
    ChildBg: Option<ThemeColor>,
    PopupBg: Option<ThemeColor>,
    Border: Option<ThemeColor>,
    BorderShadow: Option<ThemeColor>,
    FrameBg: Option<ThemeColor>,
    FrameBgHovered: Option<ThemeColor>,
    FrameBgActive: Option<ThemeColor>,
    TitleBg: Option<ThemeColor>,
    TitleBgActive: Option<ThemeColor>,
    TitleBgCollapsed: Option<ThemeColor>,
    MenuBarBg: Option<ThemeColor>,
    ScrollbarBg: Option<ThemeColor>,
    ScrollbarGrab: Option<ThemeColor>,
    ScrollbarGrabHovered: Option<ThemeColor>,
    ScrollbarGrabActive: Option<ThemeColor>,
    CheckMark: Option<ThemeColor>,
    SliderGrab: Option<ThemeColor>,
    SliderGrabActive: Option<ThemeColor>,
    Button: Option<ThemeColor>,
    ButtonHovered: Option<ThemeColor>,
    ButtonActive: Option<ThemeColor>,
    Header: Option<ThemeColor>,
    HeaderHovered: Option<ThemeColor>,
    HeaderActive: Option<ThemeColor>,
    Separator: Option<ThemeColor>,
    SeparatorHovered: Option<ThemeColor>,
    SeparatorActive: Option<ThemeColor>,
    ResizeGrip: Option<ThemeColor>,
    ResizeGripHovered: Option<ThemeColor>,
    ResizeGripActive: Option<ThemeColor>,
    Tab: Option<ThemeColor>,
    TabHovered: Option<ThemeColor>,
    TabActive: Option<ThemeColor>,
    TabUnfocused: Option<ThemeColor>,
    TabUnfocusedActive: Option<ThemeColor>,
    PlotLines: Option<ThemeColor>,
    PlotLinesHovered: Option<ThemeColor>,
    PlotHistogram: Option<ThemeColor>,
    PlotHistogramHovered: Option<ThemeColor>,
    TableHeaderBg: Option<ThemeColor>,
    TableBorderStrong: Option<ThemeColor>,
    TableBorderLight: Option<ThemeColor>,
    TableRowBg: Option<ThemeColor>,
    TableRowBgAlt: Option<ThemeColor>,
    TextSelectedBg: Option<ThemeColor>,
    DragDropTarget: Option<ThemeColor>,
    NavHighlight: Option<ThemeColor>,
    NavWindowingHighlight: Option<ThemeColor>,
    NavWindowingDimBg: Option<ThemeColor>,
    ModalWindowDimBg: Option<ThemeColor>,
}

#[derive(Debug, Deserialize)]
pub struct Theme {
    pub name: String,
    // author: String,
    // description: String,
    // tags: Vec<String>,
    // date: String,
    pub style: ThemeStyle,
}

impl Theme {
    pub fn apply(&self, style: &mut Style) {
        self.style.apply(style);
    }
}

macro_rules! apply_theme_color {
    ($style:ident, $colors:expr, $name:ident) => {
        if let Some(color) = &$colors.$name {
            $style[StyleColor::$name] = color.0;
        }
    }
}

impl ThemeStyle {
    pub fn apply(&self, style: &mut Style) {
        style.alpha = self.alpha;
        style.disabled_alpha = self.disabled_alpha;
        style.window_padding = self.window_padding;
        style.window_border_size = self.window_border_size;
        style.window_min_size = self.window_min_size;
        style.window_title_align = self.window_title_align;
        style.window_menu_button_position = self.window_menu_button_position.0;
        style.child_rounding = self.child_rounding;
        style.child_border_size = self.child_border_size;
        style.popup_rounding = self.popup_rounding;
        style.popup_border_size = self.popup_border_size;
        style.frame_padding = self.frame_padding;
        style.frame_rounding = self.frame_rounding;
        style.frame_border_size = self.frame_border_size;
        style.item_spacing = self.item_spacing;
        style.item_inner_spacing = self.item_inner_spacing;
        style.cell_padding = self.cell_padding;
        style.indent_spacing = self.indent_spacing;
        style.columns_min_spacing = self.columns_min_spacing;
        style.scrollbar_size = self.scrollbar_size;
        style.scrollbar_rounding = self.scrollbar_rounding;
        style.grab_min_size = self.grab_min_size;
        style.grab_rounding = self.grab_rounding;
        style.tab_rounding = self.tab_rounding;
        style.tab_border_size = self.tab_border_size;
        style.tab_min_width_for_close_button = self.tab_min_width_for_close_button;
        style.color_button_position = self.color_button_position.0;
        style.button_text_align = self.button_text_align;
        style.selectable_text_align = self.selectable_text_align;
        apply_theme_color!(style, self.colors, Text);
        apply_theme_color!(style, self.colors, TextDisabled);
        apply_theme_color!(style, self.colors, WindowBg);
        apply_theme_color!(style, self.colors, ChildBg);
        apply_theme_color!(style, self.colors, PopupBg);
        apply_theme_color!(style, self.colors, Border);
        apply_theme_color!(style, self.colors, BorderShadow);
        apply_theme_color!(style, self.colors, FrameBg);
        apply_theme_color!(style, self.colors, FrameBgHovered);
        apply_theme_color!(style, self.colors, FrameBgActive);
        apply_theme_color!(style, self.colors, TitleBg);
        apply_theme_color!(style, self.colors, TitleBgActive);
        apply_theme_color!(style, self.colors, TitleBgCollapsed);
        apply_theme_color!(style, self.colors, MenuBarBg);
        apply_theme_color!(style, self.colors, ScrollbarBg);
        apply_theme_color!(style, self.colors, ScrollbarGrab);
        apply_theme_color!(style, self.colors, ScrollbarGrabHovered);
        apply_theme_color!(style, self.colors, ScrollbarGrabActive);
        apply_theme_color!(style, self.colors, CheckMark);
        apply_theme_color!(style, self.colors, SliderGrab);
        apply_theme_color!(style, self.colors, SliderGrabActive);
        apply_theme_color!(style, self.colors, Button);
        apply_theme_color!(style, self.colors, ButtonHovered);
        apply_theme_color!(style, self.colors, ButtonActive);
        apply_theme_color!(style, self.colors, Header);
        apply_theme_color!(style, self.colors, HeaderHovered);
        apply_theme_color!(style, self.colors, HeaderActive);
        apply_theme_color!(style, self.colors, Separator);
        apply_theme_color!(style, self.colors, SeparatorHovered);
        apply_theme_color!(style, self.colors, SeparatorActive);
        apply_theme_color!(style, self.colors, ResizeGrip);
        apply_theme_color!(style, self.colors, ResizeGripHovered);
        apply_theme_color!(style, self.colors, ResizeGripActive);
        apply_theme_color!(style, self.colors, Tab);
        apply_theme_color!(style, self.colors, TabHovered);
        // apply_theme_color!(style, self.colors, TabActive);
        // apply_theme_color!(style, self.colors, TabUnfocused);
        // apply_theme_color!(style, self.colors, TabUnfocusedActive);
        apply_theme_color!(style, self.colors, PlotLines);
        apply_theme_color!(style, self.colors, PlotLinesHovered);
        apply_theme_color!(style, self.colors, PlotHistogram);
        apply_theme_color!(style, self.colors, PlotHistogramHovered);
        apply_theme_color!(style, self.colors, TableHeaderBg);
        apply_theme_color!(style, self.colors, TableBorderStrong);
        apply_theme_color!(style, self.colors, TableBorderLight);
        apply_theme_color!(style, self.colors, TableRowBg);
        apply_theme_color!(style, self.colors, TableRowBgAlt);
        apply_theme_color!(style, self.colors, TextSelectedBg);
        apply_theme_color!(style, self.colors, DragDropTarget);
        apply_theme_color!(style, self.colors, NavHighlight);
        apply_theme_color!(style, self.colors, NavWindowingHighlight);
        apply_theme_color!(style, self.colors, NavWindowingDimBg);
        apply_theme_color!(style, self.colors, ModalWindowDimBg);
    }
}

type ThemeRegistryInner = Vec<Theme>;

#[derive(Debug)]
#[repr(transparent)]
pub struct ThemeRegistry(ThemeRegistryInner);

impl Default for ThemeRegistry {
    fn default() -> Self {
        Self(vec![])
    }
}

impl Deref for ThemeRegistry {
    type Target = ThemeRegistryInner;
    fn deref(&self) -> &Self::Target {
        &self.0
    }
}

impl DerefMut for ThemeRegistry {
    fn deref_mut(&mut self) -> &mut Self::Target {
        &mut self.0
    }
}

impl ThemeRegistry {
    pub fn from_path<P: AsRef<Path>>(path: P) -> crate::Result<Self> {
        let mut out = Self::default();
        out.extend_from_path(path)?;
        Ok(out)
    }

    pub fn extend_from_path<P: AsRef<Path>>(&mut self, path: P) -> crate::Result<()> {
        let toml = std::fs::read_to_string(path.as_ref())?;
        let root = toml.parse::<Table>()?;
        let themes = root.get("themes").unwrap().as_array().unwrap();
        for theme in themes {
            let name = theme.get("name").unwrap().as_str().unwrap();
            let style = theme.get("style").unwrap().as_table().unwrap();
            self.push(Theme {
                name: name.to_string(),
                style: toml::from_str::<ThemeStyle>(&style.to_string())?
            });
            logln!(Debug, "ThemeRegistry: Added \"{}\"", name);
        }
        self.sort_by(|a, b| a.name.cmp(&b.name));
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use toml::Table;
    use crate::themes::ThemeStyle;

    #[test]
    fn parse_themes_toml() -> crate::Result<()> {
        let toml = std::fs::read_to_string(
            "E:/Reloaded-II/Mods/p4rpc.trip2.debugui/data/themes.toml")?;
        let root = toml.parse::<Table>()?;
        let themes = root.get("themes").unwrap().as_array().unwrap();
        for theme in themes {
            let name = theme.get("name").unwrap().as_str().unwrap();
            let style = theme.get("style").unwrap().as_table().unwrap();
            let parsed = toml::from_str::<ThemeStyle>(&style.to_string())?;
        }
        Ok(())
    }
}