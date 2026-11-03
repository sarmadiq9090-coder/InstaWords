fn main() {
    // Declare the app's own commands so they are denied unless a capability grants them
    // (capabilities/main.json grants exactly these two to the "main" window).
    tauri_build::try_build(
        tauri_build::Attributes::new()
            .app_manifest(tauri_build::AppManifest::new().commands(&["save_file", "open_external"])),
    )
    .expect("failed to run tauri-build");
}
