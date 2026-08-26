using System;

namespace Feeder.McpPlugin.Common.Model;

public class RequestListResourceTemplates : IRequestID, IDisposable
{
	public string RequestID { get; set; } = string.Empty;

	public RequestListResourceTemplates()
	{
	}

	public RequestListResourceTemplates(string requestID)
	{
		RequestID = requestID ?? throw new ArgumentNullException("requestID");
	}

	public virtual void Dispose()
	{
	}

	~RequestListResourceTemplates()
	{
		Dispose();
	}
}
