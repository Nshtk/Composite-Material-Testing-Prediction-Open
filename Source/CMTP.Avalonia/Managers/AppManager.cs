using System;
using System.IO;
using System.Text;
using System.Net;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Diagnostics;

using Avalonia;
using Avalonia.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Controls;

using FCGR.Common.Utilities;
using FCGR.Common.Libraries.SSystem;
using CMTP.Avalonia.Views.Controls.MainWindow;

namespace CMTP.Avalonia.Managers;

public sealed class LoggerExtended : Logger
{
	#region Fields
	public readonly bool is_printing_enabled;
	#endregion
	public LoggerExtended(string file_name) : base(file_name)
	{
		is_printing_enabled = true;
	}
	#region Methods
	public void logMessage(string message, MESSAGE_SEVERITY message_severity = MESSAGE_SEVERITY.COMMON, DateTime date_time = default, bool is_printing = false)
	{
		if (!is_logging_enabled)
			return;

		StringBuilder string_builder = new StringBuilder();

		if (date_time == default)
			date_time = DateTime.Now;

		switch (message_severity)
		{
			case MESSAGE_SEVERITY.INFORMATION:
				string_builder.AppendFormat("INFORMATION: {0}", message);
				break;
			case MESSAGE_SEVERITY.COMMON:
				string_builder.Append(message);
				break;
			case MESSAGE_SEVERITY.WARNING:
				string_builder.AppendFormat("WARNING: {0}", message);
				break;
			case MESSAGE_SEVERITY.ERROR:
				string_builder.AppendFormat("ERROR: {0}", message);
				break;
			default:
				break;
		}
		log(string_builder.ToString(), date_time);
	}
	public void printMessage(string message, MESSAGE_SEVERITY message_severity = MESSAGE_SEVERITY.COMMON)   //NOTE Cannot use ref parameters inside lambda
	{
		if (!is_printing_enabled)
			return;

		if (message == LogBox.Instance.message_last)
		{
			string message_last_times_printed_count_string, message_last_times_printed_count_previous_string;
			int difference_in_number_of_digits;
			bool is_appending_to_end;

			message_last_times_printed_count_previous_string = LogBox.Instance.message_last_times_printed_count.ToString();
			LogBox.Instance.message_last_times_printed_count++;
			message_last_times_printed_count_string = LogBox.Instance.message_last_times_printed_count.ToString(); ;
			is_appending_to_end = LogBox.Instance.message_last_times_printed_count == 2; //Declaring a variable to save current result becaouse UI thread is started after the variable is modified
			difference_in_number_of_digits = message_last_times_printed_count_string.Length - message_last_times_printed_count_previous_string.Length;

			if (difference_in_number_of_digits != 0)
				Dispatcher.UIThread.Post(() => { LogBox.Instance.modifyLastMessage(is_appending_to_end, " X" + message_last_times_printed_count_string, offset: difference_in_number_of_digits); });
			else
				Dispatcher.UIThread.Post(() => { LogBox.Instance.modifyLastMessage(is_appending_to_end, " X" + message_last_times_printed_count_string); });
			return;
		}
		LogBox.Instance.message_last = message;
		LogBox.Instance.message_last_times_printed_count = 1;

		switch (message_severity)
		{
			case MESSAGE_SEVERITY.INFORMATION:
				Dispatcher.UIThread.Post(() => { LogBox.Instance.appendMessage($"ИНФОРМАЦИЯ: {message}", null, 14, FontStyle.Italic, FontWeight.Normal, new SolidColorBrush(Colors.LightSkyBlue)); });  //NOTE string interpolation is better than concatenation
				break;
			case MESSAGE_SEVERITY.COMMON:
				Dispatcher.UIThread.Post(() => { LogBox.Instance.appendMessage(message, $"[{TimeOnly.FromDateTime(DateTime.Now).ToLongTimeString()}]", 13, FontStyle.Normal, FontWeight.Normal, new SolidColorBrush(Colors.White)); });
				break;
			case MESSAGE_SEVERITY.WARNING:
				Dispatcher.UIThread.Post(() => { LogBox.Instance.appendMessage($"ВНИМАНИЕ: {message}", $"[{TimeOnly.FromDateTime(DateTime.Now).ToLongTimeString()}]", 15, FontStyle.Normal, FontWeight.SemiBold, new SolidColorBrush(Colors.Yellow)); });
				break;
			case MESSAGE_SEVERITY.ERROR:
				Dispatcher.UIThread.Post(() => { LogBox.Instance.appendMessage($"ОШИБКА: {message}", $"[{TimeOnly.FromDateTime(DateTime.Now).ToLongTimeString()}]", 16, FontStyle.Normal, FontWeight.Bold, new SolidColorBrush(Colors.Red), TextDecorations.Underline); });
				break;
			default:
				break;
		}
	}
	public void logFromTrace(string message, MESSAGE_SEVERITY message_severity, Tracer.TRACE_FLAG flags, DateTime date_time)
	{
		if (!flags.HasFlag(Tracer.TRACE_FLAG.NO_LOG))
			logMessage(message, message_severity, date_time);
	}
	#endregion
}

public static class AppManager
{
	#region Fields
	public static readonly ConcurrentDictionary<string, Window> Windows = new ConcurrentDictionary<string, Window>();
	public static readonly LoggerExtended Logger;
	#endregion
	#region Properties
	internal static SettingsManager Settings_Manager
	{
		get;
	} = new();
	#endregion
	static AppManager()
	{
		try
		{
			Logger = new LoggerExtended("Logs/latest.log");
		}
		catch (Exception e)
		{
			throw;
		}
		Tracer.onMessageTraced += Logger.logFromTrace;
		Logger.logMessage("Program started.", MESSAGE_SEVERITY.INFORMATION);

		ProjectManager.projectOpened += (sender, e) =>     //Update server address based on value in project settings.
		{
			ServerManager.Server_Endpoint = new IPEndPoint(IPAddress.Parse((string)Settings_Manager.Server_Address.Value), (int)Settings_Manager.Server_Port.Value);
		};
		SettingsManager.themeChanged += (sender, theme) =>
		{
			if (theme == SettingsManager.THEME.SYSTEM)
				Dispatcher.UIThread.Post(() => Application.Current.RequestedThemeVariant = new ThemeVariant("Default", null));
			else if (theme == SettingsManager.THEME.LIGHT)
				Dispatcher.UIThread.Post(() => Application.Current.RequestedThemeVariant = new ThemeVariant("Light", null));
			else if (theme == SettingsManager.THEME.DARK)
				Dispatcher.UIThread.Post(() => Application.Current.RequestedThemeVariant = new ThemeVariant("Dark", null));
		};
	}
	public static void Dispose()
	{
		Logger.Dispose();
	}
	#region Methods
	#endregion
}
