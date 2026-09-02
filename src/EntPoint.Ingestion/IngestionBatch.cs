using EntPoint.Core;

namespace EntPoint.Ingestion
{
	internal sealed record IngestionBatch(
		IReadOnlyList<NormalizedSecurityEvent> Events,
		IReadOnlyList<NormalizedSecurityEvent> Alerts);
}
