using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.IO;

using Avalonia;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Collections;
using Avalonia.Media.Imaging;
using Avalonia.Controls;
using Bitmap = Avalonia.Media.Imaging.Bitmap;
using PixelSize = Avalonia.PixelSize;

using CommunityToolkit.Mvvm.Input;

using Emgu.CV;
using Emgu.CV.CvEnum;

using ScottPlot;

using FCGR.CommonAvalonia.MVVM.Controls;
using FCGR.Common.Utilities;
using FCGR.CommonAvalonia.MVVM;
using CMTP.Avalonia.Managers;
using CMTP.Avalonia.Models;
using FCGR.Common.Libraries.Models.Processors.Testing;
using ScottPlot.Avalonia;
using System.Collections.Generic;
using FCGR.Client.GRPC.Streaming;
using FCGR.Client.Services.Testing;

namespace CMTP.Avalonia.ViewModels.Controls;

public sealed class ViewModelVideoForm : ViewModel //NOTE nested types are not supported in Avalonia.
{
	#region.Fields
	private VideoStream? _video_stream;
	private SensorStream? _sensor_stream;
	private Material _material = new();
	private TestingProcessorAI? _testing_processor_ai;
	private IServiceTesting _service_testing;
	private VideoStream.FrameBuffer? frame_buffer_streaming;
	private int _frames_count_received = 0;
	private const int _Fps_measure_interval_milliseconds = 600;
	private Stopwatch? _stopwatch_fps_measure_interval = null;
	private int _bitmap_fps_current = 1;
	#region Fields.Avalonia
	private WriteableBitmap? _bitmap;
	private readonly Bitmap _bitmap_error;
	public readonly System.Drawing.Size bitmap_default_resize_resolution = new System.Drawing.Size(854, 480); //Needed to dynamically scale bitmap according to Border bounds (improves performance)
	private bool _is_bitmap_null;
	private AvaloniaList<MenuActionBase> _context_menu_actions_active = new AvaloniaList<MenuActionBase>(), _context_menu_actions_form;
	private readonly AvaloniaList<Tuple<string, Action<Plot>>> _plot_context_menu_actions;
	#endregion
	#region Fields.Utility
	private Task _task_capture_frames, _task_receive_frames;
	private CancellationTokenSource? _cts_video_stream_capturing, _cts_test_processing, _cts_test_forecasting;
	#endregion
	#endregion
	#region Properties
	public SensorStream? Sensor_Stream
	{
		get {return _sensor_stream;}
		set { _sensor_stream = value; OnPropertyChanged(); }
	}
	public VideoStream? Video_Stream
	{
		get { return _video_stream; }
		set
		{
			if (_video_stream != value)
			{
				if (_video_stream != null)
				{
					_video_stream.PropertyChanged -= onVideoStreamPropertyChanged;
					_video_stream.onDisposed -= onVideoStreamStreamingDisposed;
				}
				if (value != null)
				{
					value.PropertyChanged += onVideoStreamPropertyChanged;
					value.onDisposed += onVideoStreamStreamingDisposed;
				}
				else
				{
					Is_Bitmap_Null = true;
				}
			}
			_video_stream = value;
			OnPropertyChanged();
			videoStreamChanged?.Invoke(this, EventArgs.Empty);
		}
	}
	public Material Material
	{
		get { return _material; }
		private set { _material = value; OnPropertyChanged(); }
	}
	public TestingProcessorAI? Testing_Processor
	{
		get { return _testing_processor_ai; }
		set { _testing_processor_ai = value; OnPropertyChanged(); }
	}
    #region Properties.Avalonia
    public WriteableBitmap? Bitmap  //Image control changes between two styles with bindings to Bitmap and Bitmap_Error
	{
		get { return _bitmap; }
		set { _bitmap = value; OnPropertyChanged(); }
	}
	public Bitmap Bitmap_Error
	{
		get { return _bitmap_error; }
	}
	public bool Is_Bitmap_Null  //TargetNullValue can't be used, other ways would create extra overhead
	{
		get { return _is_bitmap_null; }
		set     //if true sets Bitmap to null to avoid null checking in Bitmap set.
		{
			if (value)
				Bitmap = null;
			_is_bitmap_null = value;
			OnPropertyChanged();
		}
	}
	public AvaloniaList<MenuActionBase> Context_Menu_Actions_Active
	{
		get { return _context_menu_actions_active; }
		private set { _context_menu_actions_active = value; OnPropertyChanged(); }
	}
	public AvaPlot Plot_Area
	{
		get;
		set;
	}
	public AvaPlot Plot_Sm
	{
		get;
		set;
	}
	public AvaPlot Plot_SLbu
	{
		get;
		set;
	}
	#endregion
	#region Properties.Commands
	#region Properties.Interface
	public Func<Task> updateImage
	{
		get;
		set;
	}
	public Action<Cursor> changeCursor
	{
		get;
		set;
	}
	public Func<TopLevel?> getTopLevelWindow
	{
		get;
		set;
	}
	#endregion
	#endregion
	#endregion
#if DEBUG
	public ViewModelVideoForm()
	{
		Bitmap = null;
	}
#endif
	public ViewModelVideoForm(int cameras_number_current, Bitmap bitmap_error)        //NOTE ref parameters cannot be used inside lambdas
	{
		_bitmap_error = bitmap_error;
		Is_Bitmap_Null = true;
		_context_menu_actions_form = new AvaloniaList<MenuActionBase> {
			new MenuAction("Выбрать испытательную машину",  (object? parameter)=>true, (object? parameter)=>{}, (object? parameter)=>_context_menu_actions_form[0].Actions_Submenu.Count>0, icon:new Bitmap(AssetLoader.Open(new Uri("avares://FCGR.CommonAvalonia/Resources/Assets/Images/Icons/tray_icon.ico")))),
			new MenuActionAsync("Остановить видеопоток",
			(object? parameter)=>
			{
				return _video_stream!=null ? _video_stream.Is_Capturing : false;
			},
			async (object? parameter)=>
			{
				await disableSensorStreamAsync();
				await disableVideoStreamAsync();
			}),
			new MenuAction("Поставить видеопоток на паузу",
			(object? parameter)=>
			{
				return _video_stream!=null ? !_video_stream.Is_Capturing_Paused : false;
			},
			async (object? parameter)=>
			{
				_video_stream.Is_Capturing_Paused = true;
			}),
			new MenuAction("Возобновить видеопоток",
			(object? parameter)=>
			{
				return _video_stream!=null ? _video_stream.Is_Capturing_Paused : false;
			},
			async (object? parameter)=>
			{
				_video_stream.Is_Capturing_Paused = false;
			}),
		};
		Context_Menu_Actions_Active.AddRange(_context_menu_actions_form);
		_plot_context_menu_actions = new AvaloniaList<Tuple<string, Action<Plot>>>() {
			new Tuple<string, Action<Plot>>("Авто-масштаб",  new Action<Plot>((Plot plot) =>
			{
				try
				{
					plot.Axes.AutoScale();
				}
				catch(Exception)
				{
					AppManager.Logger.printMessage($"Произошла неизвестная ошибка во время масштабирования графика.", MESSAGE_SEVERITY.ERROR);
				}
			})),
		};

		Plot_Area = new();
		Plot_Sm = new();
		Plot_SLbu = new ();
		Plot_Area.Menu.Clear();
		Plot_Sm.Menu.Clear();
		Plot_SLbu.Menu.Clear();
		for (int i = 0; i < _plot_context_menu_actions.Count; i++)
		{
			Plot_Area.Menu.Add(_plot_context_menu_actions[i].Item1, _plot_context_menu_actions[i].Item2);
			Plot_Sm.Menu.Add(_plot_context_menu_actions[i].Item1, _plot_context_menu_actions[i].Item2);
			Plot_SLbu.Menu.Add(_plot_context_menu_actions[i].Item1, _plot_context_menu_actions[i].Item2);
		}
		Plot_Area.Plot.Axes.Title.Label.Text = "Площадь петли гистерезиса area VS текущий номер цикла N_current";
		Plot_Area.Plot.Axes.Bottom.Label.Text = "N_current";
		Plot_Area.Plot.Axes.Left.Label.Text = "area";
		Plot_Sm.Plot.Axes.Title.Label.Text = "Коэффициент деформация S_max vs текущий номер цикла N_current";
		Plot_Sm.Plot.Axes.Bottom.Label.Text = "N_current";
		Plot_Sm.Plot.Axes.Left.Label.Text = "S_max";
		Plot_SLbu.Plot.Axes.Title.Label.Text = "Коэффициент деформация S_level_bottom_up vs текущий номер цикла N_current";
		Plot_SLbu.Plot.Axes.Bottom.Label.Text = "N_current";
		Plot_SLbu.Plot.Axes.Left.Label.Text = "S_level_bottom_up";

		changeBitmapSize(new PixelSize(bitmap_default_resize_resolution.Width, bitmap_default_resize_resolution.Height));
		updateCamerasNumber(this, cameras_number_current);
	}
	#region Methods
	public void updateCamerasNumber(object? sender, int cameras_number)
	{
		_context_menu_actions_form[0].Actions_Submenu.Clear();
		for (int i = 0; i < cameras_number; i++)
		{
			_context_menu_actions_form[0].Actions_Submenu.Add(new MenuActionAsync(i.ToString(), (object? parameter) => true, (object? parameter) => { enableSensorStreamingAsync(parameter.ToString()); return enableStreamingAsync(parameter.ToString()); },
			(object? parameter) =>
			{
				if (_video_stream != null)
				{
					bool is_checked = _video_stream.Id == (int)parameter;
					_context_menu_actions_form[0].Actions_Submenu[(int)parameter].Is_Checked = is_checked;
					return !is_checked;
				}
				else
				{
					if (parameter != null)
						_context_menu_actions_form[0].Actions_Submenu[(int)parameter].Is_Checked = false;
					return true;
				}
			}, i, toggle_type: MenuItemToggleType.CheckBox));
		}
	}
	[Obsolete]
	public void changeBitmapSize(PixelSize size_new)
	{
		if (size_new.Width == 0 || size_new.Height == 0)
			return;
		Bitmap = new WriteableBitmap(size_new, new Vector(96, 96), PixelFormats.Rgb24, AlphaFormat.Opaque);     //Vector magic numbers probably won't change, so...
	}
	private async Task setBitmapFromMatAsync(Mat frame, bool is_measuring_fps=true)   //No way to easily improve the performance of this method
	{
		int bitmap_width = (int)Bitmap.Size.Width, bitmap_height = (int)Bitmap.Size.Height;
		if (bitmap_width != frame.Width || bitmap_height != frame.Height) //Needed for e.g. crop result saving
			CvInvoke.Resize(frame, frame, new System.Drawing.Size(bitmap_width, bitmap_height), 0, 0, Inter.Nearest);
		byte[] frame_data = frame.GetRawData();
		using (ILockedFramebuffer bitmap_buffer = _bitmap.Lock()) //CAUTION OnPropertyChanged(Bitmap) is not invoked for better performance //NOTE Wrapping this in lock causes AccessViolationException when different lock in different thread is used for the same variable
		{
			System.Runtime.InteropServices.Marshal.Copy(frame_data, 0, bitmap_buffer.Address, frame_data.Length);  //BUG ExecutionEngineException memmove, separate var for dst in resize does not help, check if frame.Dispose() is guilty
		}
		Task task=updateImage();
		_frames_count_received++;
		if(is_measuring_fps)
		{
			if (_stopwatch_fps_measure_interval.Elapsed.TotalMilliseconds >= _Fps_measure_interval_milliseconds)
			{
				_bitmap_fps_current = (int)(_frames_count_received / _stopwatch_fps_measure_interval.Elapsed.TotalMilliseconds * 1000);
				_frames_count_received = 0;
				_stopwatch_fps_measure_interval.Restart();
			}
		}
		await task;
	}
	public async Task enableStreamingAsync(object? camera_id_or_video_path) //FIXME initial bitmap resolution is not default
	{
		if (_cts_video_stream_capturing != null)
			await disableVideoStreamAsync();
		changeCursor(new Cursor(StandardCursorType.Wait));

		int camera_id = Int32.TryParse((string)camera_id_or_video_path, out camera_id) ? camera_id : -1;

		Video_Stream = new VideoStream(camera_id, 1280, 720, 30);
		_cts_video_stream_capturing = new CancellationTokenSource();
		if (await Task.Run(_video_stream.enable).ConfigureAwait(false))    //NOTE Task.Start() is deprecated
		{
			Is_Bitmap_Null = false;
			frame_buffer_streaming = _video_stream.getAvailableFrameBuffer(3);
			_task_capture_frames = Task.Factory.StartNew(async () =>
			{
				_video_stream.startCapturingFrames(_cts_video_stream_capturing.Token);
				//Code below disposes VideoStream and completes all running operations
				changeCursor(new Cursor(StandardCursorType.Wait));
				_video_stream.Is_Capturing_Paused = false;          //Otherwise cancellation tokens will not work
				if (_cts_video_stream_capturing != null)
				{
					await _cts_video_stream_capturing.CancelAsync().ConfigureAwait(false);
					await _task_receive_frames;
					_cts_video_stream_capturing.Dispose();
					_cts_video_stream_capturing = null;
				}
				_video_stream.Dispose();        //CAUTION
				Video_Stream = null;
				_stopwatch_fps_measure_interval = null;
				Is_Bitmap_Null = true;
				changeCursor(Cursor.Default);
			}, default, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
			_task_receive_frames = Task.Factory.StartNew(async () =>
			{
				_stopwatch_fps_measure_interval = Stopwatch.StartNew();
				while (_video_stream.Is_Capturing && !_cts_video_stream_capturing.Token.IsCancellationRequested)
				{
					if (frame_buffer_streaming.TryDequeue(out Mat frame_current))
					{
						//if (_video_stream.frame_color_format == ColorConversion.Bgr2Rgb)
						//CvInvoke.CvtColor(frame_current, frame_current, ColorConversion.Bgr2Rgb);    //BUG Crash with AccessViolationException, tried creating separate variable and lock()
						await setBitmapFromMatAsync(frame_current);
					}
					else if (_video_stream.Is_Capturing_Paused)
						_video_stream.auto_reset_event_streaming.WaitOne();
				}
				frame_buffer_streaming.Dispose();
				
			}, _cts_video_stream_capturing.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
			changeCursor(Cursor.Default);
		}
	}
	public async Task<bool> disableSensorStreamAsync()
	{
		if (_cts_video_stream_capturing != null)
		{
			await _cts_test_processing.CancelAsync();
			return true;
		}
		else
			return false;
	}
	public async Task<bool> disableVideoStreamAsync()
	{
		if (_cts_video_stream_capturing != null)
		{
			await _cts_video_stream_capturing.CancelAsync();
			return true;
		}
		else
			return false;
	}
	public void enableSensorStreamingAsync(object? machine_id)
	{
		int machine_id_as_int = Int32.Parse(machine_id as string);

		Sensor_Stream = new(new(machine_id_as_int, _material));
	}
	public async Task startTestProcessingAsync()
	{
		_cts_test_processing = new();
        Testing_Processor = new();
		_service_testing = new GRPCServiceTesting(ServerManager.Server_Endpoint);
		_testing_processor_ai.Forecast_Horizon = 1;
		_testing_processor_ai.PropertyChanged += (o, e)=>Task.Run(async ()=>await _service_testing.sendTestingParameters(_testing_processor_ai));
		await _service_testing.sendTestingParameters(_testing_processor_ai);
		Sensor_Stream.start();
        Sensor_Stream.Testing_Machine.start();

		List<int> N_values = new();
		List<float> area_values = new();
		List<float> Sm_values = new();
		List<float> SLbu_values = new();

		Plot_Area.Plot.Add.Scatter(N_values, area_values);
		Plot_Sm.Plot.Add.Scatter(N_values, Sm_values);
		Plot_SLbu.Plot.Add.Scatter(N_values, SLbu_values);

		await foreach (var sensor_data in _sensor_stream.streamDataAsync($"Data{Path.DirectorySeparatorChar}Datasets{Path.DirectorySeparatorChar}673{Path.DirectorySeparatorChar}", _cts_test_processing.Token))
		{
			var sendDataAsync= _service_testing.sendDataAsync(sensor_data);
			N_values.Add((int)sensor_data.x[^1]);
			area_values.Add(sensor_data.y[0]);
			Sm_values.Add(sensor_data.y[1]);
			SLbu_values.Add(sensor_data.y[2]);
			Plot_Area.Plot.Axes.AutoScale();
			Plot_Area.Refresh();
			Plot_Sm.Plot.Axes.AutoScale();
			Plot_Sm.Refresh();
			Plot_SLbu.Plot.Axes.AutoScale();
			Plot_SLbu.Refresh();
			await sendDataAsync;
		}
    }
	public async Task startTestForecasting()
	{
		_cts_test_forecasting = new();

		List<int> N_values = new();
		List<float> area_values = new();
		List<float> Sm_values = new();
		List<float> SLbu_values = new();

		Plot_Area.Plot.Add.Scatter(N_values, area_values);
		Plot_Sm.Plot.Add.Scatter(N_values, Sm_values);
		Plot_SLbu.Plot.Add.Scatter(N_values, SLbu_values);

		while (!_cts_test_forecasting.IsCancellationRequested)
		{
			var task = _service_testing.receiveForecast();
			N_values.Clear();
			area_values.Clear();
			Sm_values.Clear();
			SLbu_values.Clear();
			var result=await task;
			for(int i=0; i<result.predictions.Length; i++)
			{
				N_values.Add(result.N_predictions_start+i);
				area_values.Add(result.predictions[i][0]);
				Sm_values.Add(result.predictions[i][1]);
				SLbu_values.Add(result.predictions[i][2]);
			}
			await Task.Delay(Sensor_Stream.Testing_Machine.Sensors_Update_Interval);
		}
	}
	#endregion
	#region Events
	public event EventHandler videoStreamChanged;
	public event EventHandler isProcessingEnabledChanged;
	private void onVideoStreamStreamingDisposed(object? sender, EventArgs e)
	{
		Video_Stream = null;
	}
	private async void onVideoStreamPropertyChanged(object? sender, PropertyChangedEventArgs e)     //TODO use events for better performance?
	{
		switch (e.PropertyName)
		{
			case nameof(_video_stream.Is_Capturing):
				goto case nameof(_video_stream.Is_Capturing_Paused);
			case nameof(_video_stream.Is_Capturing_Paused):
				break;
			case nameof(_video_stream.Frame_Current):
				if(_video_stream.Frame_Current!=null)
					if (_video_stream.Is_Capturing_Paused && frame_buffer_streaming.Is_Empty)
						await setBitmapFromMatAsync(_video_stream.Frame_Current.Clone(), false);
				break;
			default:
				break;
		}
	}
	#endregion
}