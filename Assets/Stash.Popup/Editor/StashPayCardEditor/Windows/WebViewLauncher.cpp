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

// Simple host object for WebView2 - minimal IDispatch implementation
class SimpleHostObject : public IDispatch {
public:
    SimpleHostObject() : m_refCount(1) {}
    
    // IUnknown
    STDMETHOD(QueryInterface)(REFIID riid, void** ppvObject) {
        if (riid == IID_IUnknown || riid == IID_IDispatch) {
            *ppvObject = this;
            AddRef();
            return S_OK;
        }
        *ppvObject = nullptr;
        return E_NOINTERFACE;
    }
    
    STDMETHOD_(ULONG, AddRef)() {
        return InterlockedIncrement(&m_refCount);
    }
    
    STDMETHOD_(ULONG, Release)() {
        ULONG count = InterlockedDecrement(&m_refCount);
        if (count == 0) {
            delete this;
        }
        return count;
    }
    
    // IDispatch
    STDMETHOD(GetTypeInfoCount)(UINT* pctinfo) {
        *pctinfo = 0;
        return S_OK;
    }
    
    STDMETHOD(GetTypeInfo)(UINT iTInfo, LCID lcid, ITypeInfo** ppTInfo) {
        return E_NOTIMPL;
    }
    
    STDMETHOD(GetIDsOfNames)(REFIID riid, LPOLESTR* rgszNames, UINT cNames, LCID lcid, DISPID* rgDispId) {
        return E_NOTIMPL;
    }
    
    STDMETHOD(Invoke)(DISPID dispIdMember, REFIID riid, LCID lcid, WORD wFlags, DISPPARAMS* pDispParams, VARIANT* pVarResult, EXCEPINFO* pExcepInfo, UINT* puArgErr) {
        // Handle method calls from JavaScript
        // For now, just return success - message handling will be implemented via polling
        return S_OK;
    }

private:
    LONG m_refCount;
};

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
        OutputDebugStringA("[WebView] WebViewWindow destructor called\n");
        
        // Remove event handlers first
        if (m_webView) {
            try {
                if (m_webMessageToken.value != 0) {
                    m_webView->remove_WebMessageReceived(m_webMessageToken);
                    OutputDebugStringA("[WebView] WebMessageReceived handler removed\n");
                }
                if (m_navCompletedToken.value != 0) {
                    m_webView->remove_NavigationCompleted(m_navCompletedToken);
                    OutputDebugStringA("[WebView] NavigationCompleted handler removed\n");
                }
            } catch (...) {
                // Ignore errors during cleanup
            }
        }
        
        // Close and release WebView controller (must be done before WebView)
        if (m_webViewController) {
            OutputDebugStringA("[WebView] Closing WebView controller\n");
            try {
                m_webViewController->Close();
            } catch (...) {
                // Ignore errors
            }
            // Give time for async cleanup
            Sleep(50);
            m_webViewController.Reset(); // Release COM reference
            OutputDebugStringA("[WebView] WebView controller closed and released\n");
        }
        
        // Release WebView
        if (m_webView) {
            OutputDebugStringA("[WebView] Releasing WebView\n");
            m_webView.Reset(); // Release COM reference
            OutputDebugStringA("[WebView] WebView released\n");
        }
        
        OutputDebugStringA("[WebView] WebViewWindow destructor complete\n");
    }

    HRESULT InitializeWebView() {
        OutputDebugStringA("========================================\n");
        OutputDebugStringA("[WebView] ========== STARTING WEBVIEW2 INITIALIZATION ==========\n");
        OutputDebugStringA("========================================\n");
        
        // Get user's AppData\Local path for WebView2 user data folder
        // This avoids "Access is denied" errors when Unity is in Program Files
        wchar_t* localAppData = nullptr;
        size_t len = 0;
        if (_wdupenv_s(&localAppData, &len, L"LOCALAPPDATA") == 0 && localAppData) {
            std::wstring userDataFolder = std::wstring(localAppData) + L"\\Unity\\WebView2";
            free(localAppData);
            
            // Create directory if it doesn't exist
            CreateDirectoryW(userDataFolder.c_str(), NULL);
            
            // Convert to string for debug output
            std::string folderUtf8;
            int utf8Len = WideCharToMultiByte(CP_UTF8, 0, userDataFolder.c_str(), -1, NULL, 0, NULL, NULL);
            if (utf8Len > 0) {
                folderUtf8.resize(utf8Len - 1);
                WideCharToMultiByte(CP_UTF8, 0, userDataFolder.c_str(), -1, &folderUtf8[0], utf8Len, NULL, NULL);
            }
            OutputDebugStringA(("[WebView] Using user data folder: " + folderUtf8 + "\n").c_str());
            
            HRESULT hr = CreateCoreWebView2EnvironmentWithOptions(
                nullptr, userDataFolder.c_str(), nullptr,
                Callback<ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler>(
                [this](HRESULT result, ICoreWebView2Environment* env) -> HRESULT {
                    if (FAILED(result)) {
                        char errorMsg[256];
                        sprintf_s(errorMsg, "[WebView] Failed to create environment: 0x%08X\n", result);
                        OutputDebugStringA(errorMsg);
                        return result;
                    }
                    OutputDebugStringA("[WebView] Environment created, creating controller...\n");

                    return env->CreateCoreWebView2Controller(m_hwnd,
                        Callback<ICoreWebView2CreateCoreWebView2ControllerCompletedHandler>(
                            [this](HRESULT result, ICoreWebView2Controller* controller) -> HRESULT {
                                if (FAILED(result)) {
                                    char errorMsg[256];
                                    sprintf_s(errorMsg, "[WebView] Failed to create controller: 0x%08X\n", result);
                                    OutputDebugStringA(errorMsg);
                                    return result;
                                }
                                
                                OutputDebugStringA("[WebView] Controller created successfully\n");
                                m_webViewController = controller;
                                m_webViewController->get_CoreWebView2(&m_webView);
                                
                                if (!m_webView) {
                                    OutputDebugStringA("[WebView] ERROR: Failed to get CoreWebView2 from controller!\n");
                                    return E_FAIL;
                                }
                                
                                OutputDebugStringA("========================================\n");
                                OutputDebugStringA("[WebView] ✓✓✓ CoreWebView2 obtained successfully ✓✓✓\n");
                                OutputDebugStringA("========================================\n");
                                
                                // Set up WebMessageReceived handler FIRST - this automatically enables window.chrome.webview.postMessage()
                                // This is the WebView2 equivalent of macOS messageHandlers
                                // IMPORTANT: add_WebMessageReceived must be called BEFORE injecting JavaScript
                                // so that window.chrome.webview.postMessage is available when the script runs
                                OutputDebugStringA("[WebView] Registering WebMessageReceived handler...\n");
                                HRESULT msgResult = m_webView->add_WebMessageReceived(
                                    Callback<ICoreWebView2WebMessageReceivedEventHandler>(
                                        [this](ICoreWebView2* sender, ICoreWebView2WebMessageReceivedEventArgs* args) -> HRESULT {
                                            LPWSTR message = nullptr;
                                            HRESULT hr = args->TryGetWebMessageAsString(&message);
                                            if (SUCCEEDED(hr) && message) {
                                                char debugMsg[512];
                                                int len = WideCharToMultiByte(CP_UTF8, 0, message, -1, NULL, 0, NULL, NULL);
                                                if (len > 0 && len < 512) {
                                                    WideCharToMultiByte(CP_UTF8, 0, message, -1, debugMsg, len, NULL, NULL);
                                                    OutputDebugStringA("[WebView] ========== MESSAGE RECEIVED FROM JAVASCRIPT ==========\n");
                                                    OutputDebugStringA(debugMsg);
                                                    OutputDebugStringA("\n");
                                                    OutputDebugStringA("[WebView] ====================================================\n");
                                                } else {
                                                    OutputDebugStringA("[WebView] ========== MESSAGE RECEIVED FROM JAVASCRIPT (too long) ==========\n");
                                                }
                                                this->HandleWebMessage(message);
                                                CoTaskMemFree(message);
                                            } else {
                                                char errorMsg[256];
                                                sprintf_s(errorMsg, "[WebView] ✗ Failed to get message from JavaScript: HRESULT=0x%08X\n", hr);
                                                OutputDebugStringA(errorMsg);
                                            }
                                            return S_OK;
                                        }).Get(), &m_webMessageToken);
                                
                                if (SUCCEEDED(msgResult)) {
                                    OutputDebugStringA("[WebView] ✓✓✓ WebMessageReceived handler registered successfully ✓✓✓\n");
                                    OutputDebugStringA("[WebView] ✓✓✓ window.chrome.webview.postMessage() should now be available ✓✓✓\n");
                                } else {
                                    char errorMsg[256];
                                    sprintf_s(errorMsg, "[WebView] ✗✗✗ FAILED to register WebMessageReceived: 0x%08X ✗✗✗\n", msgResult);
                                    OutputDebugStringA(errorMsg);
                                }

                                // NOW inject JavaScript - window.chrome.webview.postMessage() is now available
                                // Inject JavaScript - WebView2 message handling
                                // Use window.chrome.webview.postMessage() to send messages to native code
                                // This matches the macOS implementation pattern exactly
                                const char* stashSDKScript = R"(
                                    (function() {
                                        console.log('[stash_sdk] Initializing stash_sdk for WebView2...');
                                        window.stash_sdk = window.stash_sdk || {};
                                        var postMessage = function(message) {
                                            try {
                                                // Use chrome.webview.postMessage to send to native code
                                                // This is available after AddWebMessageReceived is called
                                                if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
                                                    console.log('[stash_sdk] Posting message:', message);
                                                    window.chrome.webview.postMessage(message);
                                                } else {
                                                    console.error('[stash_sdk] ERROR: window.chrome.webview.postMessage not available!');
                                                    console.error('[stash_sdk] window.chrome:', window.chrome);
                                                    console.error('[stash_sdk] window.chrome.webview:', window.chrome ? window.chrome.webview : 'undefined');
                                                }
                                            } catch(e) {
                                                console.error('[stash_sdk] Error posting message:', e);
                                            }
                                        };
                                        window.stash_sdk.onPaymentSuccess = function(data) {
                                            console.log('[stash_sdk] onPaymentSuccess called with data:', data);
                                            postMessage(JSON.stringify({type: 'stashPaymentSuccess', data: data || {}}));
                                        };
                                        window.stash_sdk.onPaymentFailure = function(data) {
                                            console.log('[stash_sdk] onPaymentFailure called with data:', data);
                                            postMessage(JSON.stringify({type: 'stashPaymentFailure', data: data || {}}));
                                        };
                                        window.stash_sdk.onPurchaseProcessing = function(data) {
                                            console.log('[stash_sdk] onPurchaseProcessing called with data:', data);
                                            postMessage(JSON.stringify({type: 'stashPurchaseProcessing', data: data || {}}));
                                        };
                                        window.stash_sdk.setPaymentChannel = function(optinType) {
                                            console.log('[stash_sdk] setPaymentChannel called with optinType:', optinType);
                                            postMessage(JSON.stringify({type: 'stashOptin', data: optinType || ''}));
                                        };
                                        // Test function to verify connection works
                                        window.stash_sdk.testConnection = function() {
                                            console.log('[stash_sdk] testConnection called - testing postMessage...');
                                            postMessage(JSON.stringify({type: 'stashPaymentSuccess', data: {test: true}}));
                                        };
                                        console.log('[stash_sdk] ✓ stash_sdk initialized successfully');
                                        console.log('[stash_sdk] Available functions: onPaymentSuccess, onPaymentFailure, onPurchaseProcessing, setPaymentChannel, testConnection');
                                        // Log if chrome.webview is available
                                        if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
                                            console.log('[stash_sdk] ✓ window.chrome.webview.postMessage is available');
                                        } else {
                                            console.error('[stash_sdk] ✗ window.chrome.webview.postMessage is NOT available!');
                                            console.error('[stash_sdk] window.chrome:', window.chrome);
                                        }
                                    })();
                                )";

                                std::wstring script;
                                int len = MultiByteToWideChar(CP_UTF8, 0, stashSDKScript, -1, NULL, 0);
                                script.resize(len - 1);
                                MultiByteToWideChar(CP_UTF8, 0, stashSDKScript, -1, &script[0], len);

                                m_webView->AddScriptToExecuteOnDocumentCreated(script.c_str(), 
                                    Callback<ICoreWebView2AddScriptToExecuteOnDocumentCreatedCompletedHandler>(
                                        [this](HRESULT errorCode, LPCWSTR id) -> HRESULT {
                                            if (SUCCEEDED(errorCode)) {
                                                OutputDebugStringA("[WebView] ✓ stash_sdk JavaScript injected successfully\n");
                                                // NOTE: Removed automatic testConnection call to prevent false callbacks
                                                // The testConnection function is still available for manual testing if needed
                                            } else {
                                                char errorMsg[256];
                                                sprintf_s(errorMsg, "[WebView] ✗ Failed to inject stash_sdk JavaScript: 0x%08X\n", errorCode);
                                                OutputDebugStringA(errorMsg);
                                            }
                                            return S_OK;
                                        }).Get());

                                // Resize WebView to fill window
                                RECT bounds;
                                GetClientRect(m_hwnd, &bounds);
                                // Ensure bounds are valid
                                if (bounds.right > bounds.left && bounds.bottom > bounds.top) {
                                    m_webViewController->put_Bounds(bounds);
                                    char boundsMsg[256];
                                    sprintf_s(boundsMsg, "[WebView] Set bounds: %d x %d\n", bounds.right - bounds.left, bounds.bottom - bounds.top);
                                    OutputDebugStringA(boundsMsg);
                                } else {
                                    // Set default size if invalid
                                    bounds.left = 0;
                                    bounds.top = 0;
                                    bounds.right = 800;
                                    bounds.bottom = 600;
                                    m_webViewController->put_Bounds(bounds);
                                    OutputDebugStringA("[WebView] Set default bounds: 800 x 600\n");
                                }
                                
                                // Make sure WebView is visible
                                m_webViewController->put_IsVisible(TRUE);
                                OutputDebugStringA("[WebView] WebView set to visible\n");
                                
                                // Ensure WebView gets focus
                                BOOL isVisible = FALSE;
                                m_webViewController->get_IsVisible(&isVisible);
                                if (!isVisible) {
                                    OutputDebugStringA("[WebView] WARNING: WebView is not visible!\n");
                                }
                                
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
                                                OutputDebugStringA("[WebView] ========== NAVIGATION COMPLETED ==========\n");
                                                OutputDebugStringA("[WebView] ✓ Navigation completed successfully\n");
                                                // Force a repaint after navigation
                                                InvalidateRect(m_hwnd, NULL, TRUE);
                                                UpdateWindow(m_hwnd);
                                                SetForegroundWindow(m_hwnd);
                                                SetFocus(m_hwnd);
                                                
                                                OutputDebugStringA("[WebView] Re-injecting stash_sdk script after navigation...\n");
                                                // Re-inject stash_sdk script after navigation to ensure it's available
                                                // This is important because the page might have loaded and our script might have been overridden
                                                const char* stashSDKScript = R"(
                                                    (function() {
                                                        console.log('[stash_sdk] Re-initializing stash_sdk after navigation...');
                                                        window.stash_sdk = window.stash_sdk || {};
                                                        var postMessage = function(message) {
                                                            try {
                                                                if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
                                                                    console.log('[stash_sdk] Posting message:', message);
                                                                    window.chrome.webview.postMessage(message);
                                                                } else {
                                                                    console.error('[stash_sdk] window.chrome.webview.postMessage not available');
                                                                }
                                                            } catch(e) {
                                                                console.error('[stash_sdk] Error posting message:', e);
                                                            }
                                                        };
                                                        window.stash_sdk.onPaymentSuccess = function(data) {
                                                            console.log('[stash_sdk] onPaymentSuccess called with data:', data);
                                                            postMessage(JSON.stringify({type: 'stashPaymentSuccess', data: data || {}}));
                                                        };
                                                        window.stash_sdk.onPaymentFailure = function(data) {
                                                            console.log('[stash_sdk] onPaymentFailure called with data:', data);
                                                            postMessage(JSON.stringify({type: 'stashPaymentFailure', data: data || {}}));
                                                        };
                                                        window.stash_sdk.onPurchaseProcessing = function(data) {
                                                            console.log('[stash_sdk] onPurchaseProcessing called with data:', data);
                                                            postMessage(JSON.stringify({type: 'stashPurchaseProcessing', data: data || {}}));
                                                        };
                                                        window.stash_sdk.setPaymentChannel = function(optinType) {
                                                            console.log('[stash_sdk] setPaymentChannel called with optinType:', optinType);
                                                            postMessage(JSON.stringify({type: 'stashOptin', data: optinType || ''}));
                                                        };
                                                        window.stash_sdk.testConnection = function() {
                                                            console.log('[stash_sdk] testConnection called - testing postMessage...');
                                                            postMessage(JSON.stringify({type: 'stashPaymentSuccess', data: {test: true}}));
                                                        };
                                                        console.log('[stash_sdk] ✓ stash_sdk re-initialized after navigation');
                                                        if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
                                                            console.log('[stash_sdk] ✓ window.chrome.webview.postMessage is available');
                                                        } else {
                                                            console.error('[stash_sdk] ✗ window.chrome.webview.postMessage is NOT available!');
                                                        }
                                                    })();
                                                )";
                                                std::wstring script;
                                                int scriptLen = MultiByteToWideChar(CP_UTF8, 0, stashSDKScript, -1, NULL, 0);
                                                script.resize(scriptLen - 1);
                                                MultiByteToWideChar(CP_UTF8, 0, stashSDKScript, -1, &script[0], scriptLen);
                                                
                                                m_webView->ExecuteScript(script.c_str(),
                                                    Callback<ICoreWebView2ExecuteScriptCompletedHandler>(
                                                        [](HRESULT errorCode, LPCWSTR resultObjectAsJson) -> HRESULT {
                                                            if (SUCCEEDED(errorCode)) {
                                                                OutputDebugStringA("[WebView] ✓ stash_sdk script re-injected after navigation\n");
                                                            } else {
                                                                char errorMsg[256];
                                                                sprintf_s(errorMsg, "[WebView] ✗ Failed to re-inject stash_sdk script: 0x%08X\n", errorCode);
                                                                OutputDebugStringA(errorMsg);
                                                            }
                                                            return S_OK;
                                                        }).Get());
                                                
                                                // Also try to get the document title to verify page loaded
                                                m_webView->ExecuteScript(L"document.title", 
                                                    Callback<ICoreWebView2ExecuteScriptCompletedHandler>(
                                                        [](HRESULT errorCode, LPCWSTR resultObjectAsJson) -> HRESULT {
                                                            if (SUCCEEDED(errorCode) && resultObjectAsJson) {
                                                                std::string title;
                                                                int len = WideCharToMultiByte(CP_UTF8, 0, resultObjectAsJson, -1, NULL, 0, NULL, NULL);
                                                                if (len > 0) {
                                                                    title.resize(len - 1);
                                                                    WideCharToMultiByte(CP_UTF8, 0, resultObjectAsJson, -1, &title[0], len, NULL, NULL);
                                                                    OutputDebugStringA(("[WebView] Page title: " + title + "\n").c_str());
                                                                }
                                                            }
                                                            return S_OK;
                                                        }).Get());
                                                
                                                // CRITICAL: Verify stash_sdk is available and chrome.webview.postMessage works
                                                // NOTE: We removed the automatic testConnection call to prevent false callbacks
                                                m_webView->ExecuteScript(L"(function() { var results = []; if (typeof window.stash_sdk !== 'undefined') { results.push('stash_sdk: AVAILABLE'); if (window.stash_sdk.testConnection) { results.push('testConnection: AVAILABLE (but not calling to avoid false callbacks)'); } else { results.push('testConnection: NOT AVAILABLE'); } } else { results.push('stash_sdk: NOT AVAILABLE'); } if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) { results.push('chrome.webview.postMessage: AVAILABLE'); } else { results.push('chrome.webview.postMessage: NOT AVAILABLE'); } return results.join(' | '); })()",
                                                    Callback<ICoreWebView2ExecuteScriptCompletedHandler>(
                                                        [](HRESULT errorCode, LPCWSTR resultObjectAsJson) -> HRESULT {
                                                            if (SUCCEEDED(errorCode) && resultObjectAsJson) {
                                                                std::string result;
                                                                int len = WideCharToMultiByte(CP_UTF8, 0, resultObjectAsJson, -1, NULL, 0, NULL, NULL);
                                                                if (len > 0) {
                                                                    result.resize(len - 1);
                                                                    WideCharToMultiByte(CP_UTF8, 0, resultObjectAsJson, -1, &result[0], len, NULL, NULL);
                                                                    OutputDebugStringA("========================================\n");
                                                                    OutputDebugStringA(("[WebView] CRITICAL VERIFICATION: " + result + "\n").c_str());
                                                                    OutputDebugStringA("========================================\n");
                                                                }
                                                            } else {
                                                                OutputDebugStringA("[WebView] ✗✗✗ FAILED to verify stash_sdk availability ✗✗✗\n");
                                                            }
                                                            return S_OK;
                                                        }).Get());
                                            } else {
                                                char errorMsg[256];
                                                sprintf_s(errorMsg, "[WebView] ✗ Navigation failed with error status: %d\n", errorStatus);
                                                OutputDebugStringA(errorMsg);
                                            }
                                            return S_OK;
                                        }).Get(), &m_navCompletedToken);
                                
                                // Navigate to URL after WebView is ready
                                // Try navigating immediately first, then use PostMessage as fallback
                                if (!m_url.empty()) {
                                    // Convert wstring to string for debug output
                                    std::string urlUtf8;
                                    int len = WideCharToMultiByte(CP_UTF8, 0, m_url.c_str(), -1, NULL, 0, NULL, NULL);
                                    if (len > 0) {
                                        urlUtf8.resize(len - 1);
                                        WideCharToMultiByte(CP_UTF8, 0, m_url.c_str(), -1, &urlUtf8[0], len, NULL, NULL);
                                    }
                                    OutputDebugStringA(("[WebView] URL to navigate: " + urlUtf8 + "\n").c_str());
                                    
                                    // Try navigating immediately
                                    HRESULT immediateNav = m_webView->Navigate(m_url.c_str());
                                    if (FAILED(immediateNav)) {
                                        OutputDebugStringA("[WebView] Immediate navigation failed, will retry via message\n");
                                        // Post message to retry navigation after a delay
                                        Sleep(200);
                                        PostMessage(m_hwnd, WM_USER + 1, 0, 0);
                                    } else {
                                        OutputDebugStringA("[WebView] Immediate navigation initiated\n");
                                    }
                                } else {
                                    OutputDebugStringA("[WebView] ✗ WARNING: URL is empty!\n");
                                }

                                return S_OK;
                            }).Get());
                }).Get());
            
            return hr;
        } else {
            // Fallback: LOCALAPPDATA should always be available, but if not, use temp directory
            std::wstring fallbackFolder = std::wstring(L"C:\\Temp\\Unity\\WebView2");
            CreateDirectoryW(fallbackFolder.c_str(), NULL);
            OutputDebugStringA("[WebView] WARNING: Could not get LOCALAPPDATA, using fallback temp directory\n");
            // For now, just return error - LOCALAPPDATA should always be available on Windows
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

        OutputDebugStringA(("[WebView] Handling message: " + msgUtf8 + "\n").c_str());

        // Parse JSON message - look for type field
        // Expected format: {"type":"stashPaymentSuccess","data":{...}}
        // Also handle: {"type":"stashPaymentSuccess","data":"..."} for optin
        
        // Check for payment success (including test messages)
        if (msgUtf8.find("\"type\":\"stashPaymentSuccess\"") != std::string::npos ||
            msgUtf8.find("\"type\": 'stashPaymentSuccess'") != std::string::npos ||
            msgUtf8.find("'type':'stashPaymentSuccess'") != std::string::npos ||
            msgUtf8.find("'type':\"stashPaymentSuccess\"") != std::string::npos) {
            // Check if this is a test message - if so, ignore it and don't call callbacks
            bool isTest = msgUtf8.find("\"test\":true") != std::string::npos || 
                         msgUtf8.find("'test':true") != std::string::npos ||
                         msgUtf8.find("\"test\": true") != std::string::npos ||
                         msgUtf8.find("'test': true") != std::string::npos;
            if (isTest) {
                OutputDebugStringA("[WebView] ✓✓✓ TEST MESSAGE received - connection is working! (ignoring, not calling callbacks) ✓✓✓\n");
                return; // Don't call callbacks for test messages
            }
            
            // This is a real payment success message
            OutputDebugStringA("[WebView] ✓ Payment success detected (REAL MESSAGE)\n");
            if (g_paymentSuccessCallback) {
                OutputDebugStringA("[WebView] → Calling payment success callback\n");
                g_paymentSuccessCallback();
            } else {
                OutputDebugStringA("[WebView] ⚠ Callback not set, queuing notification\n");
                QueueNotification("StashPaymentSuccess", "");
            }
            return;
        }
        
        // Check for payment failure
        if (msgUtf8.find("\"type\":\"stashPaymentFailure\"") != std::string::npos ||
            msgUtf8.find("\"type\": 'stashPaymentFailure'") != std::string::npos ||
            msgUtf8.find("'type':'stashPaymentFailure'") != std::string::npos ||
            msgUtf8.find("'type':\"stashPaymentFailure\"") != std::string::npos) {
            OutputDebugStringA("[WebView] ✓ Payment failure detected\n");
            if (g_paymentFailureCallback) {
                OutputDebugStringA("[WebView] → Calling payment failure callback\n");
                g_paymentFailureCallback();
            } else {
                OutputDebugStringA("[WebView] ⚠ Callback not set, queuing notification\n");
                QueueNotification("StashPaymentFailure", "");
            }
            return;
        }
        
        // Check for purchase processing
        if (msgUtf8.find("\"type\":\"stashPurchaseProcessing\"") != std::string::npos ||
            msgUtf8.find("\"type\": 'stashPurchaseProcessing'") != std::string::npos ||
            msgUtf8.find("'type':'stashPurchaseProcessing'") != std::string::npos ||
            msgUtf8.find("'type':\"stashPurchaseProcessing\"") != std::string::npos) {
            OutputDebugStringA("[WebView] ✓ Purchase processing detected\n");
            if (g_purchaseProcessingCallback) {
                OutputDebugStringA("[WebView] → Calling purchase processing callback\n");
                g_purchaseProcessingCallback();
            } else {
                OutputDebugStringA("[WebView] ⚠ Callback not set, queuing notification\n");
                QueueNotification("StashPurchaseProcessing", "");
            }
            return;
        }
        
        // Check for optin
        if (msgUtf8.find("\"type\":\"stashOptin\"") != std::string::npos ||
            msgUtf8.find("\"type\": 'stashOptin'") != std::string::npos ||
            msgUtf8.find("'type':'stashOptin'") != std::string::npos ||
            msgUtf8.find("'type':\"stashOptin\"") != std::string::npos) {
            OutputDebugStringA("[WebView] ✓ Optin detected\n");
            // Extract optin type from data field
            std::string optinType = "";
            
            // Try to find "data" field - handle both "data":"value" and "data": "value"
            size_t dataPos = msgUtf8.find("\"data\":");
            if (dataPos == std::string::npos) {
                dataPos = msgUtf8.find("'data':");
            }
            
            if (dataPos != std::string::npos) {
                // Skip past "data": or 'data':
                size_t valueStart = dataPos + 7; // Length of "data":
                // Skip whitespace and colon
                while (valueStart < msgUtf8.length() && 
                       (msgUtf8[valueStart] == ' ' || msgUtf8[valueStart] == '\t' || msgUtf8[valueStart] == ':')) {
                    valueStart++;
                }
                
                // Check if value is a quoted string
                if (valueStart < msgUtf8.length() && (msgUtf8[valueStart] == '"' || msgUtf8[valueStart] == '\'')) {
                    char quote = msgUtf8[valueStart];
                    valueStart++; // Skip opening quote
                    size_t valueEnd = valueStart;
                    while (valueEnd < msgUtf8.length() && msgUtf8[valueEnd] != quote) {
                        valueEnd++;
                    }
                    if (valueEnd > valueStart) {
                        optinType = msgUtf8.substr(valueStart, valueEnd - valueStart);
                    }
                } else {
                    // Value is not quoted - extract until comma, }, or space
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
                        // Remove any remaining quotes
                        if (optinType.length() >= 2 && 
                            ((optinType[0] == '"' && optinType[optinType.length()-1] == '"') ||
                             (optinType[0] == '\'' && optinType[optinType.length()-1] == '\''))) {
                            optinType = optinType.substr(1, optinType.length() - 2);
                        }
                    }
                }
            }
            
            OutputDebugStringA(("[WebView] → Optin type extracted: '" + optinType + "'\n").c_str());
            if (g_optinResponseCallback) {
                OutputDebugStringA("[WebView] → Calling optin callback\n");
                g_optinResponseCallback(optinType.c_str());
            } else {
                OutputDebugStringA("[WebView] ⚠ Callback not set, queuing notification\n");
                QueueNotification("StashOptinResponse", optinType);
            }
            return;
        }
        
        // Check for test messages - handle these FIRST and ignore them
        if (msgUtf8.find("\"type\":\"test\"") != std::string::npos ||
            msgUtf8.find("\"message\":\"IMMEDIATE_TEST_MESSAGE\"") != std::string::npos) {
            OutputDebugStringA("[WebView] ========== IMMEDIATE TEST MESSAGE RECEIVED ==========\n");
            OutputDebugStringA("[WebView] ✓✓✓ CONNECTION IS WORKING! postMessage is functional! ✓✓✓\n");
            OutputDebugStringA("[WebView] (Ignoring test message, not calling callbacks)\n");
            OutputDebugStringA("[WebView] ====================================================\n");
            return; // Don't process test messages further
        }
        
        // Unknown message type
        OutputDebugStringA(("[WebView] ✗ Unknown message type. Full message: " + msgUtf8 + "\n").c_str());
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
        OutputDebugStringA("[WebView] WM_DESTROY received\n");
        if (window) {
            // Clean up window object
            SetWindowLongPtr(hwnd, GWLP_USERDATA, 0);
            delete window;
            window = nullptr;
            OutputDebugStringA("[WebView] Window object deleted\n");
        }
        // Don't call PostQuitMessage here - it terminates the entire message loop
        // Only call it if this is the last window (we don't track that, so skip it)
        return 0;
    case WM_SIZE:
        if (window && window->m_webViewController) {
            RECT bounds;
            GetClientRect(hwnd, &bounds);
            window->m_webViewController->put_Bounds(bounds);
            OutputDebugStringA("[WebView] Window resized\n");
        }
        return 0;
    case WM_PAINT:
        // Ensure window is painted
        if (window && window->m_webViewController) {
            PAINTSTRUCT ps;
            BeginPaint(hwnd, &ps);
            EndPaint(hwnd, &ps);
        } else {
            // Default paint handling
            PAINTSTRUCT ps;
            BeginPaint(hwnd, &ps);
            EndPaint(hwnd, &ps);
        }
        return 0;
    case WM_CLOSE:
        OutputDebugStringA("[WebView] Window closing\n");
        DestroyWindow(hwnd);
        return 0;
    case WM_USER + 1:
        // Custom message to trigger navigation after WebView initialization
        if (window && window->m_webView && !window->m_url.empty()) {
            OutputDebugStringA("[WebView] WM_USER+1: Attempting navigation...\n");
            
            // Ensure window has focus
            SetForegroundWindow(hwnd);
            SetFocus(hwnd);
            
            // Convert URL to wide string for logging
            std::string urlUtf8;
            int len = WideCharToMultiByte(CP_UTF8, 0, window->m_url.c_str(), -1, NULL, 0, NULL, NULL);
            if (len > 0) {
                urlUtf8.resize(len - 1);
                WideCharToMultiByte(CP_UTF8, 0, window->m_url.c_str(), -1, &urlUtf8[0], len, NULL, NULL);
            }
            OutputDebugStringA(("[WebView] Navigating to: " + urlUtf8 + "\n").c_str());
            
            // Check if WebView is ready by trying to get a property
            BOOL isVisible = FALSE;
            if (window->m_webViewController) {
                window->m_webViewController->get_IsVisible(&isVisible);
                if (!isVisible) {
                    OutputDebugStringA("[WebView] WARNING: WebView not visible before navigation!\n");
                    window->m_webViewController->put_IsVisible(TRUE);
                }
            }
            
            HRESULT navResult = window->m_webView->Navigate(window->m_url.c_str());
            if (FAILED(navResult)) {
                char errorMsg[256];
                sprintf_s(errorMsg, "[WebView] ✗ Navigate() failed: 0x%08X\n", navResult);
                OutputDebugStringA(errorMsg);
                
                // Retry mechanism with static counter per window
                static std::map<HWND, int> retryCounts;
                int& retryCount = retryCounts[hwnd];
                if (retryCount < 5) {
                    retryCount++;
                    char retryMsg[128];
                    sprintf_s(retryMsg, "[WebView] Retry %d/5 in 200ms...\n", retryCount);
                    OutputDebugStringA(retryMsg);
                    Sleep(200);
                    PostMessage(hwnd, WM_USER + 1, 0, 0);
                } else {
                    retryCount = 0;
                    retryCounts.erase(hwnd);
                    OutputDebugStringA("[WebView] ✗ Navigation retry limit reached\n");
                }
            } else {
                OutputDebugStringA("[WebView] ✓ Navigate() called successfully\n");
                // Force repaint after navigation
                InvalidateRect(hwnd, NULL, TRUE);
                UpdateWindow(hwnd);
                // Clear retry count on success
                static std::map<HWND, int> retryCounts;
                retryCounts.erase(hwnd);
            }
        } else {
            if (!window) OutputDebugStringA("[WebView] WM_USER+1: Window is null\n");
            else if (!window->m_webView) OutputDebugStringA("[WebView] WM_USER+1: WebView is null\n");
            else if (window->m_url.empty()) OutputDebugStringA("[WebView] WM_USER+1: URL is empty\n");
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
        SetForegroundWindow(hwnd); // Bring to front
        SetFocus(hwnd); // Give focus
        UpdateWindow(hwnd);
        InvalidateRect(hwnd, NULL, TRUE); // Force initial paint
        OutputDebugStringA("[WebView] Window created and shown\n");

        // Process messages to allow WebView to initialize
        // Pump messages multiple times to ensure async initialization completes
        // WebView2 initialization is asynchronous and needs message pumping
        OutputDebugStringA("[WebView] Pumping messages for initialization...\n");
        for (int i = 0; i < 30; i++) {
            MSG msg;
            while (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE)) {
                TranslateMessage(&msg);
                DispatchMessage(&msg);
            }
            if (i < 29) { // Don't sleep on last iteration
                Sleep(30); // Give time for async WebView2 initialization
            }
        }
        OutputDebugStringA("[WebView] Initial message pumping complete\n");

        return hwnd;
    }

    __declspec(dllexport) void DestroyWebViewWindow(void* windowPtr) {
        if (!windowPtr) {
            OutputDebugStringA("[WebView] DestroyWebViewWindow: windowPtr is null\n");
            return;
        }
        
        HWND hwnd = reinterpret_cast<HWND>(windowPtr);
        OutputDebugStringA("[WebView] DestroyWebViewWindow called\n");
        
        // Check if window is still valid
        if (!IsWindow(hwnd)) {
            OutputDebugStringA("[WebView] Window handle is invalid, may already be destroyed\n");
            return;
        }
        
        // Get window object and clean it up properly
        WebViewWindow* window = reinterpret_cast<WebViewWindow*>(GetWindowLongPtr(hwnd, GWLP_USERDATA));
        if (window) {
            OutputDebugStringA("[WebView] Cleaning up window object\n");
            // Clear the window pointer first to prevent re-entry
            SetWindowLongPtr(hwnd, GWLP_USERDATA, 0);
            
            // Delete the window object (destructor will clean up WebView2 objects)
            delete window;
            window = nullptr;
            OutputDebugStringA("[WebView] Window object deleted\n");
        }
        
        // Destroy the window (this will send WM_DESTROY)
        if (IsWindow(hwnd)) {
            OutputDebugStringA("[WebView] Destroying window\n");
            // Send WM_CLOSE first to allow graceful shutdown
            SendMessage(hwnd, WM_CLOSE, 0, 0);
            Sleep(50); // Give time for WM_CLOSE to process
            DestroyWindow(hwnd);
        }
        
        // Process remaining messages to ensure cleanup completes
        // This is critical for WebView2 to release all resources and unlock the DLL
        OutputDebugStringA("[WebView] Processing remaining messages for cleanup\n");
        for (int i = 0; i < 30; i++) {
            MSG msg;
            // Process messages for this specific window
            while (PeekMessage(&msg, hwnd, 0, 0, PM_REMOVE)) {
                TranslateMessage(&msg);
                DispatchMessage(&msg);
            }
            // Process general messages (but be careful not to steal from other windows)
            while (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE | PM_NOYIELD)) {
                if (msg.hwnd == hwnd || msg.hwnd == NULL) {
                    TranslateMessage(&msg);
                    DispatchMessage(&msg);
                } else {
                    // Put it back - it's for another window
                    PostMessage(msg.hwnd, msg.message, msg.wParam, msg.lParam);
                    break;
                }
            }
            if (i < 29) {
                Sleep(30); // Give more time for async cleanup
            }
        }
        
        OutputDebugStringA("[WebView] DestroyWebViewWindow complete - DLL should be unlocked now\n");
    }

    __declspec(dllexport) void SetPaymentSuccessCallback(void* callbackPtr) {
        char msg[256];
        sprintf_s(msg, "[WebView] SetPaymentSuccessCallback: callbackPtr=%p, setting global callback\n", callbackPtr);
        OutputDebugStringA(msg);
        g_paymentSuccessCallback = reinterpret_cast<PaymentSuccessCallback>(callbackPtr);
        if (g_paymentSuccessCallback) {
            OutputDebugStringA("[WebView] ✓ PaymentSuccessCallback is now set and ready\n");
        } else {
            OutputDebugStringA("[WebView] ✗ PaymentSuccessCallback is NULL!\n");
        }
    }

    __declspec(dllexport) void SetPaymentFailureCallback(void* callbackPtr) {
        char msg[256];
        sprintf_s(msg, "[WebView] SetPaymentFailureCallback: callbackPtr=%p, setting global callback\n", callbackPtr);
        OutputDebugStringA(msg);
        g_paymentFailureCallback = reinterpret_cast<PaymentFailureCallback>(callbackPtr);
        if (g_paymentFailureCallback) {
            OutputDebugStringA("[WebView] ✓ PaymentFailureCallback is now set and ready\n");
        } else {
            OutputDebugStringA("[WebView] ✗ PaymentFailureCallback is NULL!\n");
        }
    }

    __declspec(dllexport) void SetPurchaseProcessingCallback(void* callbackPtr) {
        char msg[256];
        sprintf_s(msg, "[WebView] SetPurchaseProcessingCallback: callbackPtr=%p, setting global callback\n", callbackPtr);
        OutputDebugStringA(msg);
        g_purchaseProcessingCallback = reinterpret_cast<PurchaseProcessingCallback>(callbackPtr);
        if (g_purchaseProcessingCallback) {
            OutputDebugStringA("[WebView] ✓ PurchaseProcessingCallback is now set and ready\n");
        } else {
            OutputDebugStringA("[WebView] ✗ PurchaseProcessingCallback is NULL!\n");
        }
    }

    __declspec(dllexport) void SetOptinResponseCallback(void* callbackPtr) {
        char msg[256];
        sprintf_s(msg, "[WebView] SetOptinResponseCallback: callbackPtr=%p, setting global callback\n", callbackPtr);
        OutputDebugStringA(msg);
        g_optinResponseCallback = reinterpret_cast<OptinResponseCallback>(callbackPtr);
        if (g_optinResponseCallback) {
            OutputDebugStringA("[WebView] ✓ OptinResponseCallback is now set and ready\n");
        } else {
            OutputDebugStringA("[WebView] ✗ OptinResponseCallback is NULL!\n");
        }
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

