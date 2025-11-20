#include <windows.h>
#include <string>
#include <queue>
#include <mutex>
#include <memory>
#include <map>
#include <comdef.h>
#include <comutil.h>
#include <oaidl.h>
#include <WebView2.h>
#include <wrl.h>
#include <wrl/client.h>

using namespace Microsoft::WRL;

// Forward declarations
class WebViewWindow;

// Global callback function pointers - set by C# via P/Invoke
typedef void (*PaymentSuccessCallback)();
typedef void (*PaymentFailureCallback)();
typedef void (*PurchaseProcessingCallback)();
typedef void (*OptinResponseCallback)(const char* optinType);

static PaymentSuccessCallback g_paymentSuccessCallback = nullptr;
static PaymentFailureCallback g_paymentFailureCallback = nullptr;
static PurchaseProcessingCallback g_purchaseProcessingCallback = nullptr;
static OptinResponseCallback g_optinResponseCallback = nullptr;

// Simple in-memory notification queue (max 10 notifications)
#define MAX_NOTIFICATIONS 10
#define MAX_NOTIFICATION_LENGTH 256

struct Notification {
    char type[64];
    char data[192]; // For optin type or other data
};

static Notification g_notificationQueue[MAX_NOTIFICATIONS];
static int g_notificationQueueHead = 0;
static int g_notificationQueueTail = 0;
static int g_notificationQueueCount = 0;
static std::mutex g_notificationMutex;

// Helper function to convert std::string to BSTR
BSTR StringToBSTR(const std::string& str) {
    int len = MultiByteToWideChar(CP_UTF8, 0, str.c_str(), -1, NULL, 0);
    BSTR bstr = SysAllocStringLen(NULL, len - 1);
    MultiByteToWideChar(CP_UTF8, 0, str.c_str(), -1, bstr, len);
    return bstr;
}

// Helper function to convert BSTR to std::string
std::string BSTRToString(BSTR bstr) {
    if (!bstr) return "";
    int len = WideCharToMultiByte(CP_UTF8, 0, bstr, -1, NULL, 0, NULL, NULL);
    std::string result(len - 1, '\0');
    WideCharToMultiByte(CP_UTF8, 0, bstr, -1, &result[0], len, NULL, NULL);
    return result;
}

// WebViewWindow class
class WebViewWindow {
public:
    HWND m_hwnd;
    ComPtr<ICoreWebView2Controller> m_webViewController;
    ComPtr<ICoreWebView2> m_webView;
    std::wstring m_url;
    // Note: Host object not needed - AddWebMessageReceived automatically enables window.chrome.webview.postMessage()
    EventRegistrationToken m_webMessageToken; // Store for cleanup
    EventRegistrationToken m_navCompletedToken; // Store for cleanup

    WebViewWindow(HWND hwnd, const char* url) : m_hwnd(hwnd) {
        // Initialize tokens
        m_webMessageToken.value = 0;
        m_navCompletedToken.value = 0;
        if (url) {
            int len = MultiByteToWideChar(CP_UTF8, 0, url, -1, NULL, 0);
            m_url.resize(len - 1);
            MultiByteToWideChar(CP_UTF8, 0, url, -1, &m_url[0], len);
        }
    }

    ~WebViewWindow() {
        // Remove event handlers first
        if (m_webView) {
            try {
                if (m_webMessageToken.value != 0) {
                    m_webView->remove_WebMessageReceived(m_webMessageToken);
                }
                if (m_navCompletedToken.value != 0) {
                    m_webView->remove_NavigationCompleted(m_navCompletedToken);
                }
            } catch (...) {
                // Ignore errors during cleanup
            }
        }
        
        // Close and release WebView controller
        if (m_webViewController) {
            try {
                m_webViewController->Close();
            } catch (...) {
                // Ignore errors
            }
            Sleep(50);
            m_webViewController.Reset();
        }
        
        // Release WebView
        if (m_webView) {
            m_webView.Reset();
        }
    }

    HRESULT InitializeWebView() {
        // Get user's AppData\Local path for WebView2 user data folder
        // This avoids "Access is denied" errors when Unity is in Program Files
        wchar_t* localAppData = nullptr;
        size_t len = 0;
        if (_wdupenv_s(&localAppData, &len, L"LOCALAPPDATA") == 0 && localAppData) {
            std::wstring userDataFolder = std::wstring(localAppData) + L"\\Unity\\WebView2";
            free(localAppData);
            
            CreateDirectoryW(userDataFolder.c_str(), NULL);
            
            HRESULT hr = CreateCoreWebView2EnvironmentWithOptions(
                nullptr, userDataFolder.c_str(), nullptr,
                Callback<ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler>(
                [this](HRESULT result, ICoreWebView2Environment* env) -> HRESULT {
                    if (FAILED(result)) {
                        return result;
                    }

                    return env->CreateCoreWebView2Controller(m_hwnd,
                        Callback<ICoreWebView2CreateCoreWebView2ControllerCompletedHandler>(
                            [this](HRESULT result, ICoreWebView2Controller* controller) -> HRESULT {
                                if (FAILED(result)) {
                                    return result;
                                }
                                
                                m_webViewController = controller;
                                m_webViewController->get_CoreWebView2(&m_webView);
                                
                                if (!m_webView) {
                                    return E_FAIL;
                                }
                                
                                // Set up WebMessageReceived handler - enables window.chrome.webview.postMessage()
                                // Must be called before injecting JavaScript
                                HRESULT msgResult = m_webView->add_WebMessageReceived(
                                    Callback<ICoreWebView2WebMessageReceivedEventHandler>(
                                        [this](ICoreWebView2* sender, ICoreWebView2WebMessageReceivedEventArgs* args) -> HRESULT {
                                            LPWSTR message = nullptr;
                                            HRESULT hr = args->TryGetWebMessageAsString(&message);
                                            if (SUCCEEDED(hr) && message) {
                                                this->HandleWebMessage(message);
                                                CoTaskMemFree(message);
                                            }
                                            return S_OK;
                                        }).Get(), &m_webMessageToken);
                                
                                if (FAILED(msgResult)) {
                                    return msgResult;
                                }

                                // Inject JavaScript to set up stash_sdk callbacks
                                const char* stashSDKScript = R"(
                                    (function() {
                                        window.stash_sdk = window.stash_sdk || {};
                                        var postMessage = function(message) {
                                            try {
                                                if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
                                                    window.chrome.webview.postMessage(message);
                                                }
                                            } catch(e) {
                                                // Silently ignore errors
                                            }
                                        };
                                        window.stash_sdk.onPaymentSuccess = function(data) {
                                            postMessage(JSON.stringify({type: 'stashPaymentSuccess', data: data || {}}));
                                        };
                                        window.stash_sdk.onPaymentFailure = function(data) {
                                            postMessage(JSON.stringify({type: 'stashPaymentFailure', data: data || {}}));
                                        };
                                        window.stash_sdk.onPurchaseProcessing = function(data) {
                                            postMessage(JSON.stringify({type: 'stashPurchaseProcessing', data: data || {}}));
                                        };
                                        window.stash_sdk.setPaymentChannel = function(optinType) {
                                            postMessage(JSON.stringify({type: 'stashOptin', data: optinType || ''}));
                                        };
                                    })();
                                )";

                                std::wstring script;
                                int len = MultiByteToWideChar(CP_UTF8, 0, stashSDKScript, -1, NULL, 0);
                                script.resize(len - 1);
                                MultiByteToWideChar(CP_UTF8, 0, stashSDKScript, -1, &script[0], len);

                                m_webView->AddScriptToExecuteOnDocumentCreated(script.c_str(), nullptr);

                                // Resize WebView to fill window
                                RECT bounds;
                                GetClientRect(m_hwnd, &bounds);
                                if (bounds.right > bounds.left && bounds.bottom > bounds.top) {
                                    m_webViewController->put_Bounds(bounds);
                                } else {
                                    bounds.left = 0;
                                    bounds.top = 0;
                                    bounds.right = 800;
                                    bounds.bottom = 600;
                                    m_webViewController->put_Bounds(bounds);
                                }
                                
                                m_webViewController->put_IsVisible(TRUE);
                                
                                // Force the window to repaint
                                InvalidateRect(m_hwnd, NULL, TRUE);
                                UpdateWindow(m_hwnd);
                                SetFocus(m_hwnd);

                                // Add navigation completed handler FIRST (before navigating)
                                m_webView->add_NavigationCompleted(
                                    Callback<ICoreWebView2NavigationCompletedEventHandler>(
                                        [this](ICoreWebView2* sender, ICoreWebView2NavigationCompletedEventArgs* args) -> HRESULT {
                                            BOOL isSuccess = FALSE;
                                            args->get_IsSuccess(&isSuccess);
                                            COREWEBVIEW2_WEB_ERROR_STATUS errorStatus;
                                            args->get_WebErrorStatus(&errorStatus);
                                            
                                            if (isSuccess) {
                                                // Force a repaint after navigation
                                                InvalidateRect(m_hwnd, NULL, TRUE);
                                                UpdateWindow(m_hwnd);
                                                SetForegroundWindow(m_hwnd);
                                                SetFocus(m_hwnd);
                                                
                                                // Re-inject stash_sdk script after navigation to ensure it's available
                                                const char* stashSDKScript = R"(
                                                    (function() {
                                                        window.stash_sdk = window.stash_sdk || {};
                                                        var postMessage = function(message) {
                                                            try {
                                                                if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
                                                                    window.chrome.webview.postMessage(message);
                                                                }
                                                            } catch(e) {
                                                                // Silently ignore errors
                                                            }
                                                        };
                                                        window.stash_sdk.onPaymentSuccess = function(data) {
                                                            postMessage(JSON.stringify({type: 'stashPaymentSuccess', data: data || {}}));
                                                        };
                                                        window.stash_sdk.onPaymentFailure = function(data) {
                                                            postMessage(JSON.stringify({type: 'stashPaymentFailure', data: data || {}}));
                                                        };
                                                        window.stash_sdk.onPurchaseProcessing = function(data) {
                                                            postMessage(JSON.stringify({type: 'stashPurchaseProcessing', data: data || {}}));
                                                        };
                                                        window.stash_sdk.setPaymentChannel = function(optinType) {
                                                            postMessage(JSON.stringify({type: 'stashOptin', data: optinType || ''}));
                                                        };
                                                    })();
                                                )";
                                                std::wstring script;
                                                int scriptLen = MultiByteToWideChar(CP_UTF8, 0, stashSDKScript, -1, NULL, 0);
                                                script.resize(scriptLen - 1);
                                                MultiByteToWideChar(CP_UTF8, 0, stashSDKScript, -1, &script[0], scriptLen);
                                                
                                                m_webView->ExecuteScript(script.c_str(), nullptr);
                                            }
                                            return S_OK;
                                        }).Get(), &m_navCompletedToken);
                                
                                // Navigate to URL after WebView is ready
                                if (!m_url.empty()) {
                                    HRESULT immediateNav = m_webView->Navigate(m_url.c_str());
                                    if (FAILED(immediateNav)) {
                                        Sleep(200);
                                        PostMessage(m_hwnd, WM_USER + 1, 0, 0);
                                    }
                                }

                                return S_OK;
                            }).Get());
                }).Get());
            
            return hr;
        } else {
            return E_FAIL;
        }
    }

private:
    void HandleWebMessage(const wchar_t* message) {
        // Convert wide string to UTF-8
        std::string msgUtf8;
        int len = WideCharToMultiByte(CP_UTF8, 0, message, -1, NULL, 0, NULL, NULL);
        if (len > 0) {
            msgUtf8.resize(len - 1);
            WideCharToMultiByte(CP_UTF8, 0, message, -1, &msgUtf8[0], len, NULL, NULL);
        }

        // Parse JSON message - look for type field
        // Expected format: {"type":"stashPaymentSuccess","data":{...}}
        
        // Check for payment success
        if (msgUtf8.find("\"type\":\"stashPaymentSuccess\"") != std::string::npos ||
            msgUtf8.find("\"type\": 'stashPaymentSuccess'") != std::string::npos ||
            msgUtf8.find("'type':'stashPaymentSuccess'") != std::string::npos ||
            msgUtf8.find("'type':\"stashPaymentSuccess\"") != std::string::npos) {
            // Ignore test messages
            bool isTest = msgUtf8.find("\"test\":true") != std::string::npos || 
                         msgUtf8.find("'test':true") != std::string::npos ||
                         msgUtf8.find("\"test\": true") != std::string::npos ||
                         msgUtf8.find("'test': true") != std::string::npos;
            if (isTest) {
                return;
            }
            
            if (g_paymentSuccessCallback) {
                g_paymentSuccessCallback();
            } else {
                QueueNotification("StashPaymentSuccess", "");
            }
            return;
        }
        
        // Check for payment failure
        if (msgUtf8.find("\"type\":\"stashPaymentFailure\"") != std::string::npos ||
            msgUtf8.find("\"type\": 'stashPaymentFailure'") != std::string::npos ||
            msgUtf8.find("'type':'stashPaymentFailure'") != std::string::npos ||
            msgUtf8.find("'type':\"stashPaymentFailure\"") != std::string::npos) {
            if (g_paymentFailureCallback) {
                g_paymentFailureCallback();
            } else {
                QueueNotification("StashPaymentFailure", "");
            }
            return;
        }
        
        // Check for purchase processing
        if (msgUtf8.find("\"type\":\"stashPurchaseProcessing\"") != std::string::npos ||
            msgUtf8.find("\"type\": 'stashPurchaseProcessing'") != std::string::npos ||
            msgUtf8.find("'type':'stashPurchaseProcessing'") != std::string::npos ||
            msgUtf8.find("'type':\"stashPurchaseProcessing\"") != std::string::npos) {
            if (g_purchaseProcessingCallback) {
                g_purchaseProcessingCallback();
            } else {
                QueueNotification("StashPurchaseProcessing", "");
            }
            return;
        }
        
        // Check for optin
        if (msgUtf8.find("\"type\":\"stashOptin\"") != std::string::npos ||
            msgUtf8.find("\"type\": 'stashOptin'") != std::string::npos ||
            msgUtf8.find("'type':'stashOptin'") != std::string::npos ||
            msgUtf8.find("'type':\"stashOptin\"") != std::string::npos) {
            // Extract optin type from data field
            std::string optinType = "";
            
            size_t dataPos = msgUtf8.find("\"data\":");
            if (dataPos == std::string::npos) {
                dataPos = msgUtf8.find("'data':");
            }
            
            if (dataPos != std::string::npos) {
                size_t valueStart = dataPos + 7;
                while (valueStart < msgUtf8.length() && 
                       (msgUtf8[valueStart] == ' ' || msgUtf8[valueStart] == '\t' || msgUtf8[valueStart] == ':')) {
                    valueStart++;
                }
                
                if (valueStart < msgUtf8.length() && (msgUtf8[valueStart] == '"' || msgUtf8[valueStart] == '\'')) {
                    char quote = msgUtf8[valueStart];
                    valueStart++;
                    size_t valueEnd = valueStart;
                    while (valueEnd < msgUtf8.length() && msgUtf8[valueEnd] != quote) {
                        valueEnd++;
                    }
                    if (valueEnd > valueStart) {
                        optinType = msgUtf8.substr(valueStart, valueEnd - valueStart);
                    }
                } else {
                    size_t valueEnd = valueStart;
                    while (valueEnd < msgUtf8.length() && 
                           msgUtf8[valueEnd] != ',' && 
                           msgUtf8[valueEnd] != '}' && 
                           msgUtf8[valueEnd] != ' ' &&
                           msgUtf8[valueEnd] != '\t') {
                        valueEnd++;
                    }
                    if (valueEnd > valueStart) {
                        optinType = msgUtf8.substr(valueStart, valueEnd - valueStart);
                        if (optinType.length() >= 2 && 
                            ((optinType[0] == '"' && optinType[optinType.length()-1] == '"') ||
                             (optinType[0] == '\'' && optinType[optinType.length()-1] == '\''))) {
                            optinType = optinType.substr(1, optinType.length() - 2);
                        }
                    }
                }
            }
            
            if (g_optinResponseCallback) {
                g_optinResponseCallback(optinType.c_str());
            } else {
                QueueNotification("StashOptinResponse", optinType);
            }
            return;
        }
        
    }

    void QueueNotification(const char* type, const std::string& data) {
        std::lock_guard<std::mutex> lock(g_notificationMutex);
        if (g_notificationQueueCount < MAX_NOTIFICATIONS) {
            strncpy_s(g_notificationQueue[g_notificationQueueTail].type, type, _TRUNCATE);
            strncpy_s(g_notificationQueue[g_notificationQueueTail].data, data.c_str(), _TRUNCATE);
            g_notificationQueueTail = (g_notificationQueueTail + 1) % MAX_NOTIFICATIONS;
            g_notificationQueueCount++;
        }
    }
};

// Window procedure
static LRESULT CALLBACK WindowProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam) {
    WebViewWindow* window = reinterpret_cast<WebViewWindow*>(GetWindowLongPtr(hwnd, GWLP_USERDATA));

    switch (uMsg) {
    case WM_CREATE:
        {
            CREATESTRUCT* cs = reinterpret_cast<CREATESTRUCT*>(lParam);
            const char* url = reinterpret_cast<const char*>(cs->lpCreateParams);
            window = new WebViewWindow(hwnd, url);
            SetWindowLongPtr(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(window));
            window->InitializeWebView();
            return 0;
        }
    case WM_DESTROY:
        if (window) {
            SetWindowLongPtr(hwnd, GWLP_USERDATA, 0);
            delete window;
            window = nullptr;
        }
        return 0;
    case WM_SIZE:
        if (window && window->m_webViewController) {
            RECT bounds;
            GetClientRect(hwnd, &bounds);
            window->m_webViewController->put_Bounds(bounds);
        }
        return 0;
    case WM_PAINT:
        if (window && window->m_webViewController) {
            PAINTSTRUCT ps;
            BeginPaint(hwnd, &ps);
            EndPaint(hwnd, &ps);
        } else {
            PAINTSTRUCT ps;
            BeginPaint(hwnd, &ps);
            EndPaint(hwnd, &ps);
        }
        return 0;
    case WM_CLOSE:
        DestroyWindow(hwnd);
        return 0;
    case WM_USER + 1:
        // Custom message to trigger navigation after WebView initialization
        if (window && window->m_webView && !window->m_url.empty()) {
            SetForegroundWindow(hwnd);
            SetFocus(hwnd);
            
            BOOL isVisible = FALSE;
            if (window->m_webViewController) {
                window->m_webViewController->get_IsVisible(&isVisible);
                if (!isVisible) {
                    window->m_webViewController->put_IsVisible(TRUE);
                }
            }
            
            HRESULT navResult = window->m_webView->Navigate(window->m_url.c_str());
            if (FAILED(navResult)) {
                static std::map<HWND, int> retryCounts;
                int& retryCount = retryCounts[hwnd];
                if (retryCount < 5) {
                    retryCount++;
                    Sleep(200);
                    PostMessage(hwnd, WM_USER + 1, 0, 0);
                } else {
                    retryCount = 0;
                    retryCounts.erase(hwnd);
                }
            } else {
                InvalidateRect(hwnd, NULL, TRUE);
                UpdateWindow(hwnd);
                static std::map<HWND, int> retryCounts;
                retryCounts.erase(hwnd);
            }
        }
        return 0;
    default:
        return DefWindowProc(hwnd, uMsg, wParam, lParam);
    }
}

// Note: We don't use a global window map - each window is tracked via GWLP_USERDATA

extern "C" {
    __declspec(dllexport) void* CreateWebViewWindow(double x, double y, double width, double height, const char* url) {
        // Register window class
        static bool classRegistered = false;
        if (!classRegistered) {
            WNDCLASSEX wc = {};
            wc.cbSize = sizeof(WNDCLASSEX);
            wc.style = CS_HREDRAW | CS_VREDRAW;
            wc.lpfnWndProc = WindowProc;
            wc.hInstance = GetModuleHandle(NULL);
            wc.hCursor = LoadCursor(NULL, IDC_ARROW);
            wc.lpszClassName = L"StashWebViewWindow";
            RegisterClassEx(&wc);
            classRegistered = true;
        }

        // Create window
        HWND hwnd = CreateWindowEx(
            0,
            L"StashWebViewWindow",
            L"StashPayCard Preview",
            WS_OVERLAPPEDWINDOW,
            (int)x, (int)y, (int)width, (int)height,
            NULL, NULL, GetModuleHandle(NULL),
            const_cast<char*>(url));

        if (!hwnd) {
            return nullptr;
        }

        ShowWindow(hwnd, SW_SHOW);
        SetForegroundWindow(hwnd);
        SetFocus(hwnd);
        UpdateWindow(hwnd);
        InvalidateRect(hwnd, NULL, TRUE);

        // Process messages to allow WebView to initialize
        for (int i = 0; i < 30; i++) {
            MSG msg;
            while (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE)) {
                TranslateMessage(&msg);
                DispatchMessage(&msg);
            }
            if (i < 29) {
                Sleep(30);
            }
        }

        return hwnd;
    }

    __declspec(dllexport) void DestroyWebViewWindow(void* windowPtr) {
        if (!windowPtr) {
            return;
        }
        
        HWND hwnd = reinterpret_cast<HWND>(windowPtr);
        
        if (!IsWindow(hwnd)) {
            return;
        }
        
        WebViewWindow* window = reinterpret_cast<WebViewWindow*>(GetWindowLongPtr(hwnd, GWLP_USERDATA));
        if (window) {
            SetWindowLongPtr(hwnd, GWLP_USERDATA, 0);
            delete window;
            window = nullptr;
        }
        
        if (IsWindow(hwnd)) {
            SendMessage(hwnd, WM_CLOSE, 0, 0);
            Sleep(50);
            DestroyWindow(hwnd);
        }
        
        // Process remaining messages to ensure cleanup completes
        for (int i = 0; i < 30; i++) {
            MSG msg;
            while (PeekMessage(&msg, hwnd, 0, 0, PM_REMOVE)) {
                TranslateMessage(&msg);
                DispatchMessage(&msg);
            }
            while (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE | PM_NOYIELD)) {
                if (msg.hwnd == hwnd || msg.hwnd == NULL) {
                    TranslateMessage(&msg);
                    DispatchMessage(&msg);
                } else {
                    PostMessage(msg.hwnd, msg.message, msg.wParam, msg.lParam);
                    break;
                }
            }
            if (i < 29) {
                Sleep(30);
            }
        }
    }

    __declspec(dllexport) void SetPaymentSuccessCallback(void* callbackPtr) {
        g_paymentSuccessCallback = reinterpret_cast<PaymentSuccessCallback>(callbackPtr);
    }

    __declspec(dllexport) void SetPaymentFailureCallback(void* callbackPtr) {
        g_paymentFailureCallback = reinterpret_cast<PaymentFailureCallback>(callbackPtr);
    }

    __declspec(dllexport) void SetPurchaseProcessingCallback(void* callbackPtr) {
        g_purchaseProcessingCallback = reinterpret_cast<PurchaseProcessingCallback>(callbackPtr);
    }

    __declspec(dllexport) void SetOptinResponseCallback(void* callbackPtr) {
        g_optinResponseCallback = reinterpret_cast<OptinResponseCallback>(callbackPtr);
    }

    __declspec(dllexport) int PollNotification(char* typeBuffer, int typeBufferSize, char* dataBuffer, int dataBufferSize) {
        std::lock_guard<std::mutex> lock(g_notificationMutex);
        int hasNotification = 0;
        if (g_notificationQueueCount > 0) {
            Notification* notif = &g_notificationQueue[g_notificationQueueHead];
            strncpy_s(typeBuffer, typeBufferSize, notif->type, _TRUNCATE);
            strncpy_s(dataBuffer, dataBufferSize, notif->data, _TRUNCATE);
            g_notificationQueueHead = (g_notificationQueueHead + 1) % MAX_NOTIFICATIONS;
            g_notificationQueueCount--;
            hasNotification = 1;
        }
        return hasNotification;
    }

    // Message pump function - must be called periodically from Unity
    __declspec(dllexport) void PumpMessages() {
        MSG msg;
        // Process all pending messages
        while (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE)) {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
    }
    
    // Force message pump for a specific window
    __declspec(dllexport) void PumpMessagesForWindow(void* windowPtr) {
        if (windowPtr) {
            HWND hwnd = reinterpret_cast<HWND>(windowPtr);
            MSG msg;
            // Process messages for this specific window
            while (PeekMessage(&msg, hwnd, 0, 0, PM_REMOVE)) {
                TranslateMessage(&msg);
                DispatchMessage(&msg);
            }
        }
    }
    
    // Test function to navigate to a simple URL (for debugging)
    __declspec(dllexport) int TestNavigate(void* windowPtr, const char* testUrl) {
        if (!windowPtr || !testUrl) return 0;
        
        HWND hwnd = reinterpret_cast<HWND>(windowPtr);
        WebViewWindow* window = reinterpret_cast<WebViewWindow*>(GetWindowLongPtr(hwnd, GWLP_USERDATA));
        
        if (window && window->m_webView) {
            std::wstring wurl;
            int len = MultiByteToWideChar(CP_UTF8, 0, testUrl, -1, NULL, 0);
            wurl.resize(len - 1);
            MultiByteToWideChar(CP_UTF8, 0, testUrl, -1, &wurl[0], len);
            
            HRESULT hr = window->m_webView->Navigate(wurl.c_str());
            return SUCCEEDED(hr) ? 1 : 0;
        }
        return 0;
    }
}

