using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Avalonia.Platform;
using Avalonia.Controls;
using Avalonia.Collections;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.Input;

using FCGR.Common.Utilities;
using FCGR.CommonAvalonia.MVVM;

using CMTP.Avalonia.Views;
using CMTP.Avalonia.Views.Controls.MainWindow;
using CMTP.Avalonia.Views.Windows;
using CMTP.Avalonia.ViewModels.Controls;
using CMTP.Avalonia.Models;
using CMTP.Avalonia.Managers;

namespace CMTP.Avalonia.ViewModels.Windows;

/// <summary>
///		Represents view model for <see cref="MainWindow"/>.
/// </summary>
public sealed class ViewModelMainWindow : ViewModel
{
	#region Fields
	private string _title;
	public readonly string title_default = "Испытания на М(н)ЦУ ";
	private int _number_cameras = 0;
	private ViewModelVideoForm? _view_model_video_form_selected;
	private VideoForm _video_form_selected;
	#region Fields.Avalonia
	private AvaloniaList<MenuItem> _menu_items;
	private AvaloniaList<VideoForm> _video_forms=new AvaloniaList<VideoForm>(), _video_forms_disaplayed_in_video_grid=new AvaloniaList<VideoForm>();
	private AsyncRelayCommand? _command_detect_cameras;
	private AsyncRelayCommand? _command_start_testing;
	private AsyncRelayCommand? _command_make_forecast;
	#endregion
	#endregion
	#region Properties
	public VideoForm Video_Form_Selected
	{
		get { return _video_form_selected; }
		set { _video_form_selected = value; OnPropertyChanged(); }
	}
	public ViewModelVideoForm? View_Model_Video_Form_Selected
	{
		get { return _view_model_video_form_selected; }
		set             //Putting such logic in setters is a bad practice, but VideoForm should always be highlighted if selected (not dependant on the toggleSelected() implementation)
		{
			if (_view_model_video_form_selected != null)
			{
				unsubscribeFromVideoFormSelectedEvents();
				_view_model_video_form_selected.videoStreamChanged -= onVideoFormSelectedVideoStreamChanged;
			}
			if (_view_model_video_form_selected != value)
			{
				_view_model_video_form_selected = value;
				subscribeToVideoFormSelectedEventsAndInvoke();
			}
			else
				_view_model_video_form_selected = null;
			OnPropertyChanged();
		}
	}
	#region Properties.Avalonia
	public bool Project_Manager_Is_Project_Opened_Wrapper	//HACK Avalonia can't bind to static properties
	{
		get { return ProjectManager.Is_Project_Opened; }
	}
	public string Title
	{
		get { return _title; }
		set { _title = value; OnPropertyChanged(); }
	}
	public AvaloniaList<MenuItem> Menu_Items
	{
		get { return _menu_items; }
		private set { _menu_items = value; OnPropertyChanged(); }
	}
	public AvaloniaList<VideoForm> Video_Forms
	{
		get { return _video_forms; }
		set { _video_forms = value; OnPropertyChanged(); }
	}
	public AvaloniaList<VideoForm> Video_Forms_Displayed_In_Video_Grid
	{
		get { return _video_forms_disaplayed_in_video_grid; }
		set { _video_forms_disaplayed_in_video_grid = value; OnPropertyChanged(); }
	}
	public IAsyncRelayCommand Command_Detect_Cameras
	{
		get
		{
			if (_command_detect_cameras == null)
				_command_detect_cameras = new AsyncRelayCommand(() => Task.Run(detectCameras)); //REVIEW method won't execute asynchronously withoud Task.Run() although it's being awaited?
			return _command_detect_cameras;
		}
	}
	public IAsyncRelayCommand Command_Video_Form_Start_Testing
	{
		get
		{
			if (_command_start_testing == null)
				_command_start_testing = new AsyncRelayCommand(async () =>
				{
					_view_model_video_form_selected.startTestProcessingAsync();
					await Task.Delay(50);
                    Dispatcher.UIThread.Post(Command_Video_Form_Start_Forecasting.NotifyCanExecuteChanged);
                },
				() => { return _view_model_video_form_selected != null && _view_model_video_form_selected.Video_Stream != null && ProjectManager.Is_Project_Opened; });
			return _command_start_testing;
		}
	}
	public IAsyncRelayCommand Command_Video_Form_Start_Forecasting
	{
		get
		{
			if(_command_make_forecast == null)
				_command_make_forecast = new AsyncRelayCommand(async () => 
				{
					await Task.Run(()=> _view_model_video_form_selected.startTestForecasting());
				},
				() => { 
					return _view_model_video_form_selected!=null && _view_model_video_form_selected.Testing_Processor!=null; });
			return _command_make_forecast;
		}
	}
	#endregion
	#endregion
	public ViewModelMainWindow()
	{ 
		Title = title_default;
		App app_current = (App)App.Current;

		Menu_Items = new AvaloniaList<MenuItem>
		{
			new MenuItem()
			{
				Header="Файл",
				Command=new RelayCommand(()=>{}),		//NOTE MenuItems are disabled if Command property is not set
				ItemsSource=new AvaloniaList<MenuItem> {
					new MenuItem()
					{
						Header="Создать/открыть проект...",
						Icon=new Image() { Source=new Bitmap(AssetLoader.Open(new Uri("avares://FCGR.CommonAvalonia/Resources/Assets/Images/Icons/tray_icon.ico")))},
						Command=new RelayCommand(app_current.showProjectWindow),
					},
					new MenuItem()
					{
						Header="Настройки",
						Command=app_current.Command_Open_Settings,
					},
					new MenuItem()
					{
						Header="Выход",
						Command=app_current.Command_Exit,
					}
				}
			},
			new MenuItem()
			{
				Header="Модули",
				ItemsSource=new AvaloniaList<MenuItem> {
					new MenuItem()
					{
						Header="Обработка видео",
						IsChecked=true,	//TEMP
						Command=new RelayCommand(app_current.showProjectWindow),
					},
					new MenuItem()
					{
						Header="Прогноз испытания",
						IsEnabled=false	//TEMP
					},
				}
			},
			new MenuItem()
			{
				Header="Справка",
				ItemsSource=new AvaloniaList<MenuItem> {
					new MenuItem()
					{
						Header="Документация",
					},
				}
			}
		};
#if DEBUG
		if (Design.IsDesignMode)
			return;
#endif
#if TRACE
		AppManager.Logger.printMessage("Трассировка дополнительных сообщений включена.", MESSAGE_SEVERITY.WARNING);
#endif
		AppManager.Logger.printMessage("Чтобы сохранять опорные кадры и записывать видео, создайте проект через Файл->Создать/открыть проект.", MESSAGE_SEVERITY.INFORMATION);
		AppManager.Logger.printMessage("Если при подключении камеры и нажатии на \"Обнаружить камеры\" камера не была обнаружена, подождите некоторое время и нажмите на кнопку еще раз.", MESSAGE_SEVERITY.INFORMATION);
		detectCameras();
		Video_Form_Selected = new(_number_cameras, new Bitmap(AssetLoader.Open(new Uri("avares://FCGR.CommonAvalonia/Resources/Assets/Images/image_error.png"))));
		View_Model_Video_Form_Selected = Video_Form_Selected.DataContext as ViewModelVideoForm;

		ProjectManager.projectOpened+=(sender, e) => Dispatcher.UIThread.Post(()=>
		{
			ViewModelMainWindow view_model_main_window = AppManager.Windows[nameof(ViewModelMainWindow)].DataContext as ViewModelMainWindow;
			view_model_main_window.Title = view_model_main_window.title_default + ProjectManager.Project_Directory.FullName;
			AppManager.Logger.printMessage($"Проект {ProjectManager.Project_Title} открыт.", MESSAGE_SEVERITY.COMMON);
			AppManager.Logger.logMessage($"Project path: {ProjectManager.Project_Directory.FullName}");
			Command_Video_Form_Start_Testing.NotifyCanExecuteChanged();
			OnPropertyChanged(nameof(Project_Manager_Is_Project_Opened_Wrapper));
		});
		ProjectManager.projectClosed+=(sender, e) =>
		{
			OnPropertyChanged(nameof(Project_Manager_Is_Project_Opened_Wrapper));
		};
	}
	#region Methods
	/// <summary>
	///		Detects plugged cameras by testing if their id is available.
	/// </summary>
	public void detectCameras()     //Crossplatform way //FIXME incorrect log if second camera in connected in runtime
	{
		VideoStream video_stream;
		HashSet<int> video_streams_ids_current = new HashSet<int>();
		int video_stream_id_max = -1;
		int number_cameras_current = 0;

		for (int i = 0; i < _video_forms.Count; i++)
		{
			ViewModelVideoForm view_model_video_form=null;
			Dispatcher.UIThread.Invoke(()=>view_model_video_form = _video_forms[i].DataContext as ViewModelVideoForm);	//HACK Avoiding call from invalid thread
			if (view_model_video_form.Video_Stream != null)
			{
				video_streams_ids_current.Add(view_model_video_form.Video_Stream.Id);
				if (view_model_video_form.Video_Stream.Id > video_stream_id_max)
					video_stream_id_max = view_model_video_form.Video_Stream.Id;
			}
		}
		while(true)
		{
			if (!video_streams_ids_current.Contains(number_cameras_current))
			{
				video_stream = new VideoStream(number_cameras_current);
				if (!video_stream.Is_Capturing)
					if (number_cameras_current > video_stream_id_max)
						break;
			}
			number_cameras_current++;
		}

		if (number_cameras_current != _number_cameras)
		{
			int number_cameras_difference = number_cameras_current - _number_cameras;
			if (number_cameras_difference > 0)
				AppManager.Logger.printMessage($"Обнаружены новые камеры: {number_cameras_difference} устр.", MESSAGE_SEVERITY.WARNING);
			else
				AppManager.Logger.printMessage($"Обнаружено отключение камер: {-number_cameras_difference} устр.", MESSAGE_SEVERITY.WARNING);
			_number_cameras = number_cameras_current;
			camerasNumberChanged?.Invoke(this, number_cameras_current);
		}
		else
			AppManager.Logger.printMessage($"Изменений в количестве подключенных камер ({_number_cameras} устр.) не обнаружено.", MESSAGE_SEVERITY.WARNING);
	}
	public void subscribeToVideoFormSelectedEventsAndInvoke()
	{
		_view_model_video_form_selected.videoStreamChanged += onVideoFormSelectedVideoStreamChanged;
		if(_view_model_video_form_selected.Video_Stream!=null)
		{
			_view_model_video_form_selected.Video_Stream.isStreamingChanged += onVideoFormSelectedVideoStreamIsStreamingChanged;
			onVideoFormSelectedVideoStreamChanged(this, EventArgs.Empty);
			onVideoFormSelectedVideoStreamIsStreamingChanged(this, _view_model_video_form_selected.Video_Stream.Is_Capturing);
		}
	}
	public void unsubscribeFromVideoFormSelectedEvents()
	{
		_view_model_video_form_selected.videoStreamChanged -= onVideoFormSelectedVideoStreamChanged;
		if (_view_model_video_form_selected.Video_Stream != null)
		{
			_view_model_video_form_selected.Video_Stream.isStreamingChanged -= onVideoFormSelectedVideoStreamIsStreamingChanged;
		}
	}
	#endregion
	#region Events
	public event EventHandler<int> camerasNumberChanged;
	private void onVideoFormSelectedVideoStreamChanged(object? sender, EventArgs e)
	{
		if (_view_model_video_form_selected.Video_Stream != null)
		{
			_view_model_video_form_selected.Video_Stream.isStreamingChanged -= onVideoFormSelectedVideoStreamIsStreamingChanged;	//Can't check if the event handler is null so the resubscribe approach is used
			_view_model_video_form_selected.Video_Stream.isStreamingChanged += onVideoFormSelectedVideoStreamIsStreamingChanged;
			Dispatcher.UIThread.Post(Command_Video_Form_Start_Testing.NotifyCanExecuteChanged);
        }
    }
	private void onVideoFormSelectedVideoStreamIsStreamingChanged(object? sender, bool is_streaming)
	{
		if (is_streaming)
		{
			//Dispatcher.UIThread.Post(Command_Video_Form_Start_Forecasting.NotifyCanExecuteChanged);
		}
	}
	#endregion
}
