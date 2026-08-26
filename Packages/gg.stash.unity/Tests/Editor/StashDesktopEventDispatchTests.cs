using System.Collections.Generic;
using NUnit.Framework;
using Stash.Native.Desktop;
using UnityEngine;

namespace Stash.Native.Tests
{
    /// <summary>Desktop event names -> public events and per-call callbacks, with the 2.3.0 lifecycle rules.</summary>
    public class StashDesktopEventDispatchTests
    {
        private StashNative _native;
        private List<string> _log;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("StashNativeTest");
            _native = go.AddComponent<StashNative>();
            _log = new List<string>();
            _native.OnPaymentSuccess += o => _log.Add("success:" + o);
            _native.OnPaymentFailure += () => _log.Add("failure");
            _native.OnDialogDismissed += () => _log.Add("dismissed");
            _native.OnOptinResponse += t => _log.Add("optin:" + t);
            _native.OnPageLoaded += ms => _log.Add("loaded:" + ms);
            _native.OnNetworkError += () => _log.Add("network");
            _native.OnExternalPayment += u => _log.Add("external:" + u);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_native.gameObject);
        }

        [Test]
        public void SuccessReachesPerCallAndGlobalListeners()
        {
            string perCall = null;
            _native.SetCurrentCallbacks(null, o => perCall = o, null);
            _native.HandleDesktopEvent(StashNativeDesktopBridge.EventPaymentSuccess, "{\"orderId\":\"1\"}");
            Assert.AreEqual("{\"orderId\":\"1\"}", perCall);
            CollectionAssert.AreEqual(new[] { "success:{\"orderId\":\"1\"}" }, _log);
            // Success alone keeps the per-call slots (autoClose = false may still emit later events).
            Assert.IsTrue(_native.HasCurrentCallbacks);
        }

        [Test]
        public void DismissedClearsPerCallCallbacks()
        {
            int dismissed = 0;
            _native.SetCurrentCallbacks(() => dismissed++, null, () => { });
            _native.HandleDesktopEvent(StashNativeDesktopBridge.EventDialogDismissed, "");
            Assert.AreEqual(1, dismissed);
            Assert.IsFalse(_native.HasCurrentCallbacks);
            CollectionAssert.AreEqual(new[] { "dismissed" }, _log);
            // A second dismissed (never emitted by the host, but harmless) does not call the cleared slot.
            _native.HandleDesktopEvent(StashNativeDesktopBridge.EventDialogDismissed, "");
            Assert.AreEqual(1, dismissed);
        }

        [Test]
        public void AutoCloseOffFailureThenSuccessBothDelivered()
        {
            var perCall = new List<string>();
            _native.SetCurrentCallbacks(() => perCall.Add("dismiss"), o => perCall.Add("success"), () => perCall.Add("failure"));
            _native.HandleDesktopEvent(StashNativeDesktopBridge.EventPaymentFailure, "");
            _native.HandleDesktopEvent(StashNativeDesktopBridge.EventPaymentSuccess, "order");
            _native.HandleDesktopEvent(StashNativeDesktopBridge.EventDialogDismissed, "");
            CollectionAssert.AreEqual(new[] { "failure", "success", "dismiss" }, perCall);
            CollectionAssert.AreEqual(new[] { "failure", "success:order", "dismissed" }, _log);
        }

        [Test]
        public void PageLoadedParsesInvariantMilliseconds()
        {
            _native.HandleDesktopEvent(StashNativeDesktopBridge.EventPageLoaded, "1801");
            _native.HandleDesktopEvent(StashNativeDesktopBridge.EventPageLoaded, "not a number");
            CollectionAssert.AreEqual(new[] { "loaded:1801" }, _log);
        }

        [Test]
        public void OptInNetworkErrorAndExternalPaymentMap()
        {
            _native.HandleDesktopEvent(StashNativeDesktopBridge.EventOptInResponse, "email");
            _native.HandleDesktopEvent(StashNativeDesktopBridge.EventNetworkError, "");
            _native.HandleDesktopEvent(StashNativeDesktopBridge.EventExternalPayment, "https://pay.example/?theme=dark");
            CollectionAssert.AreEqual(new[] { "optin:email", "network", "external:https://pay.example/?theme=dark" }, _log);
        }

        [Test]
        public void DiagnosticsAndProcessingEventsDoNotFireCallbacks()
        {
            _native.SetCurrentCallbacks(() => _log.Add("x"), o => _log.Add("x"), () => _log.Add("x"));
            _native.HandleDesktopEvent("navigation", "https://checkout.stash.gg/");
            _native.HandleDesktopEvent("navigationBlocked", "{\"url\":\"http://x\",\"reason\":\"insecure_http\"}");
            _native.HandleDesktopEvent(StashNativeDesktopBridge.EventPurchaseProcessing, "");
            _native.HandleDesktopEvent(StashNativeDesktopBridge.EventProcessingCompleted, "");
            _native.HandleDesktopEvent("webProcessCrashed", "reloading");
            _native.HandleDesktopEvent("error", "boom");
            Assert.IsEmpty(_log);
            Assert.IsTrue(_native.HasCurrentCallbacks);
        }

        [Test]
        public void NullPayloadsBecomeEmptyStrings()
        {
            _native.HandleDesktopEvent(StashNativeDesktopBridge.EventPaymentSuccess, null);
            _native.HandleDesktopEvent(StashNativeDesktopBridge.EventOptInResponse, null);
            CollectionAssert.AreEqual(new[] { "success:", "optin:" }, _log);
        }
    }
}
