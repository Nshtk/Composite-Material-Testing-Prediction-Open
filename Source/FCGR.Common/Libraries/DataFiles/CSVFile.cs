using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace FCGR.Common.Libraries.DataFiles;

public sealed class CSVFile : DataFile
{
	#region Fields
	public char separator;
	string[]? column_names;
	private Dictionary<int, Dictionary<string, float>> _dict_column_number_dict_categorical_value_float_value;
	#endregion
	public CSVFile(FileInfo file, char separator, Dictionary<int, Dictionary<string, float>> dict_column_number_dict_categorical_value_float_value) : base(file, ".csv")
	{
		this.separator = separator;
		_dict_column_number_dict_categorical_value_float_value = dict_column_number_dict_categorical_value_float_value;
	}
	public CSVFile(string path_file, char separator, Dictionary<int, Dictionary<string, float>> dict_column_number_dict_categorical_value_float_value) : base(path_file, ".csv")
	{
		this.separator = separator;
		_dict_column_number_dict_categorical_value_float_value = dict_column_number_dict_categorical_value_float_value;
	}
	#region Methods
	public string[]? readLine(StreamReader stream_reader)    //Categorical data is handled through ordinal encoding
	{
		var line = stream_reader.ReadLine();
		
		if(line==null)
			return null;
		
		return line.Split(separator);
	}
	public T[] parseStringValues<T>(string[] values) 
		where T : INumber<T> 
	{
		T[] values_as_T = new T[values.Length];

		for (int i = 0; i < values.Length; i++)
		{
			T value_as_T;
			if (!T.TryParse(values[i].Replace(',', '.'), null, out value_as_T))
			{
				if (!_dict_column_number_dict_categorical_value_float_value.ContainsKey(i))
					_dict_column_number_dict_categorical_value_float_value.Add(i, new Dictionary<string, float>());
				var _dict_categorical_value_float_value_casted = (IDictionary<string, T>)_dict_column_number_dict_categorical_value_float_value[i];
				
				if (!_dict_categorical_value_float_value_casted.TryGetValue(values[i], out value_as_T))
				{
					value_as_T = (T)Convert.ChangeType(_dict_categorical_value_float_value_casted.Count, typeof(T));
					_dict_categorical_value_float_value_casted.Add(values[i], value_as_T);
				}
			}
			values_as_T[i] = value_as_T;
		}

		return values_as_T;
	}
	public async Task<List<float[]>> read(int additional_columns_count = 0, int rows_count_to_skip = 0)    //Categorical data is handled through ordinal encoding
	{
		List<float[]> data = new();

		try
		{
			using (var stream_reader = new StreamReader(File.OpenRead()))
			{
				string[] column_names = (await stream_reader.ReadLineAsync()).Split(separator); //Parse csv headers
				int columns_count = column_names.Length;
				int values_count = columns_count + additional_columns_count;
				float[] categorical_data_ids = new float[columns_count];

				for (int i = 0; i < rows_count_to_skip; i++)
					await stream_reader.ReadLineAsync();
				while (true)
				{
					var values = readLine(stream_reader);
					if (values == null)
						break;
					float[]? values_as_float=parseStringValues<float>(values);
					if(values_count>values_as_float.Length)
					{
						float[] values_as_float_extended = new float[values_count];
						Buffer.BlockCopy(values_as_float, 0, values_as_float_extended, 0, values_as_float.Length * sizeof(float));
						values_as_float = values_as_float_extended;
					}
					data.Add(values_as_float);
				}
			}
		}
		catch (IOException)
		{

		}

		return data;
	}
	public async Task write(string file_name_full, float[][] data, Dictionary<int, string> columnHeaders)
	{
		if (string.IsNullOrWhiteSpace(file_name_full))
			throw new ArgumentException("File path cannot be empty", nameof(file_name_full));

		int[] column_ids_ordered = columnHeaders.Keys.OrderBy(k => k).ToArray();

		using (var writer = new StreamWriter(file_name_full))
		{
			await writer.WriteLineAsync(string.Join(separator, column_ids_ordered.Select(col => columnHeaders[col])));
			foreach (var row in data)
			{
				string[] values = new string[column_ids_ordered.Length];
				for (int i = 0; i < column_ids_ordered.Length; i++)
					values[i] = column_ids_ordered[i] < row.Length ? row[column_ids_ordered[i]].ToString() : string.Empty;
				await writer.WriteLineAsync(string.Join(separator, values));
			}
		}
	}
	#endregion
}
