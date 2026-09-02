using System.Text.Json;
using EntPoint.Core;

namespace EntPoint.Collector
{
	internal sealed class NdjsonEventWriter : IAsyncDisposable
	{
		private readonly StreamWriter _writer;

		private NdjsonEventWriter(StreamWriter writer)
		{
			_writer = writer;
		}

		public static NdjsonEventWriter Open(string outputPath)
		{
			string fullPath = Path.GetFullPath(outputPath);
			string directory = Path.GetDirectoryName(fullPath)
				?? throw new ArgumentException("Output path must include a valid directory.", nameof(outputPath));

			Directory.CreateDirectory(directory);
			FileStream stream = new FileStream(
				fullPath,
				FileMode.Append,
				FileAccess.Write,
				FileShare.Read,
				bufferSize: 4096,
				useAsync: true);

			return new NdjsonEventWriter(new StreamWriter(stream));
		}

		public async Task WriteAsync(
			NormalizedSecurityEvent securityEvent,
			CancellationToken cancellationToken)
		{
			string json = JsonSerializer.Serialize(securityEvent, SecurityEventJson.Options);
			await _writer.WriteLineAsync(json.AsMemory(), cancellationToken);
			await _writer.FlushAsync(cancellationToken);
		}

		public ValueTask DisposeAsync() => _writer.DisposeAsync();
	}
}
