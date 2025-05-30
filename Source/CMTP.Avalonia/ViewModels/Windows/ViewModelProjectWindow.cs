using System;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;

using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;

using CommunityToolkit.Mvvm.Input;

using FCGR.Common.Utilities;
using CMTP.Avalonia.Views.Windows;
using FCGR.CommonAvalonia.MVVM;
using CMTP.Avalonia.Managers;
using CMTP.Avalonia.Views.Controls.Project;

namespace CMTP.Avalonia.ViewModels.Windows;

/// <summary>
///		Represents view model for <see cref="ProjectWindow"/>.
/// </summary>
public sealed class ViewModelProjectWindow : ViewModel
{
	#region Fields
	private readonly Regex _regex_project_title = new Regex("^$|^([^\\/:*?\"<>|]\\s?)+$"), _regex_path = new Regex("^$|((([^\\/:*?\"<>|])+(\\s|/?)))+"), _regex_path_validation = new Regex($@"^([^{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}])+|({Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar})"), _regex_path_without_project_title = new Regex("");
	#endregion
	#region Fields.UI
	private bool _is_first_view_active;
	private string _project_title, _project_path, _recordings_path;
	private string? _error_message;
	private UserControl _user_control_current;
	private AsyncRelayCommand _command_switch_to_project_management_view, _command_switch_to_project_creation_view;
	private AsyncRelayCommand _command_locate_directory;
	private AsyncRelayCommand _command_open_project, _command_save_and_close;
	#endregion
	#region Properties.UI
	public string Title
	{
		get { return ProjectManager.Project_type; }
	}
	public string Project_Title     //REVIEW? to ProjectSettings
	{
		get { return _project_title; }
		set
		{
			if(value == null || _regex_project_title.IsMatch(value))
			{
				_project_title = value;
				OnPropertyChanged();
				if(String.IsNullOrEmpty(value))
				{
					Project_Path = null;
					Recordings_Path = null;
					((ProjectCreation)_user_control_current).textBox_title.BorderBrush = Brushes.Red;
				}
				else
				{
					if(Recordings_Path != null || !String.IsNullOrEmpty(Project_Path))
					{
						OnPropertyChanged(nameof(Project_Path));
						Recordings_Path = Project_Path + ProjectManager.Recordings_literal;
					}
					((ProjectCreation)_user_control_current).textBox_title.ClearValue(TextBox.BorderBrushProperty);
				}
				_command_save_and_close.NotifyCanExecuteChanged();
			}
			else
				((ProjectCreation)_user_control_current).textBox_title.Text=_project_title; //HACK Workaround for ui text updating independently from property
		}
	}
	public string? Project_Path
	{
		get
		{
			if(_project_path != null)
			{
				StringBuilder string_builder = new StringBuilder(_project_path);
				string_builder.AppendFormat("{0}{1}", Path.DirectorySeparatorChar, _project_title);
				return string_builder.ToString();
			}
			else
				return "";
		}
		set
		{
			if(value == null || _regex_path.IsMatch(value))	//BUG unhandled ArgumentOutOfRange exception
			{
				if(!String.IsNullOrEmpty(value))
				{
					if(_project_title != null && _project_path != null) //If user manually changes path
					{
						MatchCollection? matches = _regex_path_validation.Matches(value);
						if(_project_path == matches[0].Value || matches.Count>1)    //Don't let user type extra slashes
						{
							((ProjectCreation)_user_control_current).textBox_path.Text=_project_path;	//HACK Workaround for ui text updating independently from property
							return;
						}

						int project_title_new_length = 0;
						for(int i = value.Length-1; value[i]!=Path.DirectorySeparatorChar; i--) //Will be faster than regex "\\((?!\\).)*?$"
							project_title_new_length++;
						_project_path = value.Substring(0, Project_Path.Length - project_title_new_length);
					}
					else
						_project_path = value;
					Recordings_Path = Project_Path + ProjectManager.Recordings_literal;
				}
				OnPropertyChanged();
				_command_save_and_close.NotifyCanExecuteChanged();
			}
		}
	}
	public string? Recordings_Path
	{
		get { return _recordings_path; }
		set
		{
			if(value == null || _regex_path.IsMatch(value))
			{
				_recordings_path = value;
				OnPropertyChanged();
				_command_save_and_close.NotifyCanExecuteChanged();
			}
			else
				((ProjectCreation)_user_control_current).textBox_recordings_path.Text=_recordings_path;   //HACK Workaround for ui text updating independently from property
		}
	}
	public string? Error_Message
	{
		get { return _error_message; }
		set
		{
			if(value != null)
			{
				_error_message = "ОШИБКА: " + value;
				AppManager.Logger.logMessage(value, MESSAGE_SEVERITY.ERROR);
			}
			else
				_error_message = value;
			OnPropertyChanged();
		}
	}
	public bool Is_First_View_Active
	{
		get { return _is_first_view_active; }
		set { _is_first_view_active = value; OnPropertyChanged(); }
	}
	public UserControl User_Control_Current
	{
		get { return _user_control_current; }
		set
		{
			Is_First_View_Active = value is ProjectManagement;
			Error_Message = null;
			_user_control_current = value;
			OnPropertyChanged();
		}
	}

	public IAsyncRelayCommand Command_Switch_To_Project_Creation_View
	{
		get
		{
			if(_command_switch_to_project_creation_view == null)
			{
				_command_switch_to_project_creation_view = new AsyncRelayCommand(() =>
				{
					User_Control_Current = new ProjectCreation();
					return Task.CompletedTask;
				}, () => { return true; });
			}
			return _command_switch_to_project_creation_view;
		}
	}
	public IAsyncRelayCommand Command_Switch_To_Project_Management_View
	{
		get
		{
			if(_command_switch_to_project_management_view == null)
			{
				_command_switch_to_project_management_view = new AsyncRelayCommand(async () =>
				{
					User_Control_Current = new ProjectManagement();
				}, () => { return true; });
			}
			return _command_switch_to_project_management_view;
		}
	}
	public IAsyncRelayCommand Command_Open_Project
	{
		get
		{
			if(_command_open_project == null)
			{
				_command_open_project = new AsyncRelayCommand(async () =>
				{
					var files = await Window.GetTopLevel(User_Control_Current).StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
					{
						Title = "Открыть файл проекта",
						FileTypeFilter = new[] {
							new FilePickerFileType(ProjectManager.Project_type)
							{
								Patterns = new[] { "*"+ProjectManager.Project_file_extension }
							}
						},
						AllowMultiple = false
					});
					if(files.Count < 1)
						return;
					(bool, string) result = await ProjectManager.tryOpenAsync(files[0].Path.LocalPath);
					if(result.Item1)
						AppManager.Windows[nameof(ViewModelProjectWindow)].Close();
					else
						Error_Message = result.Item2;

				}, () => { return true; });
			}
			return _command_open_project;
		}
	}
	public IAsyncRelayCommand Command_Save_And_Close
	{
		get
		{
			if(_command_save_and_close == null)
			{
				_command_save_and_close = new AsyncRelayCommand(async () =>
				{
					string? project_file_name_full = await ProjectManager.tryCreateAsync(Project_Title, Project_Path, Recordings_Path);
					if(project_file_name_full!=null)
					{
						await ProjectManager.tryOpenAsync(project_file_name_full, true);
						AppManager.Windows[nameof(ViewModelProjectWindow)].Close();
					}
				}, () => { return Project_Title != null && Project_Path != null && Recordings_Path != null; });
			}
			return _command_save_and_close;
		}
	}
	public IAsyncRelayCommand Command_Locate_Directory
	{
		get
		{
			if(_command_locate_directory == null)
			{
				_command_locate_directory = new AsyncRelayCommand(async () =>       //TODO catch non existing dir exception
				{
					try
					{
						var folders = await Window.GetTopLevel(User_Control_Current).StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
						{
							Title = "Открыть папку проекта",
							AllowMultiple = false
						});
						if(folders.Count < 1)
							return;
						Project_Path = folders[0].TryGetLocalPath();    //Get path with special characters
						Error_Message = null;
					}
					catch(ArgumentException)
					{
						Error_Message = "Директория проекта не выбрана.";
						return;
					}
				}, () => true);
			}
			return _command_locate_directory;
		}
	}
	#endregion
}