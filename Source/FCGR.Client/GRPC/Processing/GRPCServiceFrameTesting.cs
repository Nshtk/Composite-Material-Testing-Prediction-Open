using System;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Text.Json;
using System.Collections.Generic;

using Grpc.Core;
using Google.Protobuf;

using FCGR.Client.Clients;
using FCGR.Common.Libraries.Net.Services;
using FCGR.Client.Services;
using FCGR.Proto.Testing;
using FCGR.Client.Services.Testing;
using Google.Protobuf.WellKnownTypes;
using FCGR.Common.Libraries.Models.Processors.Testing;
using MemoryPack;

namespace FCGR.Client.GRPC.Streaming;

public sealed class GRPCServiceTesting : GRPCClient<TestingService.TestingServiceClient>, IServiceTesting
{
	#region Fields
	private TestingParametersRequest _processing_parameters_request = new();
	private DataRequest _data_request = new DataRequest();
	private AsyncClientStreamingCall<DataRequest, Empty>? _streaming_call_send_data;
	#endregion
	#region Properties
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
	Contracts.SERVICES IService.Service
	{
		get;
	}
	#endregion
	public GRPCServiceTesting(IPEndPoint address_connected_to) : base(address_connected_to)
	{
		_streaming_call_send_data = _client.sendData();
	}
	~GRPCServiceTesting()
	{
		dispose(false);
	}
	#region Methods
	public async Task sendTestingParameters(TestingProcessorAI testing_processor)
	{
		_processing_parameters_request.TestingProcessor = ByteString.CopyFrom(MemoryPackSerializer.Serialize(testing_processor));
		await _client.sendTestingParametersAsync(_processing_parameters_request);
	}
	public async Task sendDataAsync((float[] x, float[] y) data)
	{
		_data_request.X.Clear();
		_data_request.Y.Clear();
		_data_request.X.AddRange(data.x);
		_data_request.Y.AddRange(data.y);
		await _streaming_call_send_data.RequestStream.WriteAsync(_data_request);
	}
	public async Task<TestingProcessorAI.TestingProcessorAIResult?> receiveForecast()
	{
		TestingProcessorAI.TestingProcessorAIResult? result = null;

		var reply=await _client.receiveForecastAsync(new Empty());
		byte[] result_serialized=reply.TestingProcessorResult.ToByteArray();
		result = JsonSerializer.Deserialize<TestingProcessorAI.TestingProcessorAIResult>(result_serialized, new JsonSerializerOptions() { IncludeFields=true});

		return result;
	}
	private void dispose(bool is_explicit)
	{
		if(is_explicit)
		{

		}
		_channel.ShutdownAsync().Wait();
		_streaming_call_send_data.RequestStream.CompleteAsync().Wait();
	}
	public void Dispose()
	{
		dispose(true);
		GC.SuppressFinalize(this);
	}
	#endregion
}