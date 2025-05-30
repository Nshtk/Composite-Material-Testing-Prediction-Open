using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Net;

using Emgu.CV;

using Grpc.Core;
using Google.Protobuf;

using FCGR.Proto.Streaming;
using FCGR.Client.Clients;
using FCGR.Client.Services.Streaming;
using FCGR.Common.Libraries.Net.Services;
using FCGR.Client.Services;
using Emgu.CV.CvEnum;

namespace FCGR.Client.GRPC.Streaming;

public sealed class GRPCServiceFrameStreaming : GRPCClient<FrameStreamingService.FrameStreamingServiceClient>, IServiceFrameStreaming
{
#region Fieds
	private FrameRequest _stream_frame_request = new FrameRequest();
	private AsyncDuplexStreamingCall<FrameRequest, FrameReply>? _streaming_call;
	public ConcurrentQueue<byte[]> frames_received = new ConcurrentQueue<byte[]>();
	#endregion
	#region Properties
	public int Frame_Rate
	{
		get;
		set;
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
	Contracts.SERVICES IService.Service
	{
		get;
	}
	#endregion
	public GRPCServiceFrameStreaming(IPEndPoint address_connected_to) : base(address_connected_to)
	{
		
	}
	~GRPCServiceFrameStreaming()
	{
		dispose(false);
	}
	#region Methods
	public async Task sendFrameAsync(Mat frame)
	{
		_stream_frame_request.Frame = ByteString.CopyFrom(CvInvoke.Imencode(".jpg", frame));
		_streaming_call = _client.sendFrame();
		await _streaming_call.RequestStream.WriteAsync(_stream_frame_request);
		await _streaming_call.RequestStream.CompleteAsync();
	}
	public async Task<Mat?> receiveFrame()
	{
		Mat frame = new Mat();

		if(await _streaming_call.ResponseStream.MoveNext(CancellationToken.None))
		{
			byte[]? frame_as_bytes = _streaming_call.ResponseStream.Current.Frame.ToByteArray();
			CvInvoke.Imdecode(frame_as_bytes, ImreadModes.Unchanged, frame);
		}

		return frame;
	}

	private void dispose(bool is_explicit)
	{
		if(is_explicit)
			_channel.ShutdownAsync().Wait();
		frames_received=null;
	}
	public void Dispose()
	{
		dispose(true);
		GC.SuppressFinalize(this);
	}
	#endregion
}