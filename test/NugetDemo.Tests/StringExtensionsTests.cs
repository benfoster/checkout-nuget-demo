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

        [Fact]
        public void Should_return_null_if_source_is_null() 
        {
            Assert.Equal(null, default(string).Truncate(5));
        }
    }
}
