using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Emgu.CV;

using FCGR.Common.Libraries.Models.Processors.Crack;

namespace FCGR.Client.Services.Processing;

public interface IServiceFrameProcessing : IService, IDisposable
{
	public int Frame_Rate
	{
		get;
		set;
	}

	public abstract Task sendProcessingParameters(CrackProcessor crack_processor, bool is_processing_in_real_time);
	public abstract Task sendFrameAsync(Mat frame);
	public abstract Task<CrackProcessor.Result?> sendFrameSerieAsync(List<Mat> frames);
	public abstract Task<CrackProcessor.Result?> receiveResult();
}
