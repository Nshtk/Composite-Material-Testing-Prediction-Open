using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using XGBoostSharp;

using FCGR.Common.Utilities;
using FCGR.Common.Libraries.DataFiles;

namespace FCGR.Common.Libraries.AI.Models.CrackForecasting;

public class AIModelCrackForecastingGradientBoosting : AIModelBaseXGBoostSharp<XGBRegressor>
{
	public class CrackForecastingDataLoader : DataLoader
	{
		private readonly float _downsampling_factor;
		private readonly int _lag_window_size;
		private readonly bool _is_calculating_mean;
		private readonly bool _is_calculating_std;
		private readonly bool _is_calculating_trend_slope;
		private readonly bool _is_calculating_difference;
		private readonly bool _is_calculating_proportions;
		private readonly bool _is_removing_nan;
		public readonly int _columns_calculated_count_x;
		private readonly List<int>? _column_calculate_lag_ids;

		public CrackForecastingDataLoader(List<int>? column_calculated_ids = null, float downsampling_factor = 1f, int lag_window_size = 0, bool is_calculating_proportions = true, bool is_calculating_mean = false, bool is_calculating_std = false, bool is_calculating_trend_slop = false, bool is_calculating_difference = false, bool is_removing_nan=false)
		{
			_column_calculate_lag_ids = column_calculated_ids;
			if (column_calculated_ids != null)
			{
				_downsampling_factor = downsampling_factor;
				_lag_window_size = lag_window_size;
				_is_calculating_mean = is_calculating_mean;
				_is_calculating_std = is_calculating_std;
				_is_calculating_trend_slope = is_calculating_trend_slop;
				_is_calculating_difference = is_calculating_difference;
				_is_calculating_proportions = is_calculating_proportions;
				_is_removing_nan=is_removing_nan;
				_columns_calculated_count_x = _is_calculating_proportions.toInt() * 2 + (_lag_window_size + _is_calculating_mean.toInt() + _is_calculating_std.toInt() + _is_calculating_trend_slope.toInt()) * _column_calculate_lag_ids.Count;
			}
		}
		private static void calculateProportions(float[] x)
		{
			x[^1] = x[15] / x[16];  //s_max_by_min
			x[^2] = x[15] / x[18];  //s_max_by_mean
		}
		private static float calculateMean(List<float[]> ys, int target_variable_id)
		{
			if (ys == null || ys.Count == 0 || target_variable_id < 0)
				throw new ArgumentException("Invalid input data or column index.");

			return ys.Average(row => row[target_variable_id]);
		}
		private static float calculateStd(List<float[]> ys, int target_variable_id)
		{
			if (ys == null || ys.Count == 0 || target_variable_id < 0)
				throw new ArgumentException("Invalid input data or column index.");

			int count = ys.Count;
			float mean = ys.Average(row => row[target_variable_id]);
			float sum = ys.Sum(row => MathF.Pow(row[target_variable_id] - mean, 2));

			return MathF.Sqrt(sum / count); // Population standard deviation
		}
		private static float calculateTrendSlope(List<float[]> ys, int target_variable_id)
		{
			if (ys == null || ys.Count <= 1 || target_variable_id < 0)
				throw new ArgumentException("Insufficient data for trend calculation.");

			int n = ys.Count;
			float sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;

			for (int i = 0; i < n; i++)
			{
				float x = i;
				float y = ys[i][target_variable_id];

				sumX += x;
				sumY += y;
				sumXY += x * y;
				sumXX += x * x;
			}

			float numerator = n * sumXY - sumX * sumY;
			float denominator = n * sumXX - sumX * sumX;

			return numerator / denominator;
		}

		private void downsample(List<float[]> data)
		{
			int writeIndex = 0;

			for (int i = 0; i < data.Count; i++)
			{
				if ((i % _downsampling_factor) == 0)
				{
					data[writeIndex++] = data[i];
				}
			}

			if (writeIndex < data.Count)
			{
				data.RemoveRange(writeIndex, data.Count - writeIndex);
			}
		}
		public void calculateStuffX(List<float[]> entry_x)
		{
			if (_is_calculating_proportions)
			{
				for (int item_number_current = 0; item_number_current < entry_x.Count; item_number_current++)
				{
					entry_x[item_number_current][^1] = entry_x[item_number_current][15] / entry_x[item_number_current][16];     //s_max_by_min
					entry_x[item_number_current][^2] = entry_x[item_number_current][15] / entry_x[item_number_current][18];  //s_max_by_mean
				}
			}
		}
		public void calculateStuffY(List<float[]> entry_x, List<float[]> entry_y)
		{
			if (_column_calculate_lag_ids != null)
			{
				int items_to_remove_from_beginning = new();
				for (int item_number_current = 0; item_number_current < entry_y.Count; item_number_current++)
				{
					int columns_count_filled_x = entry_x[0].Length - _columns_calculated_count_x;
					int columns_count_filled_current_x = columns_count_filled_x;
					foreach (var column_calculate_lag_id in _column_calculate_lag_ids)
					{
						float[] column_values_lag = new float[_lag_window_size];
						bool is_column_values_lag_has_nan = false;
						for (int lag_step = 1, k = 0; lag_step <= _lag_window_size; lag_step++, k++)
						{
							int item_number_lag = item_number_current - lag_step;
							if (item_number_lag > 0)
							{
								column_values_lag[k] = entry_y[item_number_lag][column_calculate_lag_id];
							}
							else
							{
								column_values_lag[k] = Single.NaN;
								is_column_values_lag_has_nan = true;
							}
							entry_x[item_number_current][columns_count_filled_current_x] = column_values_lag[k];
							columns_count_filled_current_x++;
						}
						if (is_column_values_lag_has_nan)
						{
							items_to_remove_from_beginning++;
							break;
						}
						if (_is_calculating_mean)
						{
							var mean = column_values_lag.Average();
							entry_x[item_number_current][columns_count_filled_current_x] = mean;
							columns_count_filled_current_x++;
							if (_is_calculating_std)
							{
								entry_x[item_number_current][columns_count_filled_current_x] = (float)Math.Sqrt(column_values_lag.Sum(x => (x - mean) * (x - mean)) / column_values_lag.Length);
								columns_count_filled_current_x++;
							}
							if (_is_calculating_trend_slope)
							{
								float[] column_values_lag_x = new float[_lag_window_size];
								for (int lag_step = 1, k = 0; lag_step <= _lag_window_size; lag_step++, k++)
								{
									int item_number_lag = item_number_current - lag_step;
									column_values_lag_x[k] = entry_x[item_number_lag][columns_count_filled_x - 1];
								}
								float mean_x = column_values_lag_x.Average();
								float numerator = 0f;
								float denominator = 0f;

								for (int i = 0; i < 3; i++)
								{
									float xDiff = i - mean_x;
									numerator += xDiff * (column_values_lag[i] - mean);
									denominator += xDiff * xDiff;
								}

								entry_x[item_number_current][columns_count_filled_current_x] = denominator == 0 ? 0 : numerator / denominator;
								columns_count_filled_current_x++;
							}
						}
					}
				}
				if (_is_removing_nan)
				{
					for (int kk = 0; kk < items_to_remove_from_beginning; kk++)
					{
						entry_x.RemoveAt(0);
						entry_y.RemoveAt(0);
					}
				}
			}
		}
		public static void prepareX(ref float[] x, List<float[]> ys_previous, bool is_calculating_proportions, bool is_calculating_mean, bool is_calculating_std, bool is_calculating_trend_slope)
		{
			int x_columns_initial_count = x.Length;
			int x_columns_calculated_count = is_calculating_proportions.toInt() * 2 + (ys_previous.Count + is_calculating_mean.toInt() + is_calculating_std.toInt() + is_calculating_trend_slope.toInt()) * ys_previous[0].Length;

            {
                var x_resized = new float[x.Length + x_columns_calculated_count];
                Buffer.BlockCopy(x, 0, x_resized, 0, sizeof(float) * x.Length);
                x = x_resized;
            }
            /*for (int i = 0; i < xs_previous.Count; i++)
			{
				var x_resized = new float[xs_previous[i].Length + x_columns_calculated_count];
				Buffer.BlockCopy(xs_previous[i], 0, x_resized, 0, sizeof(float) * xs_previous[i].Length);
				xs_previous[i] = x_resized;
			}*/
            if (is_calculating_proportions)
                calculateProportions(x);
			int column_calculated_number_current = x_columns_initial_count;
			for(int i=0; i< ys_previous.Count; i++)
			{
				for (int j = 0; j < ys_previous[i].Length; j++)
				{
					x[column_calculated_number_current] = ys_previous[i][j];
					column_calculated_number_current++;
				}
            }
			if (is_calculating_mean)
			{
				for (int j = 0; j < ys_previous[0].Length; j++)
				{
					x[column_calculated_number_current]=calculateMean(ys_previous, j);
					column_calculated_number_current++;
				}
			}
			if (is_calculating_std)
			{
				for (int j = 0; j < ys_previous[0].Length; j++)
				{
					x[column_calculated_number_current] = calculateStd(ys_previous, j);
					column_calculated_number_current++;
				}
			}
			if (is_calculating_trend_slope)
			{
				for (int j = 0; j < ys_previous[0].Length; j++)
				{
					x[column_calculated_number_current] = calculateTrendSlope(ys_previous, j);
					column_calculated_number_current++;
				}
			}
		}
		public override async Task<List<(float[][] x, float[][] y)>> loadDataAsync(string path, string input_file_name, string label_file_name, int batch_size, float fraction_data_to_read = 1, int data_part_size = 0, int data_read_offset = 0, bool is_reading_in_random_order = false, Dictionary<string, bool>? directory_names_to_ignore = null, Augmentation augmentation = default)
		{
			DirectoryInfo directory = new DirectoryInfo(path);
			List<(float[][] x, float[][] y)> data = new();
			DirectoryInfo[] directories = directory.GetDirectories();
			float directories_read_in_percents = 0;
			int[] directory_numbers = Enumerable.Range(0, directories.Length).ToArray();
			int directory_number_current = 0;
			Random random = new();
			Dictionary<int, Dictionary<string, float>> _dict_column_number_dict_categorical_value_float_value = new();

			if (is_reading_in_random_order)
				random.Shuffle(directory_numbers);
			if (data_part_size == 0)
				data_part_size = directories.Length / batch_size;
			if (data_read_offset > 0 && data_read_offset < directories.Length)
				directory_number_current += data_read_offset;

			for (; directory_number_current < directory_numbers.Length; directory_number_current++) //NOTE Parallel for is not possible if yield is used
			{
				if (directory_names_to_ignore != null && directory_names_to_ignore.ContainsKey(directories[directory_numbers[directory_number_current]].Name) && directory_names_to_ignore[directories[directory_numbers[directory_number_current]].Name] == true)
					continue;

				for (int ii = 0; ii <= augmentation.augmented_items_per_item; ii++)
				{ }

				FileInfo[] files = directories[directory_numbers[directory_number_current]].GetFiles().OrderBy(f => f.Name).ToArray();  //Order by name
				int file_number_current = 0;
				(List<float[]> x, List<float[]> y) entry = new();
				if (files.Length == 0)
				{
					Tracer.traceMessage($"Cannot find any files in {directories[directory_numbers[directory_number_current]]}", MESSAGE_SEVERITY.ERROR);
					continue;
				}
				foreach (FileInfo file in files)
				{
					if (file.Name == input_file_name)
					{
						entry.x = await new CSVFile(file, ';', _dict_column_number_dict_categorical_value_float_value).read(additional_columns_count: _columns_calculated_count_x);
						if (_downsampling_factor > 1)
							downsample(entry.x);
						calculateStuffX(entry.x);
					}
					else if (file.Name == label_file_name)
					{
						entry.y = await new CSVFile(file, ';', _dict_column_number_dict_categorical_value_float_value).read();
						if (_downsampling_factor > 1)
							downsample(entry.y);
						calculateStuffY(entry.x, entry.y);
					}
					else
					{
						Tracer.traceMessage($"Skipping {file.Name}", MESSAGE_SEVERITY.WARNING);
					}
					file_number_current++;
				}
				if (entry.x.Count != entry.y.Count)
				{
					var a = 5;
					var b = a + 5;
					Tracer.traceMessage($"Data in files inside {directories[directory_numbers[directory_number_current]].FullName} is not aligned properly: {MathF.Abs(entry.x.Count - entry.y.Count)} items are missing! Trimming.", MESSAGE_SEVERITY.ERROR, Tracer.TRACE_FLAG.NO_CALLER_ATTRIBUTES);
					var difference = Math.Abs(entry.x.Count - entry.y.Count);
					if (entry.x.Count > entry.y.Count)
					{
						entry.x.RemoveRange(entry.y.Count, difference);
					}
					else
					{
						entry.y.RemoveRange(entry.x.Count, difference);
					}
				}
				data.Add(new(entry.x.ToArray(), entry.y.ToArray()));
				directories_read_in_percents = directory_number_current / (float)directory_numbers.Length;
				Tracer.traceMessage($"Dataset read progress: {Math.Round(directories_read_in_percents * 100, 2)}%.\r", is_writing_line: false);
				if (directories_read_in_percents >= fraction_data_to_read)
					break;
			}

			return data;
		}
	}
	public AIModelCrackForecastingGradientBoosting(string model_name, int target_variables_count, int estimators_count, int tree_depth_max, float learning_rate = 0.1f) : base(model_name, target_variables_count, estimators_count, tree_depth_max, learning_rate)
	{
	}
}

