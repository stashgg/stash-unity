using NUnit.Framework;
using Stash.Native.Desktop;
using UnityEngine;

namespace Stash.Native.Tests
{
    /// <summary>Config structs -> the JSON the desktop hosts parse (mobile field names, desktop-only keys).</summary>
    public class StashDesktopConfigTests
    {
        [Test]
        public void CardDefaultsSerializeWithMobileFieldNames()
        {
            string json = JsonUtility.ToJson(StashDesktopCardConfigDto.From(StashNativeCardConfig.Default, StashNativeDesktopBridge.PresentationAttached, Vector2.zero, false));
            StringAssert.Contains("\"forcePortrait\":false", json);
            StringAssert.Contains("\"cardHeightRatioPortrait\":0.68", json);
            StringAssert.Contains("\"tabletHeightRatioLandscape\":0.6", json);
            StringAssert.Contains("\"autoClose\":true", json);
            StringAssert.Contains("\"backgroundColor\":\"\"", json);
            StringAssert.Contains("\"presentation\":\"attached\"", json);
            StringAssert.Contains("\"width\":0", json);
            StringAssert.Contains("\"allowFileUrls\":false", json);
        }

        [Test]
        public void CardCustomValuesRoundTrip()
        {
            var config = StashNativeCardConfig.Default;
            config.autoClose = false;
            config.backgroundColor = "#1e1e1e";
            string json = JsonUtility.ToJson(StashDesktopCardConfigDto.From(config, StashNativeDesktopBridge.PresentationWindow, new Vector2(390, 844), true));
            StringAssert.Contains("\"autoClose\":false", json);
            StringAssert.Contains("\"backgroundColor\":\"#1e1e1e\"", json);
            StringAssert.Contains("\"presentation\":\"window\"", json);
            StringAssert.Contains("\"width\":390", json);
            StringAssert.Contains("\"height\":844", json);
            StringAssert.Contains("\"allowFileUrls\":true", json);
            var parsed = JsonUtility.FromJson<StashDesktopCardConfigDto>(json);
            Assert.IsFalse(parsed.autoClose);
            Assert.AreEqual("#1e1e1e", parsed.backgroundColor);
            Assert.AreEqual(390f, parsed.width);
        }

        [Test]
        public void ModalDefaultsSerialize()
        {
            string json = JsonUtility.ToJson(StashDesktopModalConfigDto.From(StashNativeModalConfig.Default, StashNativeDesktopBridge.PresentationAttached, Vector2.zero, false));
            StringAssert.Contains("\"allowDismiss\":true", json);
            StringAssert.Contains("\"phoneWidthRatioPortrait\":0.8", json);
            StringAssert.Contains("\"tabletHeightRatioLandscape\":0.4", json);
            StringAssert.Contains("\"autoClose\":true", json);
            StringAssert.DoesNotContain("forcePortrait", json);
        }

        [Test]
        public void NullBackgroundColorBecomesEmptyString()
        {
            var config = StashNativeModalConfig.Default;
            config.backgroundColor = null;
            string json = JsonUtility.ToJson(StashDesktopModalConfigDto.From(config, StashNativeDesktopBridge.PresentationAttached, Vector2.zero, false));
            StringAssert.Contains("\"backgroundColor\":\"\"", json);
        }
    }
}
