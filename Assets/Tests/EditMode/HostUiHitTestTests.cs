using NUnit.Framework;
using UnityEngine;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class HostUiHitTestTests
    {
        [Test]
        public void PublishedRects_BlockScreenPoints_InGuiSpace()
        {
            HostUiHitTest.BeginFrame();
            HostUiHitTest.Block(new Rect(100f, 200f, 50f, 40f));
            HostUiHitTest.EndFrame();

            // GUI (100,200) → screen y = Screen.height - 200
            var screen = new Vector2(120f, Screen.height - 220f);
            Assert.IsTrue(HostUiHitTest.ContainsScreenPoint(screen));
            Assert.IsFalse(HostUiHitTest.ContainsScreenPoint(new Vector2(10f, 10f)));
        }

        [Test]
        public void ContainsCurrentGuiPoint_UsesCurrentFrameRectsOnly()
        {
            HostUiHitTest.BeginFrame();
            HostUiHitTest.Block(new Rect(10f, 20f, 100f, 80f));
            Assert.IsTrue(HostUiHitTest.ContainsCurrentGuiPoint(new Vector2(50f, 50f)));
            Assert.IsFalse(HostUiHitTest.ContainsCurrentGuiPoint(new Vector2(200f, 200f)));
            HostUiHitTest.EndFrame();
        }

        [Test]
        public void BlockSelectionWholeScreen_DoesNotBlockCurrentGuiPoint()
        {
            HostUiHitTest.BeginFrame();
            HostUiHitTest.BlockSelectionWholeScreen();
            Assert.IsFalse(HostUiHitTest.ContainsCurrentGuiPoint(new Vector2(50f, 50f)));
            HostUiHitTest.EndFrame();

            var screen = new Vector2(50f, Screen.height - 50f);
            Assert.IsTrue(HostUiHitTest.ContainsScreenPoint(screen));
        }
    }
}
