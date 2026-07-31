using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using XianXia.Core;
using XianXia.Data;

namespace XianXia.Tests
{
    public sealed class AssemblyBoundaryTests
    {
        [Test]
        public void Core_Assembly_Exposes_Phase1_Marker()
        {
            Assert.AreEqual("CoreMilestone1", CoreAssemblyMarker.MilestoneId);
            Assert.AreEqual(1, CoreAssemblyMarker.Phase);
        }

        [Test]
        public void Data_Assembly_Exposes_Phase1_Marker()
        {
            Assert.AreEqual("CoreMilestone1", DataAssemblyMarker.MilestoneId);
            Assert.AreEqual(1, DataAssemblyMarker.Phase);
        }

        [Test]
        public void Core_DoesNotReference_UnityEngine()
        {
            AssertNoUnityEngineReference(typeof(CoreAssemblyMarker).Assembly, "XianXia.Core");
        }

        [Test]
        public void Data_DoesNotReference_UnityEngine()
        {
            AssertNoUnityEngineReference(typeof(DataAssemblyMarker).Assembly, "XianXia.Data");
        }

        [Test]
        public void Data_References_Core()
        {
            var names = typeof(DataAssemblyMarker).Assembly
                .GetReferencedAssemblies()
                .Select(a => a.Name)
                .ToArray();
            Assert.That(names, Does.Contain("XianXia.Core"));
        }

        static void AssertNoUnityEngineReference(Assembly assembly, string label)
        {
            var hits = assembly.GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(IsUnityEngineAssembly)
                .ToArray();
            Assert.IsEmpty(hits, $"{label} must not reference UnityEngine. Hits: {string.Join(", ", hits)}");
        }

        static bool IsUnityEngineAssembly(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name == "UnityEngine") return true;
            if (name.StartsWith("UnityEngine.", StringComparison.Ordinal)) return true;
            if (name == "UnityEditor") return true;
            if (name.StartsWith("UnityEditor.", StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
