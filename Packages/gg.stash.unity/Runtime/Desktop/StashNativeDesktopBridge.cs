using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Stash.Native.Desktop
{
    /// <summary>Config JSON for the desktop hosts. Mobile field names plus the desktop-only keys; see stash-native docs/windows.md.</summary>
    [Serializable]
    internal class StashDesktopCardConfigDto
    {
        public bool forcePortrait;
        public float cardHeightRatioPortrait;
        public float cardWidthRatioLandscape;
        public float cardHeightRatioLandscape;
        public float tabletWidthRatioPortrait;
        public float tabletHeightRatioPortrait;
        public float tabletWidthRatioLandscape;
        public float tabletHeightRatioLandscape;
        public bool autoClose;
        public string backgroundColor;
        public string presentation;
        public float width;
        public float height;
        public bool allowFileUrls;

        public static StashDesktopCardConfigDto From(StashNativeCardConfig c, string presentation, Vector2 size, bool allowFileUrls)
        {
            return new StashDesktopCardConfigDto
            {
                forcePortrait = c.forcePortrait,
                cardHeightRatioPortrait = c.cardHeightRatioPortrait,
                cardWidthRatioLandscape = c.cardWidthRatioLandscape,
                cardHeightRatioLandscape = c.cardHeightRatioLandscape,
                tabletWidthRatioPortrait = c.tabletWidthRatioPortrait,
                tabletHeightRatioPortrait = c.tabletHeightRatioPortrait,
                tabletWidthRatioLandscape = c.tabletWidthRatioLandscape,
                tabletHeightRatioLandscape = c.tabletHeightRatioLandscape,
                autoClose = c.autoClose,
                backgroundColor = c.backgroundColor ?? "",
                presentation = presentation,
                width = size.x,
                height = size.y,
                allowFileUrls = allowFileUrls
            };
        }
    }

    [Serializable]
    internal class StashDesktopModalConfigDto
    {
        public bool allowDismiss;
        public float phoneWidthRatioPortrait;
        public float phoneHeightRatioPortrait;
        public float phoneWidthRatioLandscape;
        public float phoneHeightRatioLandscape;
        public float tabletWidthRatioPortrait;
        public float tabletHeightRatioPortrait;
        public float tabletWidthRatioLandscape;
        public float tabletHeightRatioLandscape;
        public bool autoClose;
        public string backgroundColor;
        public string presentation;
        public float width;
        public float height;
        public bool allowFileUrls;

        public static StashDesktopModalConfigDto From(StashNativeModalConfig c, string presentation, Vector2 size, bool allowFileUrls)
        {
            return new StashDesktopModalConfigDto
            {
                allowDismiss = c.allowDismiss,
                phoneWidthRatioPortrait = c.phoneWidthRatioPortrait,
                phoneHeightRatioPortrait = c.phoneHeightRatioPortrait,
                phoneWidthRatioLandscape = c.phoneWidthRatioLandscape,
                phoneHeightRatioLandscape = c.phoneHeightRatioLandscape,
                tabletWidthRatioPortrait = c.tabletWidthRatioPortrait,
                tabletHeightRatioPortrait = c.tabletHeightRatioPortrait,
                tabletWidthRatioLandscape = c.tabletWidthRatioLandscape,
                tabletHeightRatioLandscape = c.tabletHeightRatioLandscape,
                autoClose = c.autoClose,
                backgroundColor = c.backgroundColor ?? "",
                presentation = presentation,
                width = size.x,
                height = size.y,
                allowFileUrls = allowFileUrls
            };
        }
    }

    /// <summary>
    /// Binding to the stash-native desktop hosts (StashNativeDesktop.dll on Windows, StashNativeDesktop.bundle on
    /// macOS, one C ABI). Native events are enqueued from the callback and drained on the game loop by
    /// <see cref="Drain"/>; nothing here touches Unity APIs from the native callback. On every other platform the
    /// native binding is a no-op and IsSupported is false, so the DTOs and event mapping compile (and test) everywhere.
    /// </summary>
    internal static class StashNativeDesktopBridge
    {
        internal const string PresentationAttached = "attached";
        internal const string PresentationWindow = "window";

        // Event types, 1:1 with the mobile callbacks (StashNativeDesktop.h).
        internal const string EventPaymentSuccess = "paymentSuccess";
        internal const string EventPaymentFailure = "paymentFailure";
        internal const string EventDialogDismissed = "dialogDismissed";
        internal const string EventOptInResponse = "optInResponse";
        internal const string EventPageLoaded = "pageLoaded";
        internal const string EventNetworkError = "networkError";
        internal const string EventExternalPayment = "externalPayment";
        internal const string EventPurchaseProcessing = "purchaseProcessing";
        internal const string EventProcessingCompleted = "processingCompleted";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void EventCallback(IntPtr type, IntPtr payload, IntPtr userData);

        private static readonly ConcurrentQueue<KeyValuePair<string, string>> PendingEvents = new ConcurrentQueue<KeyValuePair<string, string>>();
        private static EventCallback _pinnedCallback;
        private static bool _callbackInstalled;

        internal static bool IsSupported
        {
            get
            {
#if UNITY_EDITOR_WIN || UNITY_EDITOR_OSX || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
                return true;
#else
                return false;
#endif
            }
        }

        [AOT.MonoPInvokeCallback(typeof(EventCallback))]
        private static void HandleNativeEvent(IntPtr type, IntPtr payload, IntPtr userData)
        {
            // Native UI thread, possibly inside window-message dispatch: enqueue only.
            PendingEvents.Enqueue(new KeyValuePair<string, string>(Utf8(type), Utf8(payload)));
        }

        private static string Utf8(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return "";
            int length = 0;
            while (Marshal.ReadByte(ptr, length) != 0) length++;
            if (length == 0) return "";
            var bytes = new byte[length];
            Marshal.Copy(ptr, bytes, 0, length);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        /// <summary>Delivers queued native events on the calling (game) thread.</summary>
        internal static void Drain(Action<string, string> handler)
        {
            KeyValuePair<string, string> e;
            while (PendingEvents.TryDequeue(out e))
                handler(e.Key, e.Value);
        }

        /// <summary>Loads the native host and installs the event callback. False when the binary is missing.</summary>
        internal static bool EnsureReady()
        {
            if (!Native.EnsureLoaded()) return false;
            if (!_callbackInstalled)
            {
                _pinnedCallback = HandleNativeEvent;
                Native.SetEventCallback(Marshal.GetFunctionPointerForDelegate(_pinnedCallback), IntPtr.Zero);
                _callbackInstalled = true;
            }
            return true;
        }

        internal static void OpenCard(string url, string configJson)
        {
            if (!EnsureReady()) throw new InvalidOperationException("StashNativeDesktop native host is not available");
            Native.OpenCard(url, configJson);
        }

        internal static void OpenModal(string url, string configJson)
        {
            if (!EnsureReady()) throw new InvalidOperationException("StashNativeDesktop native host is not available");
            Native.OpenModal(url, configJson);
        }

        internal static void OpenBrowser(string url)
        {
            if (!EnsureReady()) throw new InvalidOperationException("StashNativeDesktop native host is not available");
            Native.OpenBrowser(url);
        }

        internal static void Dismiss() { if (Native.IsLoaded) Native.Dismiss(); }
        internal static void ResetPresentationState() { if (Native.IsLoaded) Native.ResetPresentationState(); }
        internal static bool IsCurrentlyPresented => Native.IsLoaded && Native.IsCurrentlyPresented() != 0;
        internal static bool IsPurchaseProcessing => Native.IsLoaded && Native.IsPurchaseProcessing() != 0;
        internal static void Prewarm() { if (EnsureReady()) Native.Prewarm(); }
        internal static void SetInspectableWebViewsEnabled(bool enabled) { if (EnsureReady()) Native.SetInspectableWebViewsEnabled(enabled ? 1 : 0); }
        internal static string Version => Native.IsLoaded ? Utf8(Native.GetVersion()) : "";

        internal static void SetHostWindow(IntPtr handle)
        {
            if (EnsureReady()) Native.SetHostWindow(handle);
        }

        /// <summary>
        /// Releases the webview environment and clears the native callback. Called on quit and, in the editor,
        /// before assembly reload and when leaving play mode; otherwise the host keeps a function pointer into a
        /// dead domain. The library itself is never unloaded.
        /// </summary>
        internal static void Shutdown()
        {
            if (!Native.IsLoaded) return;
            try
            {
                Native.Shutdown();
            }
            catch (Exception e)
            {
                Debug.LogWarning("StashNative: desktop shutdown failed: " + e.Message);
            }
            _callbackInstalled = false;
            _pinnedCallback = null;
            KeyValuePair<string, string> ignored;
            while (PendingEvents.TryDequeue(out ignored)) { }
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InstallEditorLifetimeHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) Shutdown();
            };
            UnityEditor.EditorApplication.quitting += Shutdown;
        }
#endif

        // -- Native binding ----------------------------------------------------------------------------------

#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
        /// <summary>Windows: load-by-name P/Invoke into StashNativeDesktop.dll (Plugins/Windows/x86_64).</summary>
        private static class Native
        {
            private const string Dll = "StashNativeDesktop";

            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "StashNativeDesktop_SetEventCallback")]
            internal static extern void SetEventCallback(IntPtr callback, IntPtr userData);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "StashNativeDesktop_SetHostWindow")]
            internal static extern void SetHostWindow(IntPtr handle);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "StashNativeDesktop_OpenCard")]
            internal static extern void OpenCard([MarshalAs(UnmanagedType.LPUTF8Str)] string url, [MarshalAs(UnmanagedType.LPUTF8Str)] string configJson);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "StashNativeDesktop_OpenModal")]
            internal static extern void OpenModal([MarshalAs(UnmanagedType.LPUTF8Str)] string url, [MarshalAs(UnmanagedType.LPUTF8Str)] string configJson);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "StashNativeDesktop_OpenBrowser")]
            internal static extern void OpenBrowser([MarshalAs(UnmanagedType.LPUTF8Str)] string url);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "StashNativeDesktop_Dismiss")]
            internal static extern void Dismiss();
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "StashNativeDesktop_ResetPresentationState")]
            internal static extern void ResetPresentationState();
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "StashNativeDesktop_IsCurrentlyPresented")]
            internal static extern int IsCurrentlyPresented();
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "StashNativeDesktop_IsPurchaseProcessing")]
            internal static extern int IsPurchaseProcessing();
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "StashNativeDesktop_Prewarm")]
            internal static extern void Prewarm();
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "StashNativeDesktop_SetInspectableWebViewsEnabled")]
            internal static extern void SetInspectableWebViewsEnabled(int enabled);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "StashNativeDesktop_GetVersion")]
            internal static extern IntPtr GetVersion();
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "StashNativeDesktop_Shutdown")]
            internal static extern void Shutdown();

            private static bool _probed;
            private static bool _loaded;

            internal static bool IsLoaded => _loaded;

            internal static bool EnsureLoaded()
            {
                if (_probed) return _loaded;
                _probed = true;
                try
                {
                    GetVersion();
                    _loaded = true;
                }
                catch (DllNotFoundException e)
                {
                    Debug.LogError("StashNative: StashNativeDesktop.dll not found (Plugins/Windows/x86_64): " + e.Message);
                }
                catch (EntryPointNotFoundException e)
                {
                    Debug.LogError("StashNative: StashNativeDesktop.dll is not the expected version: " + e.Message);
                }
                return _loaded;
            }
        }
#elif UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR)
        /// <summary>
        /// macOS: dlopen / dlsym by explicit path. Load-by-name of a flat bundle from a UPM package is what both
        /// existing codebases avoid; the path is the package folder in the editor and Contents/PlugIns in players.
        /// </summary>
        private static class Native
        {
            private const string BundleName = "StashNativeDesktop.bundle";

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetEventCallbackFn(IntPtr callback, IntPtr userData);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetHostWindowFn(IntPtr handle);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void OpenFn([MarshalAs(UnmanagedType.LPUTF8Str)] string url, [MarshalAs(UnmanagedType.LPUTF8Str)] string configJson);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void OpenBrowserFn([MarshalAs(UnmanagedType.LPUTF8Str)] string url);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void VoidFn();
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int IntFn();
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void IntArgFn(int value);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr PtrFn();

            [DllImport("libdl.dylib", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr dlopen(string filename, int flag);
            [DllImport("libdl.dylib", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr dlsym(IntPtr handle, string symbol);
            [DllImport("libdl.dylib", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr dlerror();

            private static IntPtr _handle = IntPtr.Zero;
            private static bool _probed;
            private static SetEventCallbackFn _setEventCallback;
            private static SetHostWindowFn _setHostWindow;
            private static OpenFn _openCard;
            private static OpenFn _openModal;
            private static OpenBrowserFn _openBrowser;
            private static VoidFn _dismiss;
            private static VoidFn _resetPresentationState;
            private static IntFn _isCurrentlyPresented;
            private static IntFn _isPurchaseProcessing;
            private static VoidFn _prewarm;
            private static IntArgFn _setInspectable;
            private static PtrFn _getVersion;
            private static VoidFn _shutdown;

            internal static bool IsLoaded => _handle != IntPtr.Zero;

            internal static bool EnsureLoaded()
            {
                if (IsLoaded) return true;
                if (_probed) return false;
                _probed = true;
                string path = ResolveBundlePath();
                if (path == null)
                {
                    Debug.LogError("StashNative: " + BundleName + " not found (package Plugins/macOS in the editor, Contents/PlugIns in players).");
                    return false;
                }
                IntPtr handle = dlopen(path, 2 | 8); // RTLD_NOW | RTLD_GLOBAL
                if (handle == IntPtr.Zero)
                {
                    IntPtr err = dlerror();
                    Debug.LogError("StashNative: dlopen failed for " + path + ": " + (err != IntPtr.Zero ? Marshal.PtrToStringAnsi(err) : "unknown error"));
                    return false;
                }
                _handle = handle;
                _setEventCallback = Resolve<SetEventCallbackFn>("StashNativeDesktop_SetEventCallback");
                _setHostWindow = Resolve<SetHostWindowFn>("StashNativeDesktop_SetHostWindow");
                _openCard = Resolve<OpenFn>("StashNativeDesktop_OpenCard");
                _openModal = Resolve<OpenFn>("StashNativeDesktop_OpenModal");
                _openBrowser = Resolve<OpenBrowserFn>("StashNativeDesktop_OpenBrowser");
                _dismiss = Resolve<VoidFn>("StashNativeDesktop_Dismiss");
                _resetPresentationState = Resolve<VoidFn>("StashNativeDesktop_ResetPresentationState");
                _isCurrentlyPresented = Resolve<IntFn>("StashNativeDesktop_IsCurrentlyPresented");
                _isPurchaseProcessing = Resolve<IntFn>("StashNativeDesktop_IsPurchaseProcessing");
                _prewarm = Resolve<VoidFn>("StashNativeDesktop_Prewarm");
                _setInspectable = Resolve<IntArgFn>("StashNativeDesktop_SetInspectableWebViewsEnabled");
                _getVersion = Resolve<PtrFn>("StashNativeDesktop_GetVersion");
                _shutdown = Resolve<VoidFn>("StashNativeDesktop_Shutdown");
                if (_setEventCallback == null || _openCard == null || _openModal == null || _openBrowser == null || _dismiss == null
                    || _resetPresentationState == null || _isCurrentlyPresented == null || _isPurchaseProcessing == null || _shutdown == null)
                {
                    Debug.LogError("StashNative: " + BundleName + " is missing exports; it is not the expected version.");
                    _handle = IntPtr.Zero;
                    return false;
                }
                return true;
            }

            private static T Resolve<T>(string symbol) where T : class
            {
                IntPtr ptr = dlsym(_handle, symbol);
                if (ptr == IntPtr.Zero) ptr = dlsym(_handle, "_" + symbol);
                return ptr != IntPtr.Zero ? Marshal.GetDelegateForFunctionPointer(ptr, typeof(T)) as T : null;
            }

            private static string ResolveBundlePath()
            {
                var candidates = new System.Collections.Generic.List<string>();
#if UNITY_EDITOR
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(StashNativeDesktopBridge).Assembly);
                if (package != null)
                    candidates.Add(Path.Combine(package.resolvedPath, "Plugins", "macOS", BundleName));
                candidates.Add(Path.Combine(Application.dataPath, "Plugins", "macOS", BundleName));
#else
                // Unity copies native plugins to <app>/Contents/PlugIns.
                candidates.Add(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PlugIns", BundleName)));
                candidates.Add(Path.GetFullPath(Path.Combine(Application.dataPath, "PlugIns", BundleName)));
                candidates.Add(Path.GetFullPath(Path.Combine(Application.dataPath, "Plugins", BundleName)));
#endif
                foreach (string candidate in candidates)
                {
                    if (File.Exists(candidate) || Directory.Exists(candidate)) return candidate;
                }
                return null;
            }

            internal static void SetEventCallback(IntPtr callback, IntPtr userData) => _setEventCallback(callback, userData);
            internal static void SetHostWindow(IntPtr handle) { if (_setHostWindow != null) _setHostWindow(handle); }
            internal static void OpenCard(string url, string configJson) => _openCard(url, configJson);
            internal static void OpenModal(string url, string configJson) => _openModal(url, configJson);
            internal static void OpenBrowser(string url) => _openBrowser(url);
            internal static void Dismiss() => _dismiss();
            internal static void ResetPresentationState() => _resetPresentationState();
            internal static int IsCurrentlyPresented() => _isCurrentlyPresented();
            internal static int IsPurchaseProcessing() => _isPurchaseProcessing();
            internal static void Prewarm() { if (_prewarm != null) _prewarm(); }
            internal static void SetInspectableWebViewsEnabled(int enabled) { if (_setInspectable != null) _setInspectable(enabled); }
            internal static IntPtr GetVersion() => _getVersion != null ? _getVersion() : IntPtr.Zero;
            internal static void Shutdown() => _shutdown();
        }
#else
        private static class Native
        {
            internal static bool IsLoaded => false;
            internal static bool EnsureLoaded() => false;
            internal static void SetEventCallback(IntPtr callback, IntPtr userData) { }
            internal static void SetHostWindow(IntPtr handle) { }
            internal static void OpenCard(string url, string configJson) { }
            internal static void OpenModal(string url, string configJson) { }
            internal static void OpenBrowser(string url) { }
            internal static void Dismiss() { }
            internal static void ResetPresentationState() { }
            internal static int IsCurrentlyPresented() => 0;
            internal static int IsPurchaseProcessing() => 0;
            internal static void Prewarm() { }
            internal static void SetInspectableWebViewsEnabled(int enabled) { }
            internal static IntPtr GetVersion() => IntPtr.Zero;
            internal static void Shutdown() { }
        }
#endif
    }
}
