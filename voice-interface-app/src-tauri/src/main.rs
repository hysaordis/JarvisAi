#![cfg_attr(
    all(not(debug_assertions), target_os = "windows"),
    windows_subsystem = "windows"
)]
use tauri::{CustomMenuItem, SystemTray, SystemTrayMenu, SystemTrayEvent, WindowEvent};
use tauri::Manager;

#[tauri::command]
fn minimize_to_tray(window: tauri::Window) {
    window.hide().unwrap();
}

fn main() {
    let quit = CustomMenuItem::new("quit".to_string(), "Quit");
    let hide = CustomMenuItem::new("hide".to_string(), "Hide");
    let show = CustomMenuItem::new("show".to_string(), "Show");
   
    let tray_menu = SystemTrayMenu::new()
        .add_item(show)
        .add_item(hide)
        .add_item(quit);
       
    let system_tray = SystemTray::new()
        .with_menu(tray_menu);

    tauri::Builder::default()
        .system_tray(system_tray)
        .invoke_handler(tauri::generate_handler![minimize_to_tray])
        .setup(|app| {
            let window = app.get_window("main").unwrap();
            window.set_decorations(false)?;
            window.set_always_on_top(true)?;
            window.set_fullscreen(false)?; // Aggiungi questa riga
            window.show()?;
            Ok(())
        })
        .on_system_tray_event(|app, event| match event {
            SystemTrayEvent::MenuItemClick { id, .. } => {
                let window = app.get_window("main").unwrap();
                match id.as_str() {
                    "quit" => {
                        std::process::exit(0);
                    }
                    "hide" => {
                        window.hide().unwrap();
                    }
                    "show" => {
                        window.show().unwrap();
                        window.set_always_on_top(true).unwrap();
                        window.center().unwrap();
                    }
                    _ => {}
                }
            }
            SystemTrayEvent::LeftClick { .. } => {
                let window = app.get_window("main").unwrap();
                if !window.is_visible().unwrap() {
                    window.show().unwrap();
                    window.set_always_on_top(true).unwrap();
                }
            }
            _ => {}
        })
        .on_window_event(|event| {
            if let WindowEvent::CloseRequested { api, .. } = event.event() {
                event.window().hide().unwrap();
                api.prevent_close();
            }
        })
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}