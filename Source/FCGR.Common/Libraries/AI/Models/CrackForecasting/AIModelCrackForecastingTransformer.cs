using System;

using TorchSharp;

using FCGR.Common.Libraries.AI.Modules;
using FCGR.Common.Libraries.AI.Models;

namespace FCGR.Common.Libraries.AI.Models.CrackForecasting;

public class AIModelCrackForecastingTransformer : AIModelBaseTorchSharp<CrackForecastingNet>
{
	public int Forecasting_Horizon
	{
		get {return _model.forecasting_horizon;}
	}
	public int Timesteps_Count
	{
		get {return _model.timesteps_count;}
	}
	
	public AIModelCrackForecastingTransformer(string name, long batch_size, DeviceType device_type, string? log_path = null) : base(name, batch_size, device_type, log_path)
	{
		_model =new CrackForecastingNet(Device);
		x_shape = new long[] { batch_size, _model.timesteps_count, _model.features_number };
		y_shape = new long[] { batch_size, _model.forecasting_horizon, _model.output_variables_count };
		initialise(device_type);
	}
}