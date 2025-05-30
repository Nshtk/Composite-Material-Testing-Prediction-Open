using System;

using FCGR.Common.Libraries.Net.Services;

namespace FCGR.Client.Services;

public interface IService
{
	public Contracts.SERVICES Service
	{
		get;
	}
	public int Connection_Failures_Count
	{
		get;
		set;
	}
	public int Connection_Failures_Limit
	{
		get;
		set;
	}
}
