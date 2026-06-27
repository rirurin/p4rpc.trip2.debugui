use std::path::{Path, PathBuf};
use riri_mod_tools::{git_version, mod_package};

fn get_project_root<P>(base: P) -> PathBuf
where P: AsRef<Path> {
    base.as_ref()
        .parent().unwrap()
        .join("Cargo.toml")
}

fn get_output_directory() -> PathBuf {
    PathBuf::from(std::env::var("OUT_DIR").unwrap())
        .parent().unwrap()
        .parent().unwrap()
        .parent().unwrap()
        .to_owned()
}

fn main() {
    let base = std::env::current_dir().unwrap();
    let out_dir = get_output_directory();
    // Compile shaders and copy into the crate's /shaders folder so we can push the compiled
    // bytecode to the repo. This avoids having to bundle shaderc which slows down compilation
    // by a lot.
    let shader_names = [
        "shaders/basic3d.ps",
        "shaders/basic3d.vs",
        "shaders/imgui.ps",
        "shaders/imgui.vs",
        "shaders/phong.ps",
        "shaders/phong.vs",
    ];

    let shaders_out = shader_names.map(|v| {
        let (name, ext) = v.rsplit_once(".").unwrap();
        let spirv_ext = format!("{}.{}.spv", name, ext);
        base.join(&spirv_ext)
    });
    let shaders_target = shader_names.map(|v| {
        let (name, ext) = v.rsplit_once(".").unwrap();
        let spirv_ext = format!("{}.{}.spv", name, ext);
        out_dir.join(&spirv_ext)
    });
    std::fs::create_dir_all(out_dir.join("shaders")).unwrap();
    // Copy compiled shaders to output directory
    for (path_in, path_out) in shaders_out.iter().zip(shaders_target.iter()) {
        std::fs::copy(path_in, path_out).unwrap();
    }
    // Copy fonts to output directory
    let font_names = [
        "data/LibreBodoni-Bold.ttf",
        "data/QwitcherGrypen-Bold.ttf",
        "data/NotoSansCJKjp-Medium.otf"
    ];
    let fonts_in = font_names.map(|v| base.join(v));
    let fonts_out = font_names.map(|v| out_dir.join(v));
    std::fs::create_dir_all(out_dir.join("data")).unwrap();
    for (font_in, font_out) in fonts_in.iter().zip(fonts_out.iter()) {
        std::fs::copy(font_in, font_out).unwrap();
    }
    // Get version info
    let cargo_info = mod_package::CargoInfo::new_with_resolver(base.as_path(), get_project_root).unwrap();
    git_version::create_version_file(&base, cargo_info.get_package_string_required("version").unwrap());
}