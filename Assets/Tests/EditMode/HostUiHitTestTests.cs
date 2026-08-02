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
    }
}
