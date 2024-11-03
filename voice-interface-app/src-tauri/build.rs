fn main() {
    #[cfg(target_os = "windows")]
    {
        println!("cargo:rustc-link-search=native=C:\\Windows\\System32");
        println!("cargo:rustc-link-lib=dylib=dwmapi");
    }
}
