using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Globalization;
using System.ComponentModel;
using System.Linq;
using System.IO;
using System.Diagnostics;

namespace FCGR.Common.Utilities;

public static class BooleanExtensions
{
	public static unsafe int toInt(this bool value)
	{
		return *(byte*)&value;
	}
}
public static class EnumExtensions
{
	public static string getDescription(this Enum value)
	{
		object[]? attributes = value.GetType().GetField(value.ToString()).GetCustomAttributes(typeof(DescriptionAttribute), false);
		if (attributes.Any())
			return (attributes.First() as DescriptionAttribute).Description;

		TextInfo text_info = CultureInfo.CurrentCulture.TextInfo;		 //Replace underscores with spaces if there is no description
		return text_info.ToTitleCase(text_info.ToLower(value.ToString().Replace("_", " ")));
	}
}
public static class StringExtensions
{
	public static int getHashCodeConsistent(this string str)
	{
		unchecked
		{
			int hash1 = 5381;
			int hash2 = hash1;

			for (int i = 0; i < str.Length && str[i] != '\0'; i += 2)
			{
				hash1 = (hash1 << 5) + hash1 ^ str[i];
				if (i == str.Length - 1 || str[i + 1] == '\0')
					break;
				hash2 = (hash2 << 5) + hash2 ^ str[i + 1];
			}

			return hash1 + hash2 * 1566083941;
		}
	}
}
public static class NullExtensions
{
	public static T toNonNullable<T>(this T? value)
	{
		T result = value ?? default;
		return result;
	}
}
public static class FileStreamExtensions
{
	public static (string[], int) readLinesWithPosition(this FileStream file_stream, int lines_number)
	{
		string[] lines = new string[lines_number];
		List<byte> line_bytes = new List<byte>(128);
		int bytes_read_count = 0;

		lines_number--;
		for (int line_current = 0, c = 1; c >= 0;)
		{
			c = file_stream.ReadByte();
			line_bytes.Add(Convert.ToByte(c));
			bytes_read_count++;
			if (c == 10)
			{
				lines[line_current] = Encoding.Default.GetString(line_bytes.ToArray()).TrimEnd();
				line_bytes.Clear();
				if (line_current == lines_number)
					break;
				line_current++;
			}
		}
		return (lines, bytes_read_count);
	}
}
public static class IEnumerableExtensions
{
	internal static T[][] ToJaggedArray<T>(this T[,] array_2d)
	{
		int rows_first_index = array_2d.GetLowerBound(0);
		int rows_last_index = array_2d.GetUpperBound(0);
		int rows_count = rows_last_index - rows_first_index + 1;
		int cols_first_index = array_2d.GetLowerBound(1);
		int cols_last_index = array_2d.GetUpperBound(1);
		int cols_count = cols_last_index - cols_first_index + 1;
		T[][] array_jagged = new T[rows_count][];
		
		for (int i = 0; i < rows_count; i++)
		{
			array_jagged[i] = new T[cols_count];
			for (int j = 0; j < cols_count; j++)
				array_jagged[i][j] = array_2d[i + rows_first_index, j + cols_first_index];
		}

		return array_jagged;
	}
	public static void DisposeElements<T>(this IEnumerable<T> enumerable)
	{
		if (typeof(T).GetInterface(nameof(IDisposable)) != null)
			foreach (IDisposable disposable in enumerable)
				if(disposable!=null)
					disposable.Dispose();
		else
			Tracer.traceMessage($"Call to {nameof(DisposeElements)} failed: type {nameof(T)} doesn't implement IDisposable.", MESSAGE_SEVERITY.ERROR);
	}
}
public static class ICollectionExtensions
{
	public static (IEnumerable<TValue>, IEnumerable<TKey>) SortALike<TValue, TKey>(this ICollection<TValue> values, IEnumerable<TKey> keys)
	{
		TKey[] keys_as_raw_array = keys.ToArray();
		TValue[] values_as_raw_array = values.ToArray();

		Array.Sort(keys_as_raw_array, values_as_raw_array);
		values=values_as_raw_array;

		return (values_as_raw_array, keys_as_raw_array);
	}
}


#if DEBUG
public static class ObjectExtensions
{
	public static long getMemoryAddress(this object obj)
	{
		return GCHandle.ToIntPtr(GCHandle.Alloc(obj, GCHandleType.WeakTrackResurrection)).ToInt64();
	}
}
#endif
