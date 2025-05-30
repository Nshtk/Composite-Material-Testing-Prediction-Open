using System;
using System.ComponentModel;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;

using Avalonia.Controls;

using FCGR.Common.Utilities;

namespace CMTP.Avalonia.Managers;
/// <summary>
///		Used for launching/stopping local server.
/// </summary>
public static class ServerManager
{
	#region Fields
	private static Process? _process_server = null;
	private static bool _is_server_running = false;
	#endregion
	#region Properties
	public static bool Is_Server_Running
	{
		get
		{
#if DEBUG
			return _is_server_running;
#endif
#if RELEASE
			return _is_server_running && _process_server != null && !_process_server.HasExited;
#endif
		}
		private set { _is_server_running = value; }
	}
	public static IPEndPoint Server_Endpoint
	{
		get;
		set;
	}
	#endregion
	static ServerManager()
	{
		AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
#if DEBUG                            //Server should be started by IDE in debug, Is_Server_Running prevents starting server from code                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             
		Is_Server_Running = true;    //Comment this line to test server launch "in production" 
		if (!Design.IsDesignMode)
		{
			Process[] processes;
			if ((processes = Process.GetProcessesByName("FCGR.Server")).Length > 0)
				_process_server = processes[0];
			else
			{
				//throw new FileNotFoundException();
			}
		}
#endif
	}
	#region Methods
	/// <summary>
	///		Starts the local server process.
	/// </summary>
	/// <returns></returns>
	public static async Task<bool> startServerAsync()
	{
		if (Is_Server_Running)
			return true;
		Tracer.traceMessage($"Starting local server...");

		Process[] processes;

		if ((processes = Process.GetProcessesByName("FCGR.Server")).Length > 0)
		{
			for (int i = 0; i < processes.Length; i++)
			{
				_process_server = processes[i];
				await stopServerAsync().ConfigureAwait(false);
			}
		}


#if DEBUG
		ProcessStartInfo process_start_info = new ProcessStartInfo("../../../../FCGR.Server/bin/Release/net8.0/FCGR.Server.exe", new string[] { Server_Endpoint.Address.ToString(), Server_Endpoint.Port.ToString() });
#endif
#if RELEASE
		ProcessStartInfo process_start_info = new ProcessStartInfo("Server/FCGR.Server.exe", new string[] { "-port", Server_Address.Port.ToString(), "-ip_type", (bool)AppManager.Settings_Manager.Server_Is_Using_IPV6.Value==true ? "v6" : "v4"});
		process_start_info.RedirectStandardOutput = true;
		process_start_info.RedirectStandardError = true;
		process_start_info.UseShellExecute = false;
		process_start_info.CreateNoWindow = true;
#endif
		try
		{
			_process_server = Process.Start(process_start_info);
		}
		catch (Win32Exception ex)
		{
			AppManager.Logger.logMessage("Не удалось найти исполняемый файл сервера, запуск невозможен. Проверьте целостность файлов программы.", MESSAGE_SEVERITY.ERROR);
			return false;
		}
		if (_process_server != null)
		{
			Is_Server_Running = true;
			AppManager.Logger.logMessage($"Запуск локального сервера {Server_Endpoint.Address}:{Server_Endpoint.Port}.", is_printing: true);
		}
		else
			Is_Server_Running = false;

		return Is_Server_Running;
	}
	/// <summary>
	///		Kills local server process.
	/// </summary>
	/// <returns></returns>
	public static async Task stopServerAsync()
	{
		Tracer.traceMessage($"Shutting down server.");
		try
		{
			_process_server.Kill();
		}
		catch (Exception ex)
		{

		}
		await _process_server.WaitForExitAsync().ConfigureAwait(false);
		_process_server = null;
	}
	/// <summary>
	///		Restarts local server process.
	/// </summary>
	/// <returns></returns>
	public static async Task<bool> restartServerAsync()
	{
		if (Is_Server_Running)
			await stopServerAsync().ConfigureAwait(false);

		return await startServerAsync();
	}
	#endregion
}
