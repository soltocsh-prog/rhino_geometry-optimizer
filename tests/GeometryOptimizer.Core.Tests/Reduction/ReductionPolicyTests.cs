using GeometryOptimizer.Core.Contracts;
using GeometryOptimizer.Core.Reduction;
using Xunit;

namespace GeometryOptimizer.Core.Tests.Reduction;

public sealed class ReductionPolicyTests
{
    [Theory]
    [InlineData(100, 10, 90)]
    [InlineData(100, 50, 50)]
    [InlineData(100, 90, 10)]
    [InlineData(5, 90, 4)]
    public void TargetFaceCount_UsesPercentageWithFourFaceMinimum(
        int originalFaceCount,
        int reductionPercent,
        int expected)
    {
        var request = new ReductionRequest(Guid.NewGuid(), originalFaceCount, reductionPercent);

        Assert.Equal(expected, ReductionPolicy.TargetFaceCount(request));
    }

    [Theory]
    [InlineData(0, 50, 7)]
    [InlineData(100, 9, 7)]
    [InlineData(100, 91, 7)]
    [InlineData(100, 50, 0)]
    [InlineData(100, 50, 11)]
    public void TargetFaceCount_RejectsInvalidInput(
        int originalFaceCount,
        int reductionPercent,
        int accuracy)
    {
        var request = new ReductionRequest(
            Guid.NewGuid(),
            originalFaceCount,
            reductionPercent,
            accuracy);

        Assert.Throws<ArgumentOutOfRangeException>(() => ReductionPolicy.TargetFaceCount(request));
    }
}
