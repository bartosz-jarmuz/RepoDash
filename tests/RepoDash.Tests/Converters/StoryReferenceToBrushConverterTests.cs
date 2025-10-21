using System.Globalization;
using System.Windows.Media;
using Moq;
using NUnit.Framework;
using RepoDash.App.Converters;
using RepoDash.Core.Abstractions;

namespace RepoDash.Tests.Converters;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public sealed class StoryReferenceToBrushConverterTests
{
    [Test]
    public void Convert_Background_ReturnsColorizerBrush()
    {
        var colorizer = new Mock<IColorizer>();
        colorizer.Setup(c => c.GetBackgroundColorFor("ABC-123")).Returns(0xFFAABBCC);

        var converter = new StoryReferenceToBrushConverter
        {
            Mode = StoryColorMode.Background,
            Colorizer = colorizer.Object
        };

        var result = converter.Convert("ABC-123", typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.InstanceOf<SolidColorBrush>());
        var brush = (SolidColorBrush)result!;
        Assert.That(brush.Color, Is.EqualTo(Color.FromArgb(0xFF, 0xAA, 0xBB, 0xCC)));
        colorizer.Verify(c => c.GetBackgroundColorFor("ABC-123"), Times.Once);
    }

    [Test]
    public void Convert_Foreground_FallsBackToBlackWhenColorMissing()
    {
        var colorizer = new Mock<IColorizer>();
        colorizer.Setup(c => c.GetForegroundColorFor(It.IsAny<string>())).Returns((uint?)null);

        var converter = new StoryReferenceToBrushConverter
        {
            Mode = StoryColorMode.Foreground,
            Colorizer = colorizer.Object
        };

        var result = converter.Convert("ABC-123", typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.SameAs(Brushes.Black));
    }
}
