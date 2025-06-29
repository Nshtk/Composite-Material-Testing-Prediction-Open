using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;

using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

using FCGR.Common.Libraries.Models.Processors.Testing;
using FCGR.Proto.Testing;
using MemoryPack;
using System.IO;

namespace FCGR.Server.GRPC.Services.Testing;

public class ServiceTesting : TestingService.TestingServiceBase
{
	private TestingProcessorAI _testing_processor;	

	public ServiceTesting()
	{
	}

	public override Task<Empty> sendTestingParameters(TestingParametersRequest request, ServerCallContext context)
	{
		_testing_processor = MemoryPackSerializer.Deserialize<TestingProcessorAI>(request.TestingProcessor.ToByteArray());
		if (_testing_processor == null)
			throw new Exception("Deserialisation unsuccessfull");
		_testing_processor.loadModels($"Data{Path.DirectorySeparatorChar}Models{Path.DirectorySeparatorChar}");

		return Task.FromResult(new Empty());
	}
	public override async Task<Empty> sendData(IAsyncStreamReader<DataRequest> stream_request, ServerCallContext context)
	{
		DataRequest request;

		while (await stream_request.MoveNext(new CancellationToken()))
		{
			request = stream_request.Current;
			_testing_processor.addData((request.X.ToArray(), request.Y.ToArray()));
		}

		return new Empty();
	}
	public override Task<TestingProcessorResultReply> receiveForecast(Empty request, ServerCallContext context)
	{
		TestingProcessorAI.TestingProcessorAIResult result = _testing_processor.calculateForecast(5, true, false, false, false);

		return Task.FromResult(new TestingProcessorResultReply
		{
			TestingProcessorResult = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(result, new JsonSerializerOptions() { IncludeFields = true })),
		});
	}
}
