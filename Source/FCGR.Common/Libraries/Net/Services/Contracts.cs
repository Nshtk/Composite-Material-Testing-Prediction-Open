using System;

namespace FCGR.Common.Libraries.Net.Services;

public sealed record Contracts
{
	/// <summary>
	///		Service ids which are sent with every packet.
	/// </summary>
	public enum SERVICES
	{
		GENERAL,
		FRAME_STREAMING,
		FRAME_PROCESSING,
	}
	/// <summary>
	///		Service-specific contracts.
	/// </summary>
	public enum FRAME_STREAMING
	{
		FRAME,
	}
	public enum FRAME_PROCESSING
	{
		CRACK_PROCESSOR,
		FRAME,
		RESULT,
		RESULT_FRAME_HIGHLIGHTED_CRACK,
	}
	private Contracts()
	{}
}
