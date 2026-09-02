using EntPoint.Core;

namespace EntPoint.Persistence
{
	public sealed record EndpointDescriptor(
		Guid EndpointId,
		EndpointOperatingSystem OperatingSystem);
}
