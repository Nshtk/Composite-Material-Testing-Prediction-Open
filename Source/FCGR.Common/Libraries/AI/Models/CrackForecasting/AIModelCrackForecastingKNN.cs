using System.Collections.Generic;
using System.Linq;

using Microsoft.ML;
using Microsoft.ML.Data;

namespace FCGR.Common.Libraries.AI.Models.CrackForecasting;

public class AIModelCrackForecastingKNN : AIModelBaseMLNet
{
	public class MultivariateSeriesData
	{
		[VectorType(24 * 20)]
		public float[] Features
		{
			get;
			set;
		}

		[VectorType(12 * 3)]
		public float[] Forecast
		{
			get;
			set;
		}
	}
	private class ClusterPrediction
	{
		[ColumnName("PredictedLabel")]
		public uint ClusterId { get; set; }

		[ColumnName("Score")]
		public float[] Distances { get; set; }
	}

	private MLContext _ml_context;
	private ITransformer _model;
	private int _window_size;
	private int _horizon;
	private int _clusters;

	public AIModelCrackForecastingKNN(string name, int windowSize, int horizon, int clusters) : base(name)
	{
		_ml_context = new MLContext();
		_window_size = windowSize;
		_horizon = horizon;
		_clusters = clusters;
	}

	public void train(List<(float[][] x, float[][] y)> x_y)
	{
		var samples = new List<MultivariateSeriesData>();
		foreach (var (x, y) in x_y)
		{
			for (int i = 0; i < x.Length - _window_size - _horizon; i++)
			{
				samples.Add(new MultivariateSeriesData
				{
					Features = x.Skip(i).Take(_window_size).SelectMany(v => v).ToArray(),
					Forecast = y.Skip(i + _window_size).Take(_horizon).SelectMany(v => v).ToArray()
				});
			}
		}
		IDataView dataView = _ml_context.Data.LoadFromEnumerable(samples);
		var pipeline = _ml_context.Transforms.Concatenate("Features", nameof(MultivariateSeriesData.Features)).Append(_ml_context.Clustering.Trainers.KMeans(featureColumnName: "Features", numberOfClusters: _clusters));
		_model = pipeline.Fit(dataView);
	}

	public double test(List<(float[][] x, float[][] y)> x_y_test)
	{
		var testSamples = new List<MultivariateSeriesData>();

		foreach (var (x, y) in x_y_test)
		{
			for (int i = 0; i < x.Length - _window_size - _horizon; i++)
			{
				testSamples.Add(new MultivariateSeriesData
				{
					Features = x.Skip(i).Take(_window_size).SelectMany(v => v).ToArray(),
					Forecast = y.Skip(i + _window_size).Take(_horizon).SelectMany(v => v).ToArray()
				});
			}
		}
		IDataView testData = _ml_context.Data.LoadFromEnumerable(testSamples);
		var predictions = _model.Transform(testData);
		var metrics = _ml_context.Regression.Evaluate(predictions, labelColumnName: nameof(MultivariateSeriesData.Forecast), scoreColumnName: "Features");  // Note: K-Means outputs cluster IDs, not direct forecasts

		return metrics.RootMeanSquaredError;
	}

	public float[][] predict(float[][] historicalData)
	{
		var input = new MultivariateSeriesData
		{
			Features = historicalData.Take(_window_size).SelectMany(v => v).ToArray()
		};

		var engine = _ml_context.Model.CreatePredictionEngine<MultivariateSeriesData, ClusterPrediction>(_model);
		var prediction = engine.Predict(input);

		float[][] forecast = new float[_horizon][];
		for (int i = 0; i < _horizon; i++)
			forecast[i] = new float[historicalData[0].Length];

		return forecast;
	}
}
