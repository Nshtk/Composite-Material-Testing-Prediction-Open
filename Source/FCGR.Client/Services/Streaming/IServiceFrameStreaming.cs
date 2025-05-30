using System;
using System.Threading.Tasks;

using Emgu.CV;

namespace FCGR.Client.Services.Streaming;

public interface IServiceFrameStreaming : IService, IDisposable
{
	public int Frame_Rate
	{
		get;
		set;
	}

	public abstract Task sendFrameAsync(Mat frame);
	public abstract Task<Mat?> receiveFrame();
}
