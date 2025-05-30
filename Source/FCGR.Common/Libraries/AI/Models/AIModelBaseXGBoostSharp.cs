using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

using XGBoostSharp;

using FCGR.Common.Utilities;

namespace FCGR.Common.Libraries.AI.Models;

public class AIModelBaseXGBoostSharp<TModel> : IDisposable
	where TModel : XGBModelBase
{
	#region Definitions
	public abstract class DataLoader
	{
		#region Definitions
		public struct Augmentation
		{
			[Flags]
			public enum TYPE
			{
				NONE = 0,
			}

			public TYPE augmentation_type;
			public int augmented_items_per_item = -1;

			public Augmentation(int augmented_items_per_item)
			{
				this.augmented_items_per_item = augmented_items_per_item;
			}
		}
		#endregion
		public DataLoader()
		{

		}
		#region Methods
		public abstract Task<List<(float[][] x, float[][] y)>> loadDataAsync(string path, string input_file_name, string label_file_name, int batch_size, float fraction_data_to_read = 1f, int data_part_size = 0, int data_read_offset = 0, bool is_reading_in_random_order = false, Dictionary<string, bool> directory_names_to_ignore = null, Augmentation augmentation_parameters = default);
		public (List<(float[][] x, float[][] y)>, List<(float[][] x, float[][] y)>) split(List<(float[][] x, float[][] y)> data, float data_proportion_1, float data_proportion_2)
		{
			List<(float[][] x, float[][] y)> data_part_1 = new(), data_part_2 = new();

			int i;
			for (i = 0; i < data.Count * data_proportion_1; i++)
				data_part_1.Add(data[i]);
			int end = Math.Min((int)(i + data.Count * data_proportion_2), data.Count);
			for (; i < end; i++)
				data_part_2.Add(data[i]);

			return (data_part_1, data_part_2);
		}
		public void shuffle(ref List<(float[][] x, float[][] y)> data)
		{
			int data_size = data.Count;

			for (int i = 0, j; i < data_size - 1; i++)
			{
				j = Random.Shared.Next(i, data_size);
				if (j != i)
				{
					(float[][] x, float[][] y) temp = data[i];
					data[i] = data[j];
					data[j] = temp;
				}
			}
		}
		#endregion
	}
	#endregion
	#region Fields
	protected XGBRegressor[] _models;   ///Can't use base class <c cref="XGBModelBase"/> because it does not implement Fit() and has no overloads for other methods.
	TargetTransformer transformer = new TargetTransformer();
	#endregion
	#region Properties
	public string Model_Name
	{
		get;
	}
	public int Target_Variables_Count
	{
		get { return _models.Length; }
	}
	#endregion
	public AIModelBaseXGBoostSharp(string name, int target_variables_count, int estimators_count, int tree_depth_max, float learning_rate = 0.1f, string? log_path = null)
	{
		Model_Name = name;
		_models = new XGBRegressor[target_variables_count];
		for (int i = 0; i < target_variables_count; i++)
			_models[i] = new XGBRegressor(estimators_count, tree_depth_max, learningRate: learning_rate, objective: "reg:absoluteerror", device: "gpu");    //reg:absoluteerror
	}
	~AIModelBaseXGBoostSharp()
	{
		dispose(false);
	}
	#region Methods
	public float[][] predict(float[][] x)
	{
		float[][] ys = new float[_models.Length][];

		for (int i = 0; i < _models.Length; i++)
			ys[i] = _models[i].Predict(x);

		/*ys[0] = transformer.InverseTransformTarget0(ys[0]);
		transformer.InverseTransformLog(ys[1]);
		transformer.InverseTransformLog(ys[2]);*/

		Helper.swapDimensions(ys);
		
		return ys;
	}
	public void train((float[][] x, float[][] y) data)
	{
		float[][] ys = new float[_models.Length][];     //Track issue in XGBoostSharp
		for (int i = 0; i < Target_Variables_Count; i++)
		{
			ys[i] = new float[data.y.Length];
			for (int j = 0; j < data.y.Length; j++)
				ys[i][j] = data.y[j][i];
		}
		//ys = transformer.Preprocess(ys, useMinMax: true);
		for (int i = 0; i < ys.Length; i++)
		{
			_models[i].Fit(data.x, ys[i]);
		}
	}
	public void test((float[][] x, float[][] y) data, Func<float, float, float> loss_func)
	{
		var predictions = predict(data.x);
		float[] loss_averages = new float[predictions.Length];
		float[] accuracy_averages = new float[predictions.Length];

		for (int i = 0; i < predictions[0].Length; i++)
		{
			for (int j = 0; j < predictions.Length; j++)
			{
				loss_averages[j] += loss_func(data.y[i][j], predictions[j][i]);
				accuracy_averages[j] += MathF.Abs(data.y[i][j] - predictions[j][i]);
			}
		}
		for (int i = 0; i < loss_averages.Length; i++)
		{
			Console.WriteLine($"Average loss: {loss_averages[i] / predictions[0].Length}");
			Console.WriteLine($"Average accuracy: {(accuracy_averages[i] / predictions[0].Length).ToString("0.00####")}");
		}
	}
	public Dictionary<string, float>[] getFeatureImportance(string importance_type)
	{
		Dictionary<string, float>[] dicts_feature_importance = new Dictionary<string, float>[_models.Length];

		for (int i = 0; i < _models.Length; i++)
			dicts_feature_importance[i] = _models[i].GetFeatureImportance(Parameters.ImportanceType.Weight);

		return dicts_feature_importance;
	}
	public virtual bool load(string path_models, string file_extension = ".json")
	{
		for (int i = 0; i < _models.Length; i++)
		{
			try
			{
				_models[i] = XGBRegressor.LoadFromFile($"{path_models}{Model_Name}-{i}{file_extension}");
			}
			catch (Exception e)
			{
				return false;
			}
		}
		return true;
	}
	public virtual bool save(string path)
	{
		if (!Directory.Exists(path))
			Directory.CreateDirectory(path);

		string model_file_name_full = $"{path}{Path.DirectorySeparatorChar}{Model_Name}";
		try
		{
			for (int i = 0; i < _models.Length; i++)
				_models[i].SaveModelToFile($"{model_file_name_full}-{i}.json");
		}
		catch (UnauthorizedAccessException e)
		{
			Tracer.traceMessage(e.Message);
			return false;
		}

		return true;
	}
	protected virtual void dispose(bool is_explicit)
	{
		if (is_explicit)
		{
		}
		for (int i = 0; i < _models.Length; i++)
			_models[i].Dispose();
	}
	public void Dispose()
	{
		dispose(is_explicit: true);
		GC.SuppressFinalize(this);
	}

	public (float[][] x, float[][] y) prepareData(List<(float[][], float[][])> rawFiles, int windowSize, int forecastHorizon)
	{
		var features = new List<float[]>();
		var targets = new List<float[]>();

		foreach (var (x, y) in rawFiles)
		{
			int numTimesteps = x.Length;
			int numFeatures = x[0].Length;
			int numTargets = y[0].Length;

			for (int i = 0; i <= numTimesteps - windowSize - forecastHorizon; i++)
			{
				// Input window: [windowSize × numFeatures]
				var inputWindow = new float[windowSize][];
				for (int w = 0; w < windowSize; w++)
					inputWindow[w] = x[i + w];

				var flatInput = inputWindow.SelectMany(r => r).ToArray();

				// Output horizon: [forecastHorizon × numTargets]
				var outputWindow = new float[forecastHorizon][];
				for (int h = 0; h < forecastHorizon; h++)
					outputWindow[h] = y[i + windowSize + h];

				var flatOutput = outputWindow.SelectMany(r => r).ToArray();

				features.Add(flatInput);
				targets.Add(flatOutput);
			}
		}

		return (features.ToArray(), targets.ToArray());
	}
	#endregion
}

public static class MinMaxScaler
{
	public static float Transform(float value, float min, float max, float newMin = 0f, float newMax = 1f)
	{
		if (max == min) return (newMin + newMax) / 2f;
		return ((value - min) / (max - min)) * (newMax - newMin) + newMin;
	}

	public static float[] Transform(float[] values, float newMin = 0f, float newMax = 1f)
	{
		float min = values.Min();
		float max = values.Max();

		return values.Select(v => Transform(v, min, max, newMin, newMax)).ToArray();
	}

	public static float InverseTransform(float scaled, float min, float max, float newMin = 0f, float newMax = 1f)
	{
		if (max == min) return min;
		return ((scaled - newMin) / (newMax - newMin)) * (max - min) + min;
	}
}

public static class StandardScaler
{
	public static float Transform(float value, float mean, float stdDev)
	{
		if (stdDev == 0) return 0;
		return (value - mean) / stdDev;
	}

	public static float[] Transform(float[] values, float mean, float stdDev)
	{
		return values.Select(v => Transform(v, mean, stdDev)).ToArray();
	}

	public static float InverseTransform(float scaled, float mean, float stdDev)
	{
		if (stdDev == 0) return mean;
		return scaled * stdDev + mean;
	}
}

public class TargetTransformer
{
	private (float min, float max)? _minMaxParams;
	private (float mean, float stdDev)? _standardParams;
	private bool _useMinMax;

	public float[][] Preprocess(float[][] targets, bool useMinMax = true)
	{
		_useMinMax = useMinMax;
		var result = new float[targets.Length][];

		// Normalize target 0
		if (useMinMax)
		{
			var min = targets[0].Min();
			var max = targets[0].Max();
			_minMaxParams = (min, max);
			result[0] = targets[0].Select(v => MinMaxScaler.Transform(v, min, max)).ToArray();
		}
		else
		{
			var mean = (float)targets[0].Average();
			var stdDev = (float)Math.Sqrt(targets[0].Select(v => (v - mean) * (v - mean)).Average());
			_standardParams = (mean, stdDev);
			result[0] = targets[0].Select(v => StandardScaler.Transform(v, mean, stdDev)).ToArray();
		}

		// Apply log to targets 1 and 2
		for (int i = 1; i <= 2 && i < targets.Length; i++)
		{
			result[i] = new float[targets[i].Length];
			for (int j = 0; j < targets[i].Length; j++)
			{
				float val = targets[i][j];
				if (val <= 0f)
				{
					// Optional: Add epsilon to avoid log(0)
					val = Math.Max(val, 1e-6f);
				}
				result[i][j] = (float)Math.Log(val);
			}
		}

		return result;
	}

	public float[] InverseTransformTarget0(float[] transformedValues)
	{
		var original = new float[transformedValues.Length];
		if (_useMinMax)
		{
			var (min, max) = _minMaxParams.Value;
			for (int i = 0; i < original.Length; i++)
			{
				original[i] = MinMaxScaler.InverseTransform(transformedValues[i], min, max);
			}
		}
		else
		{
			var (mean, stdDev) = _standardParams.Value;
			for (int i = 0; i < original.Length; i++)
			{
				original[i] = StandardScaler.InverseTransform(transformedValues[i], mean, stdDev);
			}
		}
		return original;
	}

	public void InverseTransformLog(float[] transformedValues)
	{
		for (int i = 0; i < transformedValues.Length; i++)
		{
			transformedValues[i] = (float)Math.Exp(transformedValues[i]);
		}
	}
}