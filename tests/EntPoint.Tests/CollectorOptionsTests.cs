using EntPoint.Collector;
using Xunit;

namespace EntPoint.Tests
{
	public sealed class CollectorOptionsTests
	{
		[Fact]
		public void Parse_DefaultsToOneMachine()
		{
			CollectorOptions options = CollectorOptions.Parse([]);

			Assert.Equal(1, options.MachineCount);
		}

		[Fact]
		public void Parse_ReadsConfiguredMachineCount()
		{
			CollectorOptions options = CollectorOptions.Parse(["--machines", "3"]);

			Assert.Equal(3, options.MachineCount);
		}

		[Theory]
		[InlineData("0")]
		[InlineData("-1")]
		public void Parse_RejectsInvalidMachineCount(string machineCount)
		{
			Assert.Throws<ArgumentOutOfRangeException>(
				() => CollectorOptions.Parse(["--machines", machineCount]));
		}
	}
}
