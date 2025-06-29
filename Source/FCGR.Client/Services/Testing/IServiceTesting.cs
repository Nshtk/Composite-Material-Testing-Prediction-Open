using System;
using System.Threading.Tasks;

using FCGR.Common.Libraries.Models.Processors.Testing;

namespace FCGR.Client.Services.Testing;

public interface IServiceTesting : IService, IDisposable
{
	public abstract Task sendTestingParameters(TestingProcessorAI crack_processor);
	public abstract Task sendDataAsync((float[] x, float[] y) x_y);
	public abstract Task<TestingProcessorAI.TestingProcessorAIResult?> receiveForecast();
}
