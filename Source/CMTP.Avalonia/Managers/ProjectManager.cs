using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;

using Emgu.CV;

using ClosedXML.Excel;

using FCGR.Common.Utilities;
using FCGR.Common.Libraries.Models.Processors.Crack;
using FCGR.Common.Libraries.DataFiles;
using CMTP.Avalonia.Views.Windows;
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
			(file_lines, Project_Options_Last_Position_In_File) = file_stream.readLinesWithPosition(2);
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
		if ((bool)AppManager.Settings_Manager.Project_Is_Individual_Video_Stream_Setting_Enabled.Value || (bool)AppManager.Settings_Manager.Is_Saving_Opened_Video_Form_Count.Value)
		{
			result = await AppManager.Settings_Manager.saveSettingsAsync().ConfigureAwait(false);
		}

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
	private record struct CrackProcessorResultRecord
	{
		public readonly int frame_serie_number, crack_tip_location_x, crack_tip_location_y;
		public readonly string crack_length_formula, crack_length_growth_formula;

		public CrackProcessorResultRecord(int frame_serie_number, int crack_tip_location_x, int crack_tip_location_y)
		{
			this.frame_serie_number = frame_serie_number;
			this.crack_tip_location_x = crack_tip_location_x;
			this.crack_tip_location_y = crack_tip_location_y;
			crack_length_formula = $"=[@[{nameof(crack_tip_location_x)}]]-INDEX([{nameof(crack_tip_location_x)}];1)";		//TODO? Calculate distance between points?
			crack_length_growth_formula = $"=[@[{nameof(crack_tip_location_x)}]]-OFFSET([@[{nameof(crack_tip_location_x)}]];-1;0)";
		}
	}
	public static bool saveCrackProcessingResult(CrackProcessor.Result result, int video_stream_id, string video_stream_description, int frame_serie_number, bool is_converting_to_rgb=false)
	{
		string date_time_now = DateTime.Now.ToString("dd.MM.yyyy HH-mm-ss");

		if (result is CrackProcessorIntensityDifference.ResultIntensityDifference result_intensity_difference)
		{
			string path_data_images_processed = getDataPath(video_stream_id, video_stream_description, CONTENT_TYPE.PROCESSED, FILE_TYPE.IMAGES, frame_serie_number);
			string path_data_images_difference = getDataPath(video_stream_id, video_stream_description, CONTENT_TYPE.PROCESSED, FILE_TYPE.IMAGES, frame_serie_number, PROCESSING_TYPE.DIFFERENCE);
			string path_data_protocols = getDataPath(video_stream_id, video_stream_description, CONTENT_TYPE.PROCESSED, FILE_TYPE.PROTOCOLS);
			Mat frame_highlighted_crack = new Mat();
			string file_protocol_full_path = $"{path_data_protocols}{Path.DirectorySeparatorChar}protocol.xlsx";
			IXLWorksheet file_protocol_worksheet=null; string file_protocol_worksheet_name = "Результаты";
			IXLTable file_protocol_table=null; string file_protocol_table_name="Характеристики трещины";

			//AppManager.Logger.printMessage($"Длина трещины: {result.crack_length}.");
			//AppManager.Logger.printMessage($"Прирост за серию: {result.crack_length_growth}.");

			lock(_Locker)
			{
				if (File_Protocol == null)
				{
					File_Protocol = new(file_protocol_full_path);
					_File_protocol.onDispose += (sender, e) =>
					{
						file_protocol_table.Sort(nameof(CrackProcessorResultRecord.frame_serie_number), XLSortOrder.Ascending);
						file_protocol_worksheet?.Columns().AdjustToContents();
						_File_protocol.save();
					};
					_File_protocol.work_book.CalculationOnSave = true;
				}
				if (!File_Protocol.work_book.TryGetWorksheet(file_protocol_worksheet_name, out file_protocol_worksheet))
				{
					file_protocol_worksheet = File_Protocol.work_book.AddWorksheet(file_protocol_worksheet_name);
				}
				if (!file_protocol_worksheet.Tables.TryGetTable(file_protocol_table_name, out file_protocol_table))
				{
					file_protocol_worksheet.Cell(1, 1).Value = "Номер серии фреймов";
					file_protocol_worksheet.Cell(1, 2).Value = "Координта вершины трещины X";
					file_protocol_worksheet.Cell(1, 3).Value = "Координта вершины трещины Y";
					file_protocol_worksheet.Cell(1, 4).Value = "Длина трещины";
					file_protocol_worksheet.Cell(1, 5).Value = "Прирост длины трещины";

					file_protocol_table = file_protocol_worksheet.FirstCell().InsertTable(new CrackProcessorResultRecord[] {
						new CrackProcessorResultRecord(frame_serie_number, result.crack_tip_location.Value.X, result.crack_tip_location.Value.Y),
					}, file_protocol_table_name, true).SetShowHeaderRow().SetShowTotalsRow().SetShowRowStripes().SetShowColumnStripes();
					file_protocol_table.Field(nameof(CrackProcessorResultRecord.crack_tip_location_x)).TotalsRowFunction = XLTotalsRowFunction.None;
					file_protocol_table.Field(nameof(CrackProcessorResultRecord.crack_tip_location_y)).TotalsRowFunction = XLTotalsRowFunction.None;
					//file_protocol_table.Field(nameof(CrackProcessorResultRecord.crack_length)).TotalsRowFunction = XLTotalsRowFunction.StandardDeviation;
					//file_protocol_table.Field(nameof(CrackProcessorResultRecord.crack_length_growth)).TotalsRowFunction = XLTotalsRowFunction.Average;

					file_protocol_table.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				}
				else
				{
					file_protocol_table.AppendData(new CrackProcessorResultRecord[] {
						new CrackProcessorResultRecord(frame_serie_number, result.crack_tip_location.Value.X, result.crack_tip_location.Value.Y)
					});
				}
				file_protocol_worksheet.Columns().AdjustToContents();
				File_Protocol.save();
			}

			if (is_converting_to_rgb)       //HACK terrible, why... Maybe OpenCV 5 will change this
			{
				CvInvoke.CvtColor(result_intensity_difference.frame_highlighted_crack, frame_highlighted_crack, Emgu.CV.CvEnum.ColorConversion.Rgb2Bgr);
			}
			else
			{
				frame_highlighted_crack = result.frame_highlighted_crack;
			}
			CvInvoke.Imwrite($"{path_data_images_processed}{Path.DirectorySeparatorChar}{result.frame_number_in_serie}.jpg", frame_highlighted_crack);
			for (int i = 0; i < result_intensity_difference.frames_difference.Length; i++)
				CvInvoke.Imwrite($"{path_data_images_difference}{Path.DirectorySeparatorChar}{i}.jpg", result_intensity_difference.frames_difference[i]);
		}
		else if(result is CrackProcessorAI.ResultAI)
		{
			throw new NotImplementedException();
		}
		else
		{
			AppManager.Logger.logMessage($"Saving only basic info, saving for {result.GetType().Name} class is not implemented yet.", MESSAGE_SEVERITY.WARNING);
			string data_path_images = getDataPath(video_stream_id, video_stream_description, CONTENT_TYPE.PROCESSED, FILE_TYPE.IMAGES, frame_serie_number);
			CvInvoke.Imwrite($"{data_path_images}{Path.DirectorySeparatorChar}{frame_serie_number}{Path.DirectorySeparatorChar}{result.frame_number_in_serie}.jpg", result.frame_highlighted_crack);
		}

		return true;
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
