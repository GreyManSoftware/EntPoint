namespace EntPoint.Core
{
	public sealed record NormalizationResult(
		NormalizedSecurityEvent? Event,
		string? RejectionReason)
	{
		public bool IsAccepted => Event is not null;

		public static NormalizationResult Accepted(NormalizedSecurityEvent securityEvent) =>
			new(securityEvent, null);

		public static NormalizationResult Rejected(string reason) =>
			new(null, reason);
	}
}
