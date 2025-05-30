using System;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;

using FCGR.Proto.Streaming;

namespace FCGR.Server.GRPC.Services.Streaming;

public class ServiceFrameStreaming : FrameStreamingService.FrameStreamingServiceBase
{
    public override async Task sendFrame(IAsyncStreamReader<FrameRequest> stream_request, IServerStreamWriter<FrameReply> stream_reply, ServerCallContext context)
    {
        while (stream_request.MoveNext(new CancellationToken()).Result)
        {
            FrameRequest request = stream_request.Current;
            await stream_reply.WriteAsync(new FrameReply
            {
                Frame = request.Frame,
            }); 
        }
    }
}
