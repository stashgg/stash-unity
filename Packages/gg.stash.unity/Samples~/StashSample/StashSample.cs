using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Stash.Native;

/// <summary>
/// Stash for Unity sample, mirroring the stash-native sample apps: bottom tabs Test / Settings / Instances,
/// option sub-screens, and every SDK callback surfaced as a timestamped chip.
///
/// UI is UI Toolkit (Resources/StashSample.uxml + .uss). This script wires controls to the SDK; the
/// "Actions" and "Callbacks" regions are the integration reference, the rest is navigation and form plumbing.
/// </summary>
[RequireComponent(typeof(StashLinkGenerator))]
public class StashSample : MonoBehaviour
{
    // test.stashpreview.com is a test page that can trigger each checkout callback.
    private const string DefaultUrl = "https://test.stashpreview.com/";

    [SerializeField] private StashLinkGenerator linkGenerator;

    // ---- options the forms edit --------------------------------------------------------------------
    private StashNativeCardConfig cardConfig = StashNativeCardConfig.Default;
    private StashNativeBrowserConfig browserConfig = StashNativeBrowserConfig.Default;
    private StashNativeScannerConfig scannerConfig = StashNativeScannerConfig.Default;
    private bool captureBackdrop;   // Android: snapshot the frame behind a portrait card
    private bool keepAlive;         // Android: foreground service while the browser is open

    private void Awake()
    {
        if (linkGenerator == null) linkGenerator = GetComponent<StashLinkGenerator>();
        LoadInstances();
        SubscribeCallbacks();
        Application.deepLinkActivated += url => Chip("Deeplink · " + Shorten(url));
    }

    private void Start() => BuildUI();

    #region Actions

    private void OpenCard(string url)
    {
        if (string.IsNullOrEmpty(url)) { Chip("No URL"); return; }
        StartCoroutine(OpenCardRoutine(url));
    }

    private IEnumerator OpenCardRoutine(string url)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (captureBackdrop && cardConfig.orientationPreference == StashNativeOrientationPreference.Portrait)
        {
            // Snapshot the current frame so the OS orientation switch shows the game instead of a black surface.
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(UnityEngine.Screen.width, UnityEngine.Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height), 0, 0);
            tex.Apply();
            StashNative.Instance.SetBackdropBytes(ImageConversion.EncodeToJPG(tex, 75));
            Destroy(tex);
        }
#endif
        StashNative.Instance.OpenCard(url,
            dismissCallback: () => Debug.Log("[StashSample] per-call dismiss"),
            successCallback: order => Debug.Log("[StashSample] per-call success"),
            failureCallback: () => Debug.Log("[StashSample] per-call failure"),
            config: cardConfig);
        yield break;
    }

    private void OpenBrowser(string url)
    {
        if (string.IsNullOrEmpty(url)) { Chip("No URL"); return; }
#if UNITY_ANDROID && !UNITY_EDITOR
        StashNative.Instance.SetKeepAliveEnabled(keepAlive);
        StashNative.Instance.SetKeepAliveConfig(new StashNativeKeepAliveConfig
        {
            notificationTitle = "Payment in progress",
            notificationText = "Tap to return to the game",
            notificationIconResId = 0
        });
#endif
        StashNative.Instance.OpenBrowser(url, browserConfig);
    }

    private void OpenScanner()
    {
        StashNative.Instance.OpenScanner(
            scannedCallback: code => Debug.Log("[StashSample] per-call scanned"),
            dismissCallback: () => Debug.Log("[StashSample] per-call scanner dismiss"),
            errorCallback: err => Debug.Log("[StashSample] per-call scanner error " + err),
            config: scannerConfig);
    }

    /// <summary>Signs with the selected instance and hands the generated checkout link to <paramref name="open"/>.</summary>
    private void GenerateCheckout(Action<string> open)
    {
        if (!ApplySelectedInstance()) return;
        linkGenerator.RequestCheckoutUrl(open, err => Chip("Checkout link failed · " + Shorten(err)));
    }

    private void GenerateWebshop(Action<string> open)
    {
        if (!ApplySelectedInstance()) return;
        linkGenerator.RequestAuthenticatedWebshopUrl(open, err => Chip("Webshop link failed · " + Shorten(err)), "DEFAULT");
    }

    private bool ApplySelectedInstance()
    {
        var inst = SelectedInstance;
        if (inst == null) { Chip("Add an instance first (Instances tab)"); Show(Screen.Instances); return false; }
        linkGenerator.AppId = inst.appId;
        linkGenerator.IngressApiKey = inst.secret;
        linkGenerator.Production = inst.production;
        linkGenerator.UserId = string.IsNullOrEmpty(inst.userId) ? "test_user_id" : inst.userId;
        return true;
    }

    #endregion

    #region Callbacks

    private void SubscribeCallbacks()
    {
        var s = StashNative.Instance;
        s.OnPaymentSuccess += order => OnCardEnded("Payment Success" + (string.IsNullOrEmpty(order) ? "" : " · " + Shorten(order)));
        s.OnPaymentFailure += () => OnCardEnded("Payment Failure");
        s.OnCardDismissed += () => OnCardEnded("Card Dismissed");
        s.OnNetworkError += () => OnCardEnded("Network Error");
        s.OnExternalPayment += url => OnCardEnded("External Payment");
        s.OnPageLoaded += ms => Chip("Page Loaded · " + ms.ToString("0") + " ms");
        s.OnBrowserDismissed += () => Chip("Browser Dismissed");
        s.OnQrCodeScanned += code => Chip("QR Scanned · " + Shorten(code));
        s.OnScannerDismissed += () => Chip("Scanner Dismissed");
        s.OnScannerError += err => Chip("Scanner Error · " + err);
        s.OnNativeException += (op, ex) => Chip("Native Exception · " + op);
    }

    /// <summary>Every way a card can end routes here; with autoClose off a dismiss still follows a payment result.</summary>
    private void OnCardEnded(string label)
    {
        Chip(label);
#if UNITY_ANDROID && !UNITY_EDITOR
        StashNative.Instance.SetBackdropBytes(null);
#endif
    }

    /// <summary>Full-width row with a timestamp at the top of the screen. Tap to dismiss, or it expires.</summary>
    private void Chip(string label)
    {
        Debug.Log("[StashSample] " + label);
        if (chips == null) return;
        var chip = new VisualElement();
        chip.AddToClassList("chip");
        var text = new Label(label); text.AddToClassList("chip-text");
        var time = new Label(DateTime.Now.ToString("HH:mm:ss")); time.AddToClassList("chip-time");
        chip.Add(text); chip.Add(time);
        chip.RegisterCallback<ClickEvent>(_ => chip.RemoveFromHierarchy());
        chip.schedule.Execute(() => chip.RemoveFromHierarchy()).StartingIn(6000);
        chips.Insert(0, chip);
        while (chips.childCount > 5) chips.RemoveAt(chips.childCount - 1);
    }

    private static string Shorten(string s) => s.Length > 48 ? s.Substring(0, 45) + "…" : s;

    #endregion

    #region Navigation

    private enum Screen { Test, Settings, Instances, CardOptions, BrowserOptions, ScannerOptions, InstanceEdit }

    /// <summary>Element, title, parent screen, and the toolbar action (label + destructive flag; null for none).</summary>
    private static readonly Dictionary<Screen, (string element, string title, Screen? parent, string action, bool destructive)> Screens =
        new Dictionary<Screen, (string, string, Screen?, string, bool)>
    {
        { Screen.Test,           ("screen-test",            "Test",                 null,             null,     false) },
        { Screen.Settings,       ("screen-settings",        "Settings",             null,             null,     false) },
        { Screen.Instances,      ("screen-instances",       "Instances",            null,             "Add",    false) },
        { Screen.CardOptions,    ("screen-card-options",    "Card options",         Screen.Settings,  "Reset",  false) },
        { Screen.BrowserOptions, ("screen-browser-options", "Browser options",      Screen.Settings,  null,     false) },
        { Screen.ScannerOptions, ("screen-scanner-options", "QR Code scan options", Screen.Settings,  "Reset",  false) },
        { Screen.InstanceEdit,   ("screen-instance-edit",   "Edit Instance",        Screen.Instances, "Delete", true) },
    };

    private Screen current = Screen.Test;

    private void Show(Screen screen)
    {
        current = screen;
        var info = Screens[screen];
        foreach (var kv in Screens)
            root.Q(kv.Value.element).EnableInClassList("active", kv.Key == screen);
        root.Q<Label>("title").text = info.title;
        root.Q("back").EnableInClassList("hidden", info.parent == null);
        root.Q("logo").EnableInClassList("hidden", info.parent != null);
        var action = root.Q<Button>("action");
        action.text = info.action ?? "";
        action.EnableInClassList("hidden", info.action == null);
        action.EnableInClassList("destructive", info.destructive);
        var tab = Screens[screen].parent ?? screen;
        root.Q("tab-test").EnableInClassList("active", tab == Screen.Test);
        root.Q("tab-settings").EnableInClassList("active", tab == Screen.Settings);
        root.Q("tab-instances").EnableInClassList("active", tab == Screen.Instances);
        if (screen == Screen.Instances) RenderInstanceList();
    }

    private void Back()
    {
        var parent = Screens[current].parent;
        if (parent.HasValue) Show(parent.Value);
    }

    /// <summary>The toolbar action depends on the visible screen.</summary>
    private void ToolbarAction()
    {
        switch (current)
        {
            case Screen.Instances:      AddInstance(); break;
            case Screen.CardOptions:    cardConfig = StashNativeCardConfig.Default; Rebuild(Screen.CardOptions); break;
            case Screen.ScannerOptions: scannerConfig = StashNativeScannerConfig.Default; Rebuild(Screen.ScannerOptions); break;
            case Screen.InstanceEdit:   DeleteEditingInstance(); break;
        }
    }

    #endregion

    #region UI wiring

    private VisualElement root, chips;
    private PanelSettings panelSettings;

    private void BuildUI()
    {
        var doc = gameObject.GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
        if (doc.panelSettings == null)
        {
            // Runtime copy so the sample never edits the asset; the asset carries the UI Toolkit shaders for builds.
            doc.panelSettings = Instantiate(Resources.Load<PanelSettings>("StashSamplePanelSettings"));
            if (doc.panelSettings.themeStyleSheet == null)
                doc.panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("StashSampleTheme");
        }
        panelSettings = doc.panelSettings;
        doc.visualTreeAsset = null; // assigning the same asset again would not re-instantiate the tree (see Rebuild)
        doc.visualTreeAsset = Resources.Load<VisualTreeAsset>("StashSample");
        root = doc.rootVisualElement;
        root.styleSheets.Add(Resources.Load<StyleSheet>("StashSample"));
        chips = root.Q("chips");
        ApplyScale();

        // Hairline dividers between the rows of each card; buttons and the last row stay clean.
        root.Query(className: "section").ForEach(section =>
        {
            for (int i = 0; i < section.childCount - 1; i++)
                if (section[i].ClassListContains("unity-base-field") || section[i].ClassListContains("info-row") || section[i].ClassListContains("nav"))
                    section[i].AddToClassList("divider");
        });

        // Platform-specific rows.
#if !UNITY_ANDROID
        root.Query(className: "android-only").ForEach(e => e.AddToClassList("hidden"));
#endif
#if !UNITY_IOS
        root.Query(className: "ios-only").ForEach(e => e.AddToClassList("hidden"));
#endif

        // Navigation
        Button("back", Back);
        Button("action", ToolbarAction);
        Button("tab-test", () => Show(Screen.Test));
        Button("tab-settings", () => Show(Screen.Settings));
        Button("tab-instances", () => Show(Screen.Instances));
        Button("nav-card", () => Show(Screen.CardOptions));
        Button("nav-browser", () => Show(Screen.BrowserOptions));
        Button("nav-scanner", () => Show(Screen.ScannerOptions));

        // Test
        var checkoutUrl = Text("checkout-url", DefaultUrl);
        Button("open-card", () => OpenCard(checkoutUrl.value));
        Button("card-generate-checkout", () => GenerateCheckout(url => { checkoutUrl.value = url; OpenCard(url); }));
        var browserUrl = Text("browser-url", DefaultUrl);
        Button("open-browser", () => OpenBrowser(browserUrl.value));
        Button("browser-generate-checkout", () => GenerateCheckout(url => { browserUrl.value = url; OpenBrowser(url); }));
        Button("browser-open-webshop", () => GenerateWebshop(url => { browserUrl.value = url; OpenBrowser(url); }));
        Button("open-scanner", OpenScanner);

        // Settings
        Toggle("lock-landscape", UnityEngine.Screen.orientation == ScreenOrientation.LandscapeLeft, ApplyOrientationLock);
        root.Q<Label>("sdk-version").text = string.IsNullOrEmpty(StashNative.Instance.SdkVersion) ? "n/a in Editor" : StashNative.Instance.SdkVersion;

        // Card options
        Toggle("card-portrait", cardConfig.orientationPreference == StashNativeOrientationPreference.Portrait,
            on => cardConfig.orientationPreference = on ? StashNativeOrientationPreference.Portrait : StashNativeOrientationPreference.FollowHost);
        Toggle("card-allow-dismiss", cardConfig.allowDismiss, on => cardConfig.allowDismiss = on);
        Toggle("card-auto-close", cardConfig.autoClose, on => cardConfig.autoClose = on);
        Toggle("card-backdrop", captureBackdrop, on => captureBackdrop = on);
        Slider("card-width", cardConfig.preferredContentWidth, v => cardConfig.preferredContentWidth = v);
        Slider("card-height", cardConfig.preferredContentHeight, v => cardConfig.preferredContentHeight = v);
        Slider("card-max-height", cardConfig.maximumContentHeight, v => cardConfig.maximumContentHeight = v);
        Slider("card-margin", cardConfig.edgeMargin, v => cardConfig.edgeMargin = v);

        // Browser options
        Toggle("browser-keep-alive", keepAlive, on => keepAlive = on);
        Toggle("browser-portrait", browserConfig.orientationPreference == StashNativeOrientationPreference.Portrait,
            on => browserConfig.orientationPreference = on ? StashNativeOrientationPreference.Portrait : StashNativeOrientationPreference.FollowHost);

        // Scanner options
        Text("scanner-header", scannerConfig.headerText, v => scannerConfig.headerText = v);
        Toggle("scanner-callout", scannerConfig.showCallout, on => scannerConfig.showCallout = on);
        Text("scanner-callout-title", scannerConfig.calloutTitle, v => scannerConfig.calloutTitle = v);
        Text("scanner-callout-subtitle", scannerConfig.calloutSubtitle, v => scannerConfig.calloutSubtitle = v);
        Text("scanner-camera-unavailable", scannerConfig.cameraUnavailableText, v => scannerConfig.cameraUnavailableText = v);
        Text("scanner-connected", scannerConfig.connectedText, v => scannerConfig.connectedText = v);

        // Instances. Add / Delete live in the toolbar (ToolbarAction); the edit form writes to the instance being edited.
        Text("instance-name", "", v => Edit(i => i.name = v));
        Text("instance-app-id", "", v => Edit(i => i.appId = v.Trim()));
        Text("instance-secret", "", v => Edit(i => i.secret = v.Trim()));
        Text("instance-user-id", "", v => Edit(i => i.userId = v.Trim()));
        Toggle("instance-production", false, on => Edit(i => i.production = on));

        Show(current);
    }

    /// <summary>Re-instantiates the document (after a reset) so controls show the new values, then returns to <paramref name="screen"/>.</summary>
    private void Rebuild(Screen screen)
    {
        current = screen;
        BuildUI();
    }

    // UI scale. One unit is a fraction of the shorter screen side so the layout reads the same on every device:
    //   phones:  1/390 in portrait (an iPhone point), 1/470 in landscape (height is scarce, so units are ~17% smaller)
    //   tablets: 1/600 portrait, 1/700 landscape (far more room, so units are smaller still)
    // Re-applied whenever the screen size changes (rotation, resize). Columns depend on the resulting width in units.
    private const float PhonePortrait = 390f, PhoneLandscape = 470f, TabletPortrait = 600f, TabletLandscape = 700f;
    private const float TwoColumnMinWidth = 560f;   // content width in units from which blocks flow into two columns
    private const float ContentGutter = 20f;        // minimum horizontal margin around screen content, in units
    private Vector2Int lastScreen;

    /// <summary>Tablets are squarer than phones (iPad 4:3 to 1.43:1; phones 1.78:1 and up). Aspect only: reported DPI is
    /// unreliable in the Editor Game view, the Device Simulator and on some Android devices.</summary>
    private static bool IsTablet()
    {
        float aspect = Mathf.Max(UnityEngine.Screen.width, UnityEngine.Screen.height) / (float)Mathf.Min(UnityEngine.Screen.width, UnityEngine.Screen.height);
        return aspect < 1.6f;
    }

    private void Update()
    {
        if (UnityEngine.Screen.width != lastScreen.x || UnityEngine.Screen.height != lastScreen.y) ApplyScale();
        if (Input.GetKeyDown(KeyCode.Escape)) Back(); // Android back button
    }

    private void ApplyScale()
    {
        lastScreen = new Vector2Int(UnityEngine.Screen.width, UnityEngine.Screen.height);
        if (panelSettings == null || root == null) return;
        bool landscape = UnityEngine.Screen.width > UnityEngine.Screen.height;
        bool tablet = IsTablet();
        float reference = tablet ? (landscape ? TabletLandscape : TabletPortrait) : (landscape ? PhoneLandscape : PhonePortrait);
        float scale = Mathf.Min(UnityEngine.Screen.width, UnityEngine.Screen.height) / reference;
        panelSettings.scale = scale;

        // Safe area: toolbar and tab bar stay clear of notches, rounded corners and the home indicator.
        // The content itself keeps a comfortable gutter even where the device reports no inset (Editor, flat screens).
        var safe = UnityEngine.Screen.safeArea;
        var app = root.Q("app");
        app.style.paddingTop = (UnityEngine.Screen.height - safe.yMax) / scale;
        app.style.paddingBottom = Mathf.Max(safe.yMin / scale, 8f);
        app.style.paddingLeft = safe.xMin / scale;
        app.style.paddingRight = (UnityEngine.Screen.width - safe.xMax) / scale;

        float gutter = Mathf.Max(ContentGutter, Mathf.Max(safe.xMin, UnityEngine.Screen.width - safe.xMax) / scale);
        root.Q("screens").style.paddingLeft = gutter;
        root.Q("screens").style.paddingRight = gutter;

        // Column widths are set explicitly (percent widths do not resolve inside a ScrollView's content container).
        float contentWidth = UnityEngine.Screen.width / scale - 2f * gutter;
        bool twoColumns = contentWidth >= TwoColumnMinWidth;
        root.EnableInClassList("wide", twoColumns); // grid + compact chrome, see .wide rules in the USS
        float blockWidth = twoColumns ? Mathf.Floor(contentWidth / 2f) : contentWidth;
        root.Query(className: "block").ForEach(b => b.style.width = blockWidth);
        root.Query(className: "full").ForEach(b => b.style.width = contentWidth);

        // Chips sit just under the toolbar, inside the gutter; centered half-width in landscape.
        float chipInset = gutter + (twoColumns ? contentWidth / 4f : 0f);
        chips.style.left = chipInset;
        chips.style.right = chipInset;
        chips.style.top = (UnityEngine.Screen.height - safe.yMax) / scale + (twoColumns ? 50f : 58f);
    }

    private void ApplyOrientationLock(bool lockLandscape)
    {
        if (lockLandscape)
        {
            UnityEngine.Screen.orientation = ScreenOrientation.LandscapeLeft;
        }
        else
        {
            UnityEngine.Screen.orientation = ScreenOrientation.AutoRotation;
            UnityEngine.Screen.autorotateToPortrait = UnityEngine.Screen.autorotateToPortraitUpsideDown = true;
            UnityEngine.Screen.autorotateToLandscapeLeft = UnityEngine.Screen.autorotateToLandscapeRight = true;
        }
    }

    // Small binding helpers so the wiring above reads as a list of controls.
    private void Button(string name, Action onClick) => root.Q<Button>(name).clicked += onClick;

    private void Toggle(string name, bool value, Action<bool> onChanged)
    {
        var t = root.Q<Toggle>(name);
        t.SetValueWithoutNotify(value);
        t.RegisterValueChangedCallback(e => onChanged(e.newValue));
    }

    private TextField Text(string name, string value, Action<string> onChanged = null)
    {
        var f = root.Q<TextField>(name);
        f.SetValueWithoutNotify(value ?? "");
        if (onChanged != null) f.RegisterValueChangedCallback(e => onChanged(e.newValue));
        return f;
    }

    private void Slider(string name, float value, Action<float> onChanged)
    {
        var s = root.Q<SliderInt>(name);
        s.SetValueWithoutNotify(Mathf.RoundToInt(value));
        s.RegisterValueChangedCallback(e => onChanged(e.newValue));
    }

    #endregion

    #region Instances (named App ID + ingress secret pairs, persisted on the device)

    [Serializable]
    private class Instance { public string id; public string name; public string appId; public string secret; public string userId = "test_user_id"; public bool production; }

    [Serializable]
    private class InstanceStore { public List<Instance> instances = new List<Instance>(); public string selectedId; }

    private const string PrefsKey = "stash.sample.instances";
    private InstanceStore store = new InstanceStore();
    private Instance editing;

    private Instance SelectedInstance => store.instances.Find(i => i.id == store.selectedId);

    private void LoadInstances()
    {
        var json = PlayerPrefs.GetString(PrefsKey, "");
        if (!string.IsNullOrEmpty(json)) store = JsonUtility.FromJson<InstanceStore>(json) ?? new InstanceStore();
        // First run: seed from the values on the StashLinkGenerator component, if the developer filled them in.
        if (store.instances.Count == 0 && linkGenerator.AppId != "YOUR_APP_ID" && linkGenerator.IngressApiKey != "YOUR_INGRESS_API_KEY")
        {
            var seed = new Instance { id = Guid.NewGuid().ToString("N"), name = "Inspector", appId = linkGenerator.AppId, secret = linkGenerator.IngressApiKey, userId = linkGenerator.UserId, production = linkGenerator.Production };
            store.instances.Add(seed); store.selectedId = seed.id; SaveInstances();
        }
    }

    private void SaveInstances()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(store));
        PlayerPrefs.Save();
    }

    private void Edit(Action<Instance> change)
    {
        if (editing == null) return;
        change(editing);
        SaveInstances();
    }

    private void RenderInstanceList()
    {
        var list = root.Q("instance-list");
        list.Clear();
        if (store.instances.Count == 0)
        {
            var empty = new Label("No instances yet. Add one with your App ID and ingress secret to generate real links.");
            empty.AddToClassList("instance-empty");
            list.Add(empty);
            return;
        }
        foreach (var inst in store.instances)
        {
            var row = new VisualElement(); row.AddToClassList("instance-row");
            var select = new Button(() => { store.selectedId = inst.id; SaveInstances(); RenderInstanceList(); });
            select.AddToClassList("instance-select");
            var radio = new VisualElement(); radio.AddToClassList("instance-radio"); radio.EnableInClassList("on", inst.id == store.selectedId);
            var name = new Label(string.IsNullOrEmpty(inst.name) ? "(unnamed)" : inst.name); name.AddToClassList("instance-name");
            var env = new Label(inst.production ? "PROD" : "TEST"); env.AddToClassList("instance-env"); env.EnableInClassList("prod", inst.production);
            select.Add(radio); select.Add(name); select.Add(env);
            var edit = new Button(() => EditInstance(inst)) { text = "Edit" }; edit.AddToClassList("instance-edit");
            row.Add(select); row.Add(edit);
            if (inst != store.instances[store.instances.Count - 1]) row.AddToClassList("divider");
            list.Add(row);
        }
    }

    private void AddInstance()
    {
        var inst = new Instance { id = Guid.NewGuid().ToString("N"), name = "Instance " + (store.instances.Count + 1), appId = "", secret = "" };
        store.instances.Add(inst);
        if (store.instances.Count == 1) store.selectedId = inst.id;
        SaveInstances();
        EditInstance(inst);
    }

    private void EditInstance(Instance inst)
    {
        editing = inst;
        root.Q<TextField>("instance-name").SetValueWithoutNotify(inst.name ?? "");
        root.Q<TextField>("instance-app-id").SetValueWithoutNotify(inst.appId ?? "");
        root.Q<TextField>("instance-secret").SetValueWithoutNotify(inst.secret ?? "");
        root.Q<TextField>("instance-user-id").SetValueWithoutNotify(inst.userId ?? "");
        root.Q<Toggle>("instance-production").SetValueWithoutNotify(inst.production);
        Show(Screen.InstanceEdit);
    }

    private void DeleteEditingInstance()
    {
        if (editing == null) return;
        store.instances.Remove(editing);
        if (store.selectedId == editing.id) store.selectedId = store.instances.Count > 0 ? store.instances[0].id : null;
        editing = null;
        SaveInstances();
        Show(Screen.Instances);
    }

    #endregion
}
