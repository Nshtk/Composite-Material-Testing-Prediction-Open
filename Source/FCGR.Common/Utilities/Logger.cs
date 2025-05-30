using System;
using System.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Threading;
using System.Text.RegularExpressions;
using System.IO;

namespace FCGR.Common.Utilities;

public enum MESSAGE_SEVERITY
{
	COMMON,
	INFORMATION,
	WARNING,
	ERROR,
	CRITICAL
}

/// <summary>
///		Logs messages to file.
/// </summary>
public class Logger : IDisposable
{
	#region Definitions
	#endregion
	#region Fields
	private const int _Thread_processing_iterations_idling_max = 10000;
	private readonly StringBuilder _string_builder = new StringBuilder();
	public bool is_logging_enabled = true;
	private readonly StreamWriter _stream_writer;
	private readonly ConcurrentQueue<(string, DateTime)> _queue_messages=new();
	private readonly Thread _thread_log_runner;
	private readonly AutoResetEvent _auto_reset_event_log_runner = new AutoResetEvent(false);
	#endregion
	#region Properties
	public FileInfo File_Log
	{
		get;
		private set;
	}
	#endregion
	public Logger(string file_name)
	{
		File_Log=new FileInfo(file_name);
		if (!File_Log.Directory.Exists)
			Directory.CreateDirectory(File_Log.Directory.FullName);
		FileStream file_stream = File_Log.Open(FileMode.OpenOrCreate, FileAccess.Write);
		file_stream.SetLength(0);
		_stream_writer = new StreamWriter(file_stream);
		_thread_log_runner = new Thread(listen);
		_thread_log_runner.Priority=ThreadPriority.Lowest;
		_thread_log_runner.Start();
	}
	~Logger()
	{
		dispose(false);
	}
	#region Methods
	/// <summary>
	///		Log message to file.
	/// </summary>
	/// <param name="message"></param>
	/// <param name="date_time"></param>
	public virtual void log(string message, DateTime date_time)
	{
		_queue_messages.Enqueue(new(message, date_time));
		if(_thread_log_runner.ThreadState!=System.Threading.ThreadState.Running)
			_auto_reset_event_log_runner.Set();	
	}
	protected async virtual void listen()
	{
		(string, DateTime) message_date_time;
		string string_builder_as_string;
		int iterations_idling = 0;

		while (true)
		{
			if (_queue_messages.TryDequeue(out message_date_time))
			{
				_string_builder.AppendFormat("[{0}] {1}", TimeOnly.FromDateTime(message_date_time.Item2).ToLongTimeString(), message_date_time.Item1);
				string_builder_as_string = _string_builder.ToString();
				Console.WriteLine(string_builder_as_string);
				await _stream_writer.WriteLineAsync(string_builder_as_string);
				await _stream_writer.FlushAsync();
				_string_builder.Clear();
				iterations_idling = 0;
			}
			else
			{
				if (iterations_idling > _Thread_processing_iterations_idling_max)
					_auto_reset_event_log_runner.WaitOne();
				iterations_idling++;
			}
		}
	}
	private void dispose(bool is_eplicit)
	{
		if(is_eplicit)
		{

		}
		_stream_writer.Dispose();
	}
	public void Dispose()
	{
		dispose(true);
	}
	#endregion
}
#if TRACE
/// <summary>
///		Traces messages to stdout.
/// </summary>
/// <remarks>
///		Can be used for printing messages from other assemblies to the UI of main app.
/// </remarks>
public static class Tracer	//TODO? make all classes that use tracer inherit from class that provides traceMessage method
{
	#region Definitions
	[Flags]
	public enum TRACE_FLAG
	{
		NONE=0,
		NO_CALLER_ATTRIBUTES=1,	//Set caller file path and member name to empty
		NO_LOG=2,				//Do not log message to file
		EXCEPTION = 4,
		PRINT = 8,				//Print message to the UI of main app.
	}
	#endregion
	#region Fields
	#endregion
	#region Properties
	#endregion
	static Tracer()
	{
		Trace.AutoFlush = true;
	}
	#region Methods
	/// <summary>
	///		Trace message to stdout.
	/// </summary>
	/// <param name="message"></param>
	/// <param name="message_severity"></param>
	/// <param name="flags"></param>
	/// <param name="date_time"></param>
	/// <param name="caller_file_path"></param>
	/// <param name="caller_member_name"></param>
	/// <param name="caller_line_number"></param>
	[Conditional("TRACE")]
	public static void traceMessage(string message, MESSAGE_SEVERITY message_severity = MESSAGE_SEVERITY.COMMON, TRACE_FLAG flags = default, DateTime date_time = default, bool is_writing_line=true, [CallerFilePath] string caller_file_path = "", [CallerMemberName] string caller_member_name = "", [CallerLineNumber] int caller_line_number = 0)	//Fields with attributes here should not be null and be non-nullable 
	{
		StringBuilder string_builder=new (message);
		string string_builder_as_string;

		if (date_time == default)
			date_time = DateTime.Now;
		if(!flags.HasFlag(TRACE_FLAG.NO_CALLER_ATTRIBUTES))
		{
			caller_file_path=Regex.Match(caller_file_path, @"[^/\\]+[\\/][^/\\]+$").Value;
			string_builder.Clear();
			string_builder.AppendFormat("{0}.{1}.{2}: {3}", caller_file_path, caller_member_name, caller_line_number, message);
		}
		string_builder_as_string = string_builder.ToString();
		onMessageTraced?.Invoke(string_builder_as_string, message_severity, flags, date_time);	//Send traced message to loggers
		if(is_writing_line)
			Trace.WriteLine(string_builder_as_string);
		else
			Trace.Write(string_builder_as_string);
	}
	#endregion
	#region Events
	public delegate void messageTraced(string message, MESSAGE_SEVERITY message_severity, TRACE_FLAG flags, DateTime date_time);
	public static event messageTraced onMessageTraced;
	#endregion
}
#endif