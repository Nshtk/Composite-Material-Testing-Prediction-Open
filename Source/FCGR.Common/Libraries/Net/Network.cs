using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Diagnostics;
using FCGR.Common.Utilities;

namespace FCGR.Common.Libraries.Net;

public static class Network
{
#region Definitions
#endregion
#region Fields
	internal static readonly ConcurrentDictionary<int, IPEndPoint> Dict_client_port_address_connected_to=new ConcurrentDictionary<int, IPEndPoint>();
	public static readonly ConcurrentBag<ushort> Ports_available = new ConcurrentBag<ushort>();
#endregion
	static Network()
	{
		ushort ports_range_start=5555;

		for(ushort i = ports_range_start; i<ports_range_start+16; i++)
			Ports_available.Add(i);
	}
#region Methods
	/// <summary>
	///		Gets IP address of this machine
	/// </summary>
	/// <returns></returns>
	public static IPAddress? getLocalIPAddress()
	{
		using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
		{
			try
			{
				socket.Connect("8.8.8.8", 65530);
			}
			catch(Exception ex)
			{

				Tracer.traceMessage(ex.Message, MESSAGE_SEVERITY.ERROR, Tracer.TRACE_FLAG.EXCEPTION);
				return null;
			}
			return (socket.LocalEndPoint as IPEndPoint).Address;
		}
	}
	/*public static bool addConnection(int client_port, IPEndPoint address_connected_to)
	{
		return Dict_client_port_address_connected_to.TryAdd(client_port, address_connected_to);
	}
	public static bool removeConnection(int client_port)
	{
		IPEndPoint? _;
		return Dict_client_port_address_connected_to.Remove(client_port, out _);
	}*/
	/*public static int? getAvailablePort()
	{
		int port;

		if(Ports_available.TryTake(out port))
			return port;
		return null;
	}*/
	#endregion
}