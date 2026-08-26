#if UNITY_EDITOR_WIN || UNITY_EDITOR_OSX
using NUnit.Framework;
using Stash.Native.Desktop;

namespace Stash.Native.Tests
{
    /// <summary>The vendored native host loads from the package and reports the expected ABI.</summary>
    public class StashDesktopBridgeTests
    {
        [Test]
        public void IsSupportedOnDesktopEditors()
        {
            Assert.IsTrue(StashNativeDesktopBridge.IsSupported);
        }

        [Test]
        public void NativeHostLoadsAndReportsVersion()
        {
            Assert.IsTrue(StashNativeDesktopBridge.EnsureReady(), "vendored StashNativeDesktop binary should load from the package");
            StringAssert.Contains(".", StashNativeDesktopBridge.Version);
            Assert.IsFalse(StashNativeDesktopBridge.IsCurrentlyPresented);
            Assert.IsFalse(StashNativeDesktopBridge.IsPurchaseProcessing);
        }

        [Test]
        public void DrainIsEmptyWithoutEvents()
        {
            int count = 0;
            StashNativeDesktopBridge.Drain((t, p) => count++);
            Assert.AreEqual(0, count);
        }

        [Test]
        public void ShutdownIsSafeToRepeat()
        {
            StashNativeDesktopBridge.EnsureReady();
            StashNativeDesktopBridge.Shutdown();
            StashNativeDesktopBridge.Shutdown();
            Assert.IsFalse(StashNativeDesktopBridge.IsCurrentlyPresented);
            Assert.IsTrue(StashNativeDesktopBridge.EnsureReady(), "usable again after shutdown");
        }
    }
}
#endif
