cmake_minimum_required(VERSION 3.15)
project(WebViewLauncher)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Find WebView2
find_path(WEBVIEW2_INCLUDE_DIR
    NAMES WebView2.h
    PATHS
        "${CMAKE_CURRENT_SOURCE_DIR}/../../../../../../../../Program Files (x86)/Microsoft/EdgeWebView/Application"
        "${CMAKE_CURRENT_SOURCE_DIR}/../../../../../../../../Program Files/Microsoft/EdgeWebView/Application"
        "$ENV{ProgramFiles(x86)}/Microsoft/EdgeWebView/Application"
        "$ENV{ProgramFiles}/Microsoft/EdgeWebView/Application"
    PATH_SUFFIXES
        "include"
        "."
)

# Add executable
add_library(WebViewLauncher SHARED
    WebViewLauncher.cpp
)

# Include directories
target_include_directories(WebViewLauncher PRIVATE
    ${WEBVIEW2_INCLUDE_DIR}
)

# Link libraries
target_link_libraries(WebViewLauncher PRIVATE
    ole32
    oleaut32
    user32
    gdi32
    winspool
    comdlg32
    advapi32
    shell32
    uuid
)

# Set output directory
set_target_properties(WebViewLauncher PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
)

# Define export macro
target_compile_definitions(WebViewLauncher PRIVATE
    WEBVIEWLAUNCHER_EXPORTS
    UNICODE
    _UNICODE
)


