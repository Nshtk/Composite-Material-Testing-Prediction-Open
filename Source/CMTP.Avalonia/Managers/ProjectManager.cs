using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;

using Emgu.CV;

using ClosedXML.Excel;

using FCGR.Common.Utilities;
using FCGR.Common.Libraries.DataFiles;
using CMTP.Avalonia.Models;

namespace CMTP.Avalonia.Managers;

/// <summary>
///		Handles main project properties and paths.
/// </summary>
/// <remarks>
///		Additional project properties and settings are handled by <seealso cref="SettingsManager"/>.
/// </remarks>
public static class ProjectManager
{
	#region Definitions
	public enum CONTENT_TYPE
	{
		RAW,
		PROCESSED
	}
	public enum FILE_TYPE
	{
		IMAGES,
		VIDEOS,
		PROTOCOLS
	}
	public enum PROCESSING_TYPE
	{
		DIFFERENCE,
		//TRANSFORMATION,	//UNUSED
	}
	#endregion
	#region Fields
	public const string Project_type = "Проект СРТУ";                    //NOTE const is considered static by compiler and has better performance than static readonly
	public const string Project_file_extension = ".cvfproj";
	public static readonly string Recordings_literal = $"{Path.DirectorySeparatorChar}Data";        //Using static readonly because string is interpolated
	private static DirectoryInfo _Project_directory, _Data_directory;
	private static FileInfo _Project_file;
	#region Fields.Results
	private static XLSXFile? _File_protocol;
	#endregion
	#region Fields.Utility
	private static readonly object _Locker=new object();
	#endregion
	#endregion
	#region Properties
	public static string Project_Title
	{
		get;
		private set;
	}
	public static DirectoryInfo Project_Directory
	{
		get { return _Project_directory; }
		private set
		{
			_Project_directory = value;
			if (!value.Exists)
				value.Create();
		}
	}
	//private static string Data_Directory_Path
	//{
	//	get;
	//	set;
	//}
	public static DirectoryInfo Data_Directory
	{
		get { return _Data_directory; }
		private set
		{
			_Data_directory = value;
			if (!value.Exists)
				value.Create();
		}
	}
	public static bool Is_Project_Opened
	{
		get;
		private set;
	}
	public static int Project_Options_Last_Position_In_File
	{
		get;
		private set;
	}
	#region Properties.Results
	public static XLSXFile? File_Protocol
	{
		get { return _File_protocol; }
		private set { _File_protocol = value; }
	}
	#endregion
	#endregion
	#region Methods
	/// <summary>
	///		Writes project properties to a project file.
	/// </summary>
	/// <returns>Is completed.</returns>
	private static async Task writeProjectDataAsync(bool is_overwriting_file = true)
	{
		string[] project_info = new[] {
			Project_Title,
			Project_Directory.FullName,
			Data_Directory.FullName
		};
		using StreamWriter stream_writer = _Project_file.Exists ? new StreamWriter(_Project_file.OpenWrite()) : new StreamWriter(_Project_file.Create());
		
		if (is_overwriting_file)
			stream_writer.BaseStream.SetLength(0);    //Clear file content
		for (int i = 0; i < project_info.Length; i++)
			await stream_writer.WriteLineAsync(project_info[i]).ConfigureAwait(false);
		await stream_writer.FlushAsync().ConfigureAwait(false);      //Flush to get accurate stream position
		Project_Options_Last_Position_In_File = (int)stream_writer.BaseStream.Position;
	}
	/// <summary>
	///		Creates project files and folders and then opens the project.
	/// </summary>
	/// <param name="title"></param>
	/// <param name="project_path"></param>
	/// <param name="data_path"></param>
	/// <returns>Project file path.</returns>
	public static async Task<string?> tryCreateAsync(string title, string project_path, string data_path)
	{
		try
		{
			Project_Title = title;
			Project_Directory = new DirectoryInfo(project_path);
			if (!Project_Directory.Exists)
				Project_Directory.Create();
			_Project_file = new FileInfo($"{project_path}{Path.DirectorySeparatorChar}config{Project_file_extension}");
			Data_Directory = new DirectoryInfo(data_path);
			if (!Data_Directory.Exists)
				Data_Directory.Create();
			await writeProjectDataAsync().ConfigureAwait(false);
			
			return _Project_file.FullName;
		}
		catch (Exception ex)	//TODO handle project exceptions (via MessageDialog?)
		{
			return null;
		}
	}
	/// <summary>
	///		Opens project file and read project properties. Returns non-empty error string if unsuccessfull.
	/// </summary>
	/// <param name="file"></param>
	/// <returns>status(is opened and read), error string.</returns>
	public static async Task<(bool, string)> tryOpenAsync(string file_path, bool is_overriding = false)  //Separation of concerns, views should not have such extensive logic although this would be more performant.
	{
		if (Is_Project_Opened && file_path == _Project_file?.FullName)
			return (false, "Этот проект уже открыт.");

		bool result = true;
		StringBuilder messages_error = new StringBuilder();
		string[] file_lines;

		_Project_file = new FileInfo(file_path);
		using (FileStream file_stream = _Project_file.OpenRead())
		{
			(file_lines, Project_Options_Last_Position_In_File) = file_stream.readLinesWithPosition(3);
		}

		if (!Directory.Exists(file_lines[1]))
		{
			return (false, "Директория проекта не найдена.");
		}
		Project_Directory = new DirectoryInfo(file_lines[1]);
		Data_Directory = Directory.CreateDirectory($"{file_lines[2]}{Path.DirectorySeparatorChar}Testing {DateTime.Now.ToString("dd.MM.yyyy HH-mm-ss")}");
		
		Is_Project_Opened = result;
		if (result)
		{
			Project_Title = file_lines[0];
			AppManager.Settings_Manager.readSettings(await readAdditionalProjectDataAsync().ConfigureAwait(false));
			if (is_overriding)
			{
				await writeProjectDataAsync().ConfigureAwait(false);
				await AppManager.Settings_Manager.saveSettingsAsync().ConfigureAwait(false);
			}
			projectOpened?.Invoke(null, EventArgs.Empty);
		}

		return (result, messages_error.ToString());
	}
	/// <summary>
	///		Closes project, calls method to save all individual project settings.
	/// </summary>
	/// <remarks>
	///		Called on app exit or when user manually closes project. Restores default settings if project is closed manually by user.
	/// </remarks>
	/// <param name="is_closing_on_app_exit"></param>
	/// <returns>Is closed</returns>
	public static async Task<bool> closeAsync(bool is_closing_on_app_exit = false)
	{
		bool result = true;

		projectClosed?.Invoke(null, EventArgs.Empty);

		if (is_closing_on_app_exit)
			return result;

		Is_Project_Opened = false;
		_Project_file = default;
		Project_Directory = default;
		Data_Directory = default;
		Project_Title = default;
		Project_Options_Last_Position_In_File = default;
		AppManager.Settings_Manager.restoreDefaultSettings();

		return result;
	}
	/// <summary>
	///		Reads additional project settings from file.
	/// </summary>
	/// <returns>Is completed.</returns>
	public static async Task<List<string>> readAdditionalProjectDataAsync()
	{
		List<string> lines = new List<string>();

		using (StreamReader stream_reader = new StreamReader(_Project_file.OpenRead()))
		{
			stream_reader.BaseStream.Position = Project_Options_Last_Position_In_File;
			while (!stream_reader.EndOfStream)
				lines.Add(await stream_reader.ReadLineAsync().ConfigureAwait(false));
		}

		return lines;
	}
	/// <summary>
	///		Writes additional project settings to file.
	/// </summary>
	/// <param name="position"></param>
	/// <param name="content"></param>
	/// <returns>Is completed.</returns>
	public static async Task<long> writeAdditinalProjectDataAsync(long position, string content)
	{
		using (StreamWriter stream_writer = new StreamWriter(_Project_file.OpenWrite()))
		{
			stream_writer.BaseStream.Position = position;
			await stream_writer.WriteLineAsync(content).ConfigureAwait(false);
			await stream_writer.FlushAsync().ConfigureAwait(false);
			return stream_writer.BaseStream.Position;
		}
	}
	/// <summary>
	///		Completely deletes additional project settings from file.
	/// </summary>
	public static void clearAdditionalProjectData()
	{
		using (FileStream file_stream = _Project_file.OpenWrite())
			file_stream.SetLength(Project_Options_Last_Position_In_File);
	}
	/// <summary>
	///		Gets pats to specific folder for saving data.
	/// </summary>
	/// <param name="video_stream_id"></param>
	/// <param name="description"><see cref="VideoStream.description"/></param>
	/// <param name="processing_type"></param>
	/// <returns>Path for saving data.</returns>
	public static string getDataPath(int video_stream_id, string description, CONTENT_TYPE content_type, FILE_TYPE file_type, int? frame_serie_number = null, PROCESSING_TYPE? processing_type=null)
	{
		StringBuilder string_builder = new StringBuilder();
		string path;

		string_builder.AppendFormat("{0}{1}videostream {2}({3}){4}", Data_Directory.FullName, Path.DirectorySeparatorChar, video_stream_id, description, Path.DirectorySeparatorChar);
		switch(content_type)
		{
			case CONTENT_TYPE.RAW:
				string_builder.AppendFormat("raw{0}", Path.DirectorySeparatorChar);
				break;
			case CONTENT_TYPE.PROCESSED:
				string_builder.AppendFormat("processed{0}", Path.DirectorySeparatorChar);
				break;
			default:
				throw new NotImplementedException();
		}
		switch (file_type)
		{
			case FILE_TYPE.IMAGES:
				string_builder.Append("images");
				break;
			case FILE_TYPE.VIDEOS:
				string_builder.Append("videos");
				break;
			case FILE_TYPE.PROTOCOLS:
				string_builder.Append("protocols");
				break;
			default:
				throw new NotImplementedException();
		}
		if (frame_serie_number != null)
			string_builder.AppendFormat("{0}{1}", Path.DirectorySeparatorChar, frame_serie_number.ToString());
		if(processing_type!=null)
		{
			switch (processing_type)
			{
				case PROCESSING_TYPE.DIFFERENCE:
					string_builder.AppendFormat("{0}difference", Path.DirectorySeparatorChar);
					break;
				default:
					throw new NotImplementedException();
			}
		}
		
		path = string_builder.ToString();
		if (!Directory.Exists(path))
			Directory.CreateDirectory(path);

		return path;
	}
	
	public static void disposeProtocolFile()
	{
		if (File_Protocol != null)
		{
			File_Protocol.Dispose();
			File_Protocol = null;
		}
	}
	#endregion
	#region Events
	public static event EventHandler projectClosed;
	public static event EventHandler projectOpened;
	#endregion
}
