using NugetDemo;
using Xunit;

namespace NugetDemo.Tests
{
    public class StringExtensionsTests
    {
        [Fact]
        public void Should_truncate_if_length_exceeds_max_length()
        {
            Assert.Equal("Welcome", "Welcome to .NET Core".Truncate(7));
        }
    }
}
