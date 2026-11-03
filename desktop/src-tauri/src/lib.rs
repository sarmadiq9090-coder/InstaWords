//! InstaWords desktop shell: the offline tafqit website (`_site/`) in a Tauri window.
//!
//! The site is bundled unchanged. Desktop-only behaviour lives in `init.js`, injected
//! into every page before the site's own scripts, plus two commands it calls:
//! - `save_file`: native "Save as" dialog for links with a `download` attribute
//!   (Tafqit.bas, CSV export). The page never chooses a path; the user does.
//! - `open_external`: opens http(s)/mailto links in the system browser.
//! Navigation and `window.open` are also guarded here, so an external URL can never
//! load inside the app window even if a click slips past the injected script.

use std::path::Path;

use percent_encoding::percent_decode_str;
use tauri::{
    ipc::{InvokeBody, Request},
    webview::NewWindowResponse,
    AppHandle, Manager, Runtime, Url, WebviewWindow, WebviewWindowBuilder,
};
use tauri_plugin_dialog::DialogExt;
use tauri_plugin_opener::OpenerExt;

const INIT_SCRIPT: &str = include_str!("init.js");

/// URLs served from the bundled site: `tauri://localhost` (macOS/Linux) or
/// `http(s)://tauri.localhost` (Windows).
fn is_app_url(url: &Url) -> bool {
    match url.scheme() {
        "tauri" => true,
        "http" | "https" => url.host_str() == Some("tauri.localhost"),
        "about" => url.as_str() == "about:blank",
        _ => false,
    }
}

fn is_external_url(url: &Url) -> bool {
    matches!(url.scheme(), "http" | "https" | "mailto") && !is_app_url(url)
}

fn open_in_browser<R: Runtime>(app: &AppHandle<R>, url: &Url) {
    if is_external_url(url) {
        if let Err(e) = app.opener().open_url(url.as_str(), None::<&str>) {
            eprintln!("open_external: {e}");
        }
    }
}

#[tauri::command]
fn open_external<R: Runtime>(app: AppHandle<R>, url: String) -> Result<(), String> {
    let url = Url::parse(&url).map_err(|e| e.to_string())?;
    if !is_external_url(&url) {
        return Err(format!("refusing to open {url}"));
    }
    app.opener().open_url(url.as_str(), None::<&str>).map_err(|e| e.to_string())
}

/// Keep only a plain file name (no directories, no characters Windows rejects).
fn clean_file_name(raw: &str) -> String {
    let base = raw.rsplit(['/', '\\']).next().unwrap_or_default();
    let cleaned: String = base
        .chars()
        .filter(|c| !c.is_control() && !matches!(c, '<' | '>' | ':' | '"' | '|' | '?' | '*'))
        .collect();
    let cleaned = cleaned.trim().trim_matches('.').to_string();
    if cleaned.is_empty() {
        "download".into()
    } else {
        cleaned
    }
}

/// Body: the file's raw bytes. Header `x-file-name`: suggested name (URI-encoded).
/// Returns `true` when saved, `false` when the user cancelled the dialog.
#[tauri::command]
async fn save_file<R: Runtime>(window: WebviewWindow<R>, request: Request<'_>) -> Result<bool, String> {
    // Raw bytes over the custom-protocol IPC; a JSON number array if Tauri fell back to postMessage.
    let bytes: Vec<u8> = match request.body() {
        InvokeBody::Raw(bytes) => bytes.clone(),
        InvokeBody::Json(value) => value
            .as_array()
            .and_then(|a| a.iter().map(|n| n.as_u64().and_then(|n| u8::try_from(n).ok())).collect())
            .ok_or("save_file expects a binary body")?,
    };
    let suggested = request
        .headers()
        .get("x-file-name")
        .and_then(|v| v.to_str().ok())
        .and_then(|v| percent_decode_str(v).decode_utf8().ok())
        .map(|v| clean_file_name(&v))
        .unwrap_or_else(|| "download".into());

    let mut dialog = window
        .dialog()
        .file()
        .set_parent(&window)
        .set_title("حفظ الملف — Save file")
        .set_file_name(&suggested);
    if let Some(ext) = Path::new(&suggested).extension().and_then(|e| e.to_str()) {
        let label = match ext.to_ascii_lowercase().as_str() {
            "csv" => "CSV".to_string(),
            "bas" => "VBA module (.bas)".to_string(),
            other => format!(".{other}"),
        };
        dialog = dialog.add_filter(label, &[ext]);
    }

    let Some(path) = dialog.blocking_save_file() else {
        return Ok(false);
    };
    let path = path.into_path().map_err(|e| e.to_string())?;
    std::fs::write(&path, &bytes).map_err(|e| format!("{}: {e}", path.display()))?;
    Ok(true)
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_opener::init())
        .plugin(tauri_plugin_dialog::init())
        .invoke_handler(tauri::generate_handler![save_file, open_external])
        .setup(|app| {
            // The window is declared in tauri.conf.json with `"create": false` so it can be
            // built here with the init script and navigation guards attached.
            let config = app
                .config()
                .app
                .windows
                .iter()
                .find(|w| w.label == "main")
                .cloned()
                .expect("tauri.conf.json must declare the \"main\" window");
            let nav_app = app.handle().clone();
            let popup_app = app.handle().clone();
            WebviewWindowBuilder::from_config(app.handle(), &config)?
                .initialization_script(INIT_SCRIPT)
                .on_navigation(move |url| {
                    if is_app_url(url) {
                        return true;
                    }
                    open_in_browser(&nav_app, url);
                    false
                })
                .on_new_window(move |url, _features| {
                    open_in_browser(&popup_app, &url);
                    NewWindowResponse::Deny
                })
                .build()?;
            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("error while running InstaWords");
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn app_and_external_urls() {
        let u = |s: &str| Url::parse(s).unwrap();
        assert!(is_app_url(&u("https://tauri.localhost/bulk.html")));
        assert!(is_app_url(&u("http://tauri.localhost/")));
        assert!(is_app_url(&u("tauri://localhost/index.html")));
        assert!(!is_app_url(&u("https://fastkiwi.app/ar/")));
        assert!(!is_app_url(&u("file:///C:/x.csv")));
        assert!(is_external_url(&u("https://github.com/sarmadiq9090-coder/tafket")));
        assert!(is_external_url(&u("mailto:a@b.c")));
        assert!(!is_external_url(&u("https://tauri.localhost/excel.html")));
        assert!(!is_external_url(&u("file:///etc/passwd")));
        assert!(!is_external_url(&u("javascript:alert(1)")));
    }

    #[test]
    fn file_names() {
        assert_eq!(clean_file_name("tafqit.csv"), "tafqit.csv");
        assert_eq!(clean_file_name("..\\..\\Windows\\evil.bas"), "evil.bas");
        assert_eq!(clean_file_name("a/b/c:d?.csv"), "cd.csv");
        assert_eq!(clean_file_name("تفقيط.csv"), "تفقيط.csv");
        assert_eq!(clean_file_name(".."), "download");
        assert_eq!(clean_file_name(""), "download");
    }
}
