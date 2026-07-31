using System;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;

namespace XianXia.Tests
{
    public sealed class ResultTests
    {
        [Test]
        public void Result_Success_And_Failure()
        {
            Assert.IsTrue(Result.Success().IsSuccess);
            var fail = Result.Failure(ErrorCode.InvalidArgument, "bad");
            Assert.IsTrue(fail.IsFailure);
            Assert.AreEqual(ErrorCode.InvalidArgument, fail.Error.Code);
        }

        [Test]
        public void ResultT_Failed_DoesNotExposeValue()
        {
            var fail = Result.Fail<int>(ErrorCode.NotFound, "missing");
            Assert.IsTrue(fail.IsFailure);
            Assert.Throws<InvalidOperationException>(() => _ = fail.Value);
            Assert.IsFalse(fail.TryGetValue(out _));
        }

        [Test]
        public void ResultT_Success_ExposesValue()
        {
            var ok = Result.Ok(42);
            Assert.IsTrue(ok.IsSuccess);
            Assert.AreEqual(42, ok.Value);
            Assert.Throws<InvalidOperationException>(() => _ = ok.Error);
        }

        [Test]
        public void ValidationReport_CollectsMultipleErrors()
        {
            var report = new ValidationReport();
            report.Add(ErrorCode.MissingRequiredField, "a");
            report.Add(ErrorCode.MissingRequiredField, "b");
            Assert.IsFalse(report.IsValid);
            Assert.AreEqual(2, report.Errors.Count);

            var result = report.ToResult();
            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual(ErrorCode.ValidationFailed, result.Error.Code);
        }

        [Test]
        public void DefinitionId_Parse_UsesResult_And_TryParseRemains()
        {
            var ok = DefinitionId.Parse("base:item_x");
            Assert.IsTrue(ok.IsSuccess);
            Assert.AreEqual("base:item_x", ok.Value.ToString());

            var bad = DefinitionId.Parse("nope");
            Assert.IsTrue(bad.IsFailure);
            Assert.AreEqual(ErrorCode.InvalidDefinitionId, bad.Error.Code);

            Assert.IsTrue(DefinitionId.TryParse("base:item_x", out var id));
            Assert.AreEqual(ok.Value, id);
            Assert.IsFalse(DefinitionId.TryParse(":x", out _));
        }

        sealed class SampleValidator : IValidator<string>
        {
            public void Validate(string target, ValidationReport report)
            {
                if (string.IsNullOrEmpty(target))
                    report.Add(ErrorCode.MissingRequiredField, "empty");
                if (target != null && target.Length < 2)
                    report.Add(ErrorCode.InvalidArgument, "too_short");
            }
        }

        [Test]
        public void IValidator_WritesIntoReport()
        {
            var report = new ValidationReport();
            new SampleValidator().Validate("a", report);
            Assert.AreEqual(1, report.Errors.Count);
        }
    }
}
