using System;
using System.Drawing;
using System.Text;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;

using Emgu.CV;
using Emgu.CV.CvEnum;

using TorchSharp;
using TorchSharp.Utils;
using System.Collections.Generic;

namespace FCGR.Common.Utilities;

public static class Helper
{
	#region Definitions
	/// <summary>
	///		WIP
	/// </summary>
	public static class Input
	{
		public enum MOUSE_BUTTONS
		{
			LEFT,
			RIGHT,
			MIDDLE,
		}

		public static MOUSE_BUTTONS Mouse_button_last_pressed;          //TODO? make an event here somehow
		private static readonly Stopwatch _Stopwatch = new Stopwatch();

		public static bool Is_Pointer_Being_Held
		{
			get;
			private set;
		} = false;

		public static async void startHoldingTimer(int long_press_threeshold_ms)
		{
			if (_Stopwatch.IsRunning)
				return;
			Is_Pointer_Being_Held = false;
			_Stopwatch.Restart();
			await Task.Run(() =>
			{
				while (!Is_Pointer_Being_Held)
				{
					if (_Stopwatch.Elapsed.TotalMilliseconds > long_press_threeshold_ms)
						Is_Pointer_Being_Held = true;
				}
				_Stopwatch.Stop();
			});
		}
	}
	public sealed class Spinner
	{
		public enum TYPE
		{
			BRAILE,
			ARROW,
			BQ,
			PQ,
			SLASH,
			CROSS,
			CLOCK_SQUARE,
			CLOCK,
			CIRCLE,
			DB_PIPE,
			PQ_PIPE,
			B_Q,
			P_Q,
			BOB,
			O,
			SPECIAL,
			HISTOGRAM,
			PROGRESS_BAR
		}

		private readonly List<char> _chars;
		public uint position_current;

		public Spinner(TYPE type)
		{
			switch (type)
			{
				case TYPE.BRAILE:
					_chars = new(new[] { '⣷', '⣯', '⣟', '⡿', '⢿', '⣻', '⣽', '⣾' });
					break;
				case TYPE.ARROW:
					_chars = new(new[] { '←', '↖', '↑', '↗', '→', '↘', '↓', '↙' });
					break;
				case TYPE.BQ:
					_chars = new(new[] { 'b', 'ᓂ', 'q', 'ᓄ' });
					break;
				case TYPE.PQ:
					_chars = new(new[] { 'd', 'ᓇ', 'p', 'ᓀ' });
					break;
				case TYPE.SLASH:
					_chars = new(new[] { '|', '/', '—', '\\' });
					break;
				case TYPE.CROSS:
					_chars = new(new[] { 'x', '+' });
					break;
				case TYPE.CLOCK_SQUARE:
					_chars = new(new[] { '◰', '◳', '◲', '◱' });
					break;
				case TYPE.CLOCK:
					_chars = new(new[] { '◴', '◷', '◶', '◵' });
					break;
				case TYPE.CIRCLE:
					_chars = new(new[] { '◐', '◓', '◑', '◒' });
					break;
				case TYPE.DB_PIPE:
					_chars = new(new[] { 'd', '|', 'b', '|' });
					break;
				case TYPE.PQ_PIPE:
					_chars = new(new[] { 'q', '|', 'p', '|' });
					break;
				case TYPE.B_Q:
					_chars = new(new[] { 'ᓂ', '—', 'ᓄ', '—' });
					break;
				case TYPE.P_Q:
					_chars = new(new[] { 'ᓇ', '—', 'ᓀ', '—' });
					break;
				case TYPE.BOB:
					_chars = new(new[] { '|', 'b', 'O', 'b' });
					break;
				case TYPE.O:
					_chars = new(new[] { '_', 'o', 'O', 'o' });
					break;
				case TYPE.SPECIAL:
					_chars = new(new[] { '.', 'o', 'O', '@', '*', ' ' });
					break;
				case TYPE.HISTOGRAM:
					_chars = new(new[] { '▁', '▃', '▄', '▅', '▆', '▇', '█', '▇', '▆', '▅', '▄', '▃' });
					break;
				case TYPE.PROGRESS_BAR:
					_chars = new(new[] { '▉', '▊', '▋', '▌', '▍', '▎', '▏', '▎', '▍', '▌', '▋', '▊', '▉' });
					break;
				default:
					break;
			}
		}

		public char getNextChar()
		{
			if (position_current >= _chars.Count)
				position_current = 0;
			return _chars[(int)position_current++];
		}
	}
	#endregion
	#region Methods
	[Conditional("DEBUG")] //NOTE Does not compile in Release, calls to this method are ignored
	public static void DebugWriteLine(string message, MESSAGE_SEVERITY message_severity = MESSAGE_SEVERITY.COMMON, bool is_writeline_mode = true, [CallerFilePath] string caller_class_name = "", [CallerMemberName] string caller_method_name = "")
	{
		StringBuilder string_builder = new();

		if (caller_class_name != null)
			string_builder.AppendFormat("{0}: ", caller_class_name);
		if (caller_method_name != null)
			string_builder.AppendFormat("{0}: ", caller_method_name);
		if (String.IsNullOrEmpty(message))
			throw new Exception("Message was null or empty");
		switch (message_severity)           //Color formatting used by VSColorOutput extension
		{
			case MESSAGE_SEVERITY.COMMON:
				break;
			case MESSAGE_SEVERITY.INFORMATION:
				string_builder.Append("info ");
				break;
			case MESSAGE_SEVERITY.WARNING:
				string_builder.Append("warn ");
				break;
			case MESSAGE_SEVERITY.ERROR:
				string_builder.Append("failed ");
				break;
			case MESSAGE_SEVERITY.CRITICAL:
				string_builder.Append("failed CRITICAL ");
				break;
			default:
				throw new NotImplementedException();
		}
		string_builder.Append(message);
		if (is_writeline_mode)
			string_builder.Append(Environment.NewLine);

		Debug.Write(string_builder.ToString());
	}
	/// <summary>
	///		Swaps objects.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="first"></param>
	/// <param name="second"></param>
	public static void swap<T>(ref T first, ref T second)
	{
		T temp = first;
		first = second;
		second = temp;
	}
	/// <summary>
	///		Concatenates arrays.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="arrays"></param>
	/// <param name="elements_count_total"></param>
	/// <returns></returns>
	public static T[] concatenateArrays<T>(T[][] arrays, int elements_count_total = 0)
			where T : struct
	{
		int offset = 0;
		T[] result;

		result = elements_count_total <= 0 ? new T[arrays.Sum(arr => arr.Length)] : new T[elements_count_total];
		for (int i = 0; i < arrays.Length; i++)
		{
			Array.Copy(arrays[i], 0, result, offset, arrays[i].Length);
			offset += arrays[i].Length;
		}

		return result;
	}
	public static T[,] To2D<T>(T[][] source)
	{
		try
		{
			int FirstDim = source.Length;
			int SecondDim = source.GroupBy(row => row.Length).Single().Key; // throws InvalidOperationException if source is not rectangular

			var result = new T[FirstDim, SecondDim];
			for (int i = 0; i < FirstDim; ++i)
				for (int j = 0; j < SecondDim; ++j)
					result[i, j] = source[i][j];

			return result;
		}
		catch (InvalidOperationException)
		{
			throw new InvalidOperationException("The given jagged array is not rectangular.");
		}
	}
	/// <summary>
	///		Calculates the distance between two points
	/// </summary>
	/// <param name="point_1"></param>
	/// <param name="point_2"></param>
	/// <returns>Distance between points</returns>
	public static double getDistanceBetweenPoints(in PointF point_1, in PointF point_2)
	{
		return Math.Sqrt(Math.Pow(point_2.X - point_1.X, 2) + Math.Pow(point_2.Y - point_1.Y, 2));
	}
	public static void showMat(Mat mat, string name = "image", int delay = 0, bool is_destroying_window = true)
	{
		CvInvoke.Imshow(name, mat);
		CvInvoke.WaitKey(delay);
		if (is_destroying_window)
			CvInvoke.DestroyAllWindows();
	}
	public static unsafe Mat getMatFromTensor<TTensorDType>(torch.Tensor tensor)
			where TTensorDType : unmanaged
	{
		Mat mat;
		DepthType mat_depth_type;
		TensorAccessor<TTensorDType> prediction_accessor = tensor.data<TTensorDType>();
		Type tensor_dtype = typeof(TTensorDType);

		if (tensor_dtype == typeof(byte))
			mat_depth_type = DepthType.Cv8U;
		else
			mat_depth_type = DepthType.Cv32F;

		fixed (TTensorDType* p = prediction_accessor.ToArray())
		{
			nint ptr = (nint)p;
			mat = new Mat(new int[] { (int)tensor.shape[0], (int)tensor.shape[1] }, mat_depth_type, ptr);
		}

		return mat;
	}
	public static torch.Tensor getTensorFromMat(Mat mat, ImreadModes imread_mode, torch.ScalarType tensor_dtype)
	{
		torch.Tensor image_as_tensor = torch.from_array(mat.GetData(), tensor_dtype);

		if (imread_mode == ImreadModes.Grayscale)
			image_as_tensor.unsqueeze_(0);
		else
			image_as_tensor.permute(2, 0, 1);

		return image_as_tensor;
	}
	public static float[][] swapDimensions(float[][] array)
	{
		if (array == null || array.Length == 0)
			throw new ArgumentException("Input array must not be empty.");

		int rows = array.Length;
		int cols = array[0].Length;
		float[][] array_reshaped = new float[cols][];

		for (int j = 0; j < cols; j++)
		{
			array_reshaped[j] = new float[rows];
			for (int i = 0; i < rows; i++)
			{
				array_reshaped[j][i] = array[i][j];
			}
		}

		return array_reshaped;
	}
	#endregion
}