using NUnit.Framework;
using XianXia.Unity.Host;

namespace XianXia.Tests.EditMode
{
    public sealed class OutdoorEntityReconcileGateTests
    {
        [Test]
        public void StationaryFingerprint_DoesNotReconcileAgain()
        {
            var gate = new OutdoorEntityReconcileGate();
            Assert.AreEqual(
                OutdoorEntityReconcileDecision.ReconcileAndRefresh,
                gate.Decide(17, 3));
            gate.Commit(17, 3);

            Assert.AreEqual(OutdoorEntityReconcileDecision.None, gate.Decide(17, 3));
        }

        [Test]
        public void DirtyOrScopeChange_Reconciles()
        {
            var gate = new OutdoorEntityReconcileGate();
            gate.Commit(17, 3);

            Assert.AreEqual(
                OutdoorEntityReconcileDecision.ReconcileAndRefresh,
                gate.Decide(18, 3));
            gate.Commit(18, 3);
            gate.MarkDirty(3);
            Assert.AreEqual(
                OutdoorEntityReconcileDecision.ReconcileAndRefresh,
                gate.Decide(18, 3));
        }

        [Test]
        public void RuntimeGenerationAdvance_RefreshesViewsWithoutRuntimeReconcile()
        {
            var gate = new OutdoorEntityReconcileGate();
            gate.Commit(17, 3);

            Assert.AreEqual(
                OutdoorEntityReconcileDecision.RefreshViewsOnly,
                gate.Decide(18, 4));
            gate.Commit(18, 4);
            Assert.AreEqual(OutdoorEntityReconcileDecision.None, gate.Decide(18, 4));
        }

        [Test]
        public void RuntimeGenerationAfterDirty_CoalescesTheRuntimeReconcile()
        {
            var gate = new OutdoorEntityReconcileGate();
            gate.Commit(17, 3);
            gate.MarkDirty(3);

            Assert.AreEqual(
                OutdoorEntityReconcileDecision.RefreshViewsOnly,
                gate.Decide(18, 4));
        }

        [Test]
        public void DirtyAfterRuntimeGeneration_StillReconcilesTheNewDomainChange()
        {
            var gate = new OutdoorEntityReconcileGate();
            gate.Commit(17, 3);
            gate.MarkDirty(4);

            Assert.AreEqual(
                OutdoorEntityReconcileDecision.ReconcileAndRefresh,
                gate.Decide(18, 4));
        }

        [Test]
        public void LoadedTravelBoundary_ChangesCombinedFingerprint()
        {
            var outside = OutdoorEntityReconcileGate.CombineFingerprint(17, 0);
            var inside = OutdoorEntityReconcileGate.CombineFingerprint(17, 1);

            Assert.AreNotEqual(outside, inside);
        }
    }
}
