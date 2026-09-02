namespace EntPoint.Core
{
	public sealed record SimulationOptions(
		int InitialProcessCount = 8,
		double AlertPercentage = 3,
		int? Seed = null)
	{
		public void Validate()
		{
			if (InitialProcessCount < 1)
			{
				throw new ArgumentOutOfRangeException(
					nameof(InitialProcessCount),
					"At least one initial process is required.");
			}

			if (AlertPercentage is < 0 or > 100)
			{
				throw new ArgumentOutOfRangeException(
					nameof(AlertPercentage),
					"Alert percentage must be between 0 and 100.");
			}
		}
	}
}
