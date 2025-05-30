using System;
using System.Net;
using System.Net.Sockets;

using Grpc.Core;

using FCGR.Common.Libraries.Net.Networks;

namespace FCGR.Client.Clients;

public abstract class GRPCClient<T>
	where T : ClientBase
{
	#region Fields
	protected readonly Channel _channel;
	protected readonly T _client;
	#endregion
	public GRPCClient(IPEndPoint endpoint_connected_to)
	{
		string endpoint_connected_to_address_as_string = endpoint_connected_to.Address.ToString();
		
		if (endpoint_connected_to.AddressFamily==AddressFamily.InterNetworkV6)   //HACK TEMP see issue https://github.com/grpc/grpc/issues/38731
		{
			endpoint_connected_to_address_as_string = $"[{endpoint_connected_to_address_as_string}]";
		}
		_channel = new Channel(endpoint_connected_to_address_as_string, endpoint_connected_to.Port, ChannelCredentials.Insecure, new ChannelOption[] { new ChannelOption("grpc.max_send_message_length", TCPNetwork.Buffer_size_max), new ChannelOption("grpc.max_receive_message_length", TCPNetwork.Buffer_size_max) });
		_client = (T)Activator.CreateInstance(typeof(T), _channel); //uh nuh
	}
	#region Methods
	public virtual bool isConnectionActive()    //FIXME Check in Cloned if something was lost
	{
		return !(_channel.State == ChannelState.Shutdown || _channel.State == ChannelState.TransientFailure);
	}
	#endregion
}