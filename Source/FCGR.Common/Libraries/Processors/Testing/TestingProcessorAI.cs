using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FCGR.Common.Libraries.AI.Models.CrackForecasting;
using FCGR.Common.Utilities;

namespace FCGR.Common.Libraries.Models.Processors.Testing;

public class TestingProcessorAI : Model
{
	public readonly int lags_count;
	private Queue<(float[], float[])> _x_y_received;
	private AIModelCrackForecastingGradientBoosting _ai_model_crack_forecasting_gradient_boosting = new("crack-forecasting-gradient-boosting-model", 3, 1000, 6, learning_rate: 0.1f);
	private int _forecast_horizon=1;

	public int Forecast_Horizon
	{
		get { return _forecast_horizon; }
		set { _forecast_horizon = value; OnPropertyChanged(); }
	}

	public TestingProcessorAI(string path_model_load)
	{
		if (!_ai_model_crack_forecasting_gradient_boosting.load(path_model_load))
			throw new Exception("Model was not loaded.");
		_x_y_received = new(lags_count);
	}
	public void addData((float[] x, float[] y) x_y)
	{
		while (_x_y_received.Count > lags_count)
			_x_y_received.Dequeue();
		_x_y_received.Enqueue(x_y);
	}
	
	public (int N_predictions_start, float[][] predictions) calculateForecast(int lags_count, bool is_calculating_proportions = true, bool is_calculating_mean = true, bool is_calculating_std = true, bool is_calculating_trend_slope = true)
	{
		List<float[]> xs = new (lags_count);
		List<float[]> ys = new (lags_count);
		float[][] ys_predicted = new float[Forecast_Horizon][];
		int N_predictions_start;

		for (int i = 0; i < lags_count; )
		{
			if (_x_y_received.TryDequeue(out (float[] x, float[] y) x_y))
			{
                xs.Add(x_y.x);
				ys.Add(x_y.y);
				i++;
			}
		}
		N_predictions_start = (int)xs[^1][19]+1;

		for (int i=0; i<Forecast_Horizon; i++)
		{
			float[] x = new float[xs[0].Length];
			Buffer.BlockCopy(xs[^1], 0, x, 0, sizeof(float) * xs[0].Length);
			x[19]= x[19] + i+1;
			AIModelCrackForecastingGradientBoosting.CrackForecastingDataLoader.prepareX(ref x, ys, is_calculating_proportions, is_calculating_mean, is_calculating_std, is_calculating_trend_slope);
			var y_predicted_jagged= _ai_model_crack_forecasting_gradient_boosting.predict(new float[][] { x });
			ys_predicted[i] = new float[y_predicted_jagged.Length];
			for (int j = 0; j < y_predicted_jagged.Length; j++)
				ys_predicted[i][j] = y_predicted_jagged[j][0];
			ys.RemoveAt(0);
			ys.Add(ys_predicted[i]);
		}

		return (N_predictions_start, ys_predicted);
	}
}
