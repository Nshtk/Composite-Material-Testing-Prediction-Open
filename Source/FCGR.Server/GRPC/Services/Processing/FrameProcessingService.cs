using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

using Emgu.CV;
using Emgu.CV.Cuda;

using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

using FCGR.Proto.Processing;
using FCGR.Common.Libraries.Models.Processors.Crack;
using FCGR.Common.Libraries.Serialization.Json.Converters;
using FCGR.Common.Utilities;

namespace FCGR.Server.GRPC.Services.Processing;

public class ServiceFrameProcessing : FrameProcessingService.FrameProcessingServiceBase
{
	private CrackProcessor _crack_processor;
	private List<Mat> _frames_queued_for_processing;
	private int frames_queued_for_processing_count_current = 0;

	public ServiceFrameProcessing()
	{
		/*if(CudaInvoke.HasCuda)	//TODO
		{
			
		}
		int cudaDeviceCount = CudaInvoke.GetDevice();*/
	}

	public override Task<Empty> sendProcessingParameters(ProcessingParametersRequest request, ServerCallContext context)
	{
		_crack_processor = JsonSerializer.Deserialize<CrackProcessor>(request.CrackProcessor.ToByteArray(), new JsonSerializerOptions() { Converters = { new CrackProcessorUTF8JsonConverter() }, IncludeFields = true });
		if (_crack_processor == null)
			throw new Exception("Serialisation unsuccessfull");
		else
			_frames_queued_for_processing = new (_crack_processor.Frame_Serie_Size);

		return Task.FromResult(new Empty());
	}
	public override async Task sendFrame(IAsyncStreamReader<FrameRequest> stream_request, IServerStreamWriter<CrackProcessorResultReply> stream_reply, ServerCallContext context)
	{
		FrameRequest request;

		while (stream_request.MoveNext(new CancellationToken()).Result)
		{
			request = stream_request.Current;
			_frames_queued_for_processing.Add(new Mat());
			CvInvoke.Imdecode(request.ToByteArray(), Emgu.CV.CvEnum.ImreadModes.Unchanged, _frames_queued_for_processing[frames_queued_for_processing_count_current]);
			frames_queued_for_processing_count_current++;

			if (frames_queued_for_processing_count_current >= _frames_queued_for_processing.Capacity)
			{
				CrackProcessor.Result result = _crack_processor.findCrackInFrameSerie(_frames_queued_for_processing);
				System.Type result_type_actual = result.GetType();

				await stream_reply.WriteAsync(new CrackProcessorResultReply		//FIXME If user sets frames number in serie too high a payload can exceed max allowed by GRPC message size
				{
					CrackProcessorResult = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(result, result_type_actual, new JsonSerializerOptions() { IncludeFields = true, Converters = { new CrackProcessorResultUTF8JsonConverter() } })),
				});
				_frames_queued_for_processing.Clear();
				frames_queued_for_processing_count_current = 0;
			}
		}
	}
	public override Task<CrackProcessorResultReply> sendFrameSerie(FrameSerieRequest request, ServerCallContext context)
	{
		List<Mat> frames = new (request.Frames.Count);
		CrackProcessor.Result result;

		for (int i = 0; i < request.Frames.Count; i++)
		{
			frames.Add(new Mat());
			CvInvoke.Imdecode(request.Frames[i].ToByteArray(), Emgu.CV.CvEnum.ImreadModes.Unchanged, frames[i]);
		}
		result = _crack_processor.findCrackInFrameSerie(frames);
		
		System.Type result_type_actual = result.GetType();
		try
		{
			return Task.FromResult(new CrackProcessorResultReply
			{
				CrackProcessorResult = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(result, result_type_actual, new JsonSerializerOptions() { IncludeFields = true, Converters = { new CrackProcessorResultUTF8JsonConverter() } })),
			});
		}
		finally
		{
			frames.DisposeElements();
		}
	}
}
