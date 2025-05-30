using System;
using System.Collections.Generic;
using System.IO;
using FCGR.Common.Utilities;
using Force.DeepCloner;
using SharpLearning.Containers.Extensions;
using SharpLearning.RandomForest.Learners;
using SharpLearning.RandomForest.Models;

namespace FCGR.Common.Libraries.AI.Models;


public class AIModelBaseSharpLearning<TModel> : IDisposable
{
	RegressionRandomForestLearner learner;
	protected List<RegressionForestModel> _models = new();   ///Can't use base class <c cref="XGBModelBase"/> because it does not implement Fit() and has no overloads for other methods.

	public string Model_Name
	{
		get;
	}
	public AIModelBaseSharpLearning(string name, int estimators_count, int tree_depth_max, string? log_path = null)
	{
		Model_Name = name;
		learner = new RegressionRandomForestLearner(estimators_count, maximumTreeDepth: tree_depth_max);
	}
	~AIModelBaseSharpLearning()
	{
		dispose(false);
	}
	public bool train((float[][] x, float[][] y) data)
	{
		float[][] ys = new float[data.y[0].Length][];       //Track issue in XGBoostSharp
		for (int i = 0; i < ys.Length; i++)
		{
			ys[i] = new float[data.y.Length];
			for (int j = 0; j < data.y.Length; j++)
				ys[i][j] = data.y[j][i];
		}
		if (data.y.Length != _models.Count)
		{
			var data_x_as_double = Array.ConvertAll(data.x, row => Array.ConvertAll(row, x => (double)x));
			for (int i = 0; i < ys.Length; i++)
			{
				_models.Add(learner.Learn(data_x_as_double, Array.ConvertAll(ys[i], x => (double)x)));
			}
		}

		return true;
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
	public float[][] predict(float[][] x)
	{
		float[][] ys = new float[_models.Count][];
		var x_as_double = Array.ConvertAll(x, row => Array.ConvertAll(row, x => (double)x));

		for (int i = 0; i < _models.Count; i++)
			ys[i] = Array.ConvertAll(_models[i].Predict(x_as_double.ToF64Matrix()), x => (float)x);

		return ys;
	}
	public virtual bool load(string model_file_name_template_full, int files_count, string file_extension = ".json")
	{
		_models.Clear();
		for (int i = 0; i < files_count; i++)
		{
			try
			{
				_models.Add(RegressionForestModel.Load(() => new StreamReader($"{model_file_name_template_full}-{i}{file_extension}")));
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
		/*if (!Directory.Exists(path))
			Directory.CreateDirectory(path);

		string model_file_name_full = $"{path}{Path.DirectorySeparatorChar}{Model_Name}";
		try
		{
			for (int i = 0; i < _models.Count; i++)
				_models[i].Save(() => new StreamWriter($"{model_file_name_full}-{i}.xml"));
		}
		catch (UnauthorizedAccessException e)
		{
			Tracer.traceMessage(e.Message);
			return false;
		}*/

		return true;
	}
	public Dictionary<string, double>[] getFeatureImportance(Dictionary<string, int> dict_feature_name_id)
	{
		Dictionary<string, double>[] dicts_feature_importance = new Dictionary<string, double>[_models.Count];

		for (int i = 0; i < _models.Count; i++)
			dicts_feature_importance[i] = _models[i].GetVariableImportance(dict_feature_name_id);

		return dicts_feature_importance;
	}
	protected virtual void dispose(bool is_explicit)
	{
		if (is_explicit)
		{
		}
		//for (int i = 0; i < _models.Count; i++)
		//	_models[i].Dispose();
	}
	public void Dispose()
	{
		dispose(is_explicit: true);
		GC.SuppressFinalize(this);
	}
}