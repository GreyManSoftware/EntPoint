using EntPoint.Ingestion;
using Xunit;

namespace EntPoint.Tests
{
	public sealed class IngestionOptionsTests
	{
		[Fact]
		public void Parse_UsesDefaultInputPath()
		{
			IngestionOptions options = IngestionOptions.Parse(
				[
					"--postgres",
					"Host=localhost;Database=entpoint",
					"--mongo",
					"mongodb://localhost:27017",
					"--mongo-database",
					"entpoint"
				]);

			Assert.Equal(Path.Combine("data", "events.ndjson"), options.InputPath);
			Assert.Equal("entpoint", options.MongoDatabaseName);
			Assert.False(options.Reset);
			Assert.DoesNotContain(options.PostgresConnectionString, options.ToString());
			Assert.DoesNotContain(options.MongoConnectionString, options.ToString());
		}

		[Fact]
		public void Parse_ReadsInputAndReset()
		{
			IngestionOptions options = IngestionOptions.Parse(
				[
					"--input",
					"sample.ndjson",
					"--postgres",
					"Host=localhost;Database=entpoint",
					"--mongo",
					"mongodb://localhost:27017",
					"--mongo-database",
					"entpoint",
					"--reset"
				]);

			Assert.Equal("sample.ndjson", options.InputPath);
			Assert.True(options.Reset);
		}
	}
}
