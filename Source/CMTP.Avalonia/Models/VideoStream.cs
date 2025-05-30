using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;

using Emgu.CV;
using Emgu.CV.CvEnum;

using FCGR.Common.Libraries;
using FCGR.Common.Libraries.Processors;
using FCGR.Common.Utilities;
using FCGR.Common.Libraries.System;
using CMTP.Avalonia.Managers;
using CMTP.Avalonia.Views.Windows;
using Avalonia.Threading;

namespace CMTP.Avalonia.Models;

/// <summary>
///		Class used for streaming frames from camera/video file, recording frames, preprocessing, accessing camera interface
/// </summary>
public sealed class VideoStream : Model, IDisposable
{
	#region Definitions
	/// <summary>
	///		Class used for storing frames received from stream.
	/// </summary>
	public sealed class FrameBuffer : IDisposable
	{
		public readonly int id;
		public int size;
		private readonly ConcurrentQueue<Mat> _frames;
		public bool is_enabled=true;
		public bool is_preventing_overflow_override;	//Makes streamer to stop filling up queue with new frames if frame buffer is full
		public readonly Func<object?, Mat> func_callback;//TODO? For processing on add frame append

		public int Frames_Count
		{
			get { return _frames.Count; }
		}
		public bool Is_Empty
		{
			get { return _frames.IsEmpty; }
		}

		public FrameBuffer(int id, int size, bool is_preventing_overflow_override)
		{
			this.id=id;
			this.size=size;
			_frames = new();
			this.is_preventing_overflow_override = is_preventing_overflow_override;
		}
		~FrameBuffer()
		{
			dispose(false);
		}

		public void Enqueue(Mat frame)
		{
			_frames.Enqueue(frame);
			if (is_preventing_overflow_override && _frames.Count== size)
				onFilled?.Invoke(this, EventArgs.Empty);
		}

		public bool TryDequeue(out Mat result)
		{
			bool success = _frames.TryDequeue(out result);
			if (success && is_preventing_overflow_override && _frames.Count-1==size) 
				onUnfilled?.Invoke(this, EventArgs.Empty);
			return success;
		}
		private void dispose(bool is_explicit)
		{
			onDispose?.Invoke(this, id);
			if (is_explicit)
			{
				_frames.DisposeElements();
			}
		}
		public void Dispose()
		{
			dispose(true);
			GC.SuppressFinalize(this);
		}
		#region Events
		public event EventHandler<int> onDispose;
		public event EventHandler onFilled;
		public event EventHandler onUnfilled;
		#endregion
	}
	#endregion
	#region Fields
	private int _id;
	private VideoCapture _video_capture;
	private Mat? _frame_current;
	private int _frame_width, _frame_height, _frames_per_second;
	private const uint _frame_buffers_count_max=16;
	private readonly ConcurrentDictionary<int, FrameBuffer> _dict_id_frame_buffer = new ConcurrentDictionary<int, FrameBuffer>();
	private FrameBuffer _frame_buffer_raw;
	private HashSet<int> _frame_buffers_available_ids=new HashSet<int>();
	private bool _is_capturing = false, _is_capturing_paused = false;
	#region Fields.Utility
	public readonly ManualResetEvent auto_reset_event_streaming = new (false);		//BUG preprocessing thread does not come out of suspended state if WaitOne() is called
	public readonly object locker_frame_current = new object();
	#endregion
	#endregion
	#region Properties
	public int Id
	{
		get { return _id; }
		private set { _id = value; OnPropertyChanged(); }
	}

	public Mat? Frame_Current
	{
		get { return _frame_current; }
		set 
		{
			_frame_current = value;
			OnPropertyChanged();
		}
	}
	public bool Is_Capturing
	{
		get { return _is_capturing; }
		private set { _is_capturing = value; OnPropertyChanged(); isStreamingChanged?.Invoke(this, value); }
	}
	public bool Is_Capturing_Paused
	{
		get { return _is_capturing_paused; }
		set 
		{
			_is_capturing_paused = value;
			OnPropertyChanged();
			if (!value)
			{
				auto_reset_event_streaming.Set();
				auto_reset_event_streaming.Reset();
			}
		}
	}
	public int Streaming_Interval_Milliseconds  //Time to sleep in ms between frames, used for video file streaming and throttling
	{
		get;
		set;
	}

	#endregion
	public VideoStream(int id, int frame_width, int frame_height, int frames_per_second)
	{
		_id=id;
		_frame_width = frame_width;
		_frame_height=frame_height;
		_frames_per_second = frames_per_second;
	}
	public VideoStream(int id)	
	{
		_id = id;
		_is_capturing = tryInitialise();
	}
	~VideoStream()
	{
		dispose(false).Wait();
	}
	#region Methods
	public FrameBuffer getAvailableFrameBuffer(int buffer_size = 1, bool is_preventing_overflow_override=false)
	{
		int frame_buffer_id;

		lock (_frame_buffers_available_ids)
		{
			if (_frame_buffers_available_ids.Count == 0)
				throw new KeyNotFoundException("There are no free frame buffers available.");
			frame_buffer_id = _frame_buffers_available_ids.First();
			_frame_buffers_available_ids.Remove(frame_buffer_id);
		}
		FrameBuffer frame_buffer = new FrameBuffer(frame_buffer_id, buffer_size, is_preventing_overflow_override);
		if(is_preventing_overflow_override)
		{
			frame_buffer.onFilled += (sender, e) => { Is_Capturing_Paused = true; };
			frame_buffer.onUnfilled += (sender, e) => { Is_Capturing_Paused = false; };
		}
		frame_buffer.onDispose += (sender, id) =>       ///Makes <see cref="FrameBuffer"/> id available.
		{
			_dict_id_frame_buffer.TryRemove(id, out var __);
			_frame_buffers_available_ids.Add(id);
		};
		_dict_id_frame_buffer.TryAdd(frame_buffer_id, frame_buffer);
		
		return frame_buffer;
	}
	private bool tryInitialise(VideoCapture.API api=VideoCapture.API.DShow, string path_video_file = "")    //TODO... API.Any (fix long app startup somehow)
	{
		if(_video_capture==null)
		{
			Mat frame=new Mat();

			_video_capture = new VideoCapture(_id, api, new Tuple<CapProp, int>(CapProp.HwAcceleration, (int)VideoAccelerationType.Any));	//BUG Dshow sometimes is not detecting cameras connected in runtime, api.ANY doesn't fix this
			if (!_video_capture.IsOpened)
				return false;
			try			//HACK For randomly appearing phantom camera device which passes IsOpen check but not passes Read().
			{
				_video_capture.Read(frame);
			}
			catch(Exception)
			{
				Tracer.traceMessage($"Phantom device with id{_id} detected!", MESSAGE_SEVERITY.WARNING);
				return false;
			}
			return !frame.IsEmpty;
		}
		return _video_capture.Grab();
	}
	public bool enable()
	{
		if (!tryInitialise())
		{
			Tracer.traceMessage($"Не удалось инициализировать видеопоток {_id}.", MESSAGE_SEVERITY.ERROR, flags:Tracer.TRACE_FLAG.PRINT);
			return false;
		}

		_video_capture.Set(CapProp.FrameWidth, _frame_width);
		_video_capture.Set(CapProp.FrameHeight, _frame_height);
		_video_capture.Set(CapProp.FourCC, VideoWriter.Fourcc('M', 'J', 'P', 'G'));
		_video_capture.Set(CapProp.Buffersize, 1);

		if (!_video_capture.IsOpened)
		{
			Tracer.traceMessage($"Ошибка запуска видеопотока {_id}.", MESSAGE_SEVERITY.ERROR, flags:Tracer.TRACE_FLAG.PRINT);
			return false;
		}
		Tracer.traceMessage($"Запуск видеопотока {_id}.", flags:Tracer.TRACE_FLAG.PRINT);

		_frame_buffer_raw = new(-1, 3, true);
		_frame_buffer_raw.onFilled += async (sender, e) =>
		{
			Is_Capturing_Paused = true;
			await Dispatcher.UIThread.InvokeAsync(async ()=> //REVIEW! or remove that and just pause video stream until after all frames from frame buffer are dequeued
			{
				Streaming_Interval_Milliseconds += 16;
			});
			Is_Capturing_Paused = false;
		};
		_frame_buffer_raw.onUnfilled += (sender, e) => { Is_Capturing_Paused = false; };
		for (int i = 0; i < _frame_buffers_count_max; i++)
			_frame_buffers_available_ids.Add(i);

		return true;
	}
	private Mat? captureFrame()
	{
		Mat? frame=new Mat();

		try
		{
			if (!_video_capture.Read(frame))		//BUG Occasional MemAccessViolation when openning multiple cameras/stopping video???
			{
				Tracer.traceMessage($"Не удалось получить кадр из видеопотока {_id}.", MESSAGE_SEVERITY.WARNING, flags:Tracer.TRACE_FLAG.PRINT);
				return null;
			}
		}
		catch (Exception ex) 
		{
			Tracer.traceMessage($"Произошла ошибка при попытке получить кадр из видеопотока {_id}. См. лог файл для подробной информации.", MESSAGE_SEVERITY.ERROR, flags:Tracer.TRACE_FLAG.PRINT);
			Tracer.traceMessage(ex.Message, MESSAGE_SEVERITY.ERROR);
		}

		return frame;
	}
	private void broadcastFrame(Mat frame)
	{
		foreach (var kv in _dict_id_frame_buffer)
		{
			if (kv.Value.is_enabled)
			{
				kv.Value.Enqueue(frame.Clone());
				if (kv.Value.Frames_Count > kv.Value.size)
				{
					kv.Value.TryDequeue(out Mat __);    //NOTE Discards doesn't call Dispose()
					__?.Dispose();
				}
			}
		}
		lock (locker_frame_current)
		{
			Frame_Current = frame;
		}
	}
	public void startCapturingFrames(CancellationToken cancellation_token)	//NOTE If unmanaged VideoCapture object is not freed after stopping, opening the same camera will result in crash.
	{
		Is_Capturing = true;	//CAUTION Don't move it any lower - some functions in VideoForm depend on execution order
		Mat? frame;
		Thread thread_preprocess_and_broadcast_frames = new Thread(()=> 
		{
			while (_is_capturing || !_frame_buffer_raw.Is_Empty)
			{
				if (_is_capturing_paused && _frame_buffer_raw.Is_Empty)
					auto_reset_event_streaming.WaitOne();
				if(_frame_buffer_raw.TryDequeue(out Mat frame))
				{
					broadcastFrame(frame);
				}
			}
		});
		const int unsuccessfull_attempts_to_capture_frame_since_last_successfull_attempt_max=3;	//Hardcoded
		int unsuccessfull_attempts_to_capture_frame_since_last_successfull_attempt=0;
		Stopwatch stopwatch_frame_capture = new Stopwatch();
		int fps_measure_cycle_duration_milliseconds = 500, fps_measure_cycle_iterations_count_current = 0, fps_measure_cycle_milliseconds_elapsed = 0;  //Calculates average fps for given cycle duration
		double frame_capture_duration_milliseconds;

		thread_preprocess_and_broadcast_frames.Start();
		while(!cancellation_token.IsCancellationRequested)
		{
			if (_is_capturing_paused)
				auto_reset_event_streaming.WaitOne();  //TODO Check if the timers are still running
			stopwatch_frame_capture.Restart();
			frame = captureFrame();
			if (frame == null)
			{
				if (unsuccessfull_attempts_to_capture_frame_since_last_successfull_attempt >= unsuccessfull_attempts_to_capture_frame_since_last_successfull_attempt_max)
				{
					lock (locker_frame_current)
					{
						Frame_Current = null;
					}
					Helper.DebugWriteLine($"Stopped capturing due to unsucessfull attempts: {unsuccessfull_attempts_to_capture_frame_since_last_successfull_attempt}.", MESSAGE_SEVERITY.ERROR);
					break;
				}
				unsuccessfull_attempts_to_capture_frame_since_last_successfull_attempt++;
				Thread.Sleep(5000);
			}
			else
			{
				stopwatch_frame_capture.Stop();
				frame_capture_duration_milliseconds = stopwatch_frame_capture.Elapsed.TotalMilliseconds;
				fps_measure_cycle_milliseconds_elapsed += (int)stopwatch_frame_capture.ElapsedMilliseconds;
				fps_measure_cycle_iterations_count_current++;
				if (fps_measure_cycle_milliseconds_elapsed > fps_measure_cycle_duration_milliseconds)
				{
					_frames_per_second = 1000 / (fps_measure_cycle_milliseconds_elapsed / fps_measure_cycle_iterations_count_current);
					fps_measure_cycle_iterations_count_current = 0;
					fps_measure_cycle_milliseconds_elapsed = 0;
				}
				_frame_buffer_raw.Enqueue(frame);
				unsuccessfull_attempts_to_capture_frame_since_last_successfull_attempt = 0;
				if (Streaming_Interval_Milliseconds >= 16)    //Magic number, Thread.Sleep() is useleses when number is < 16
					Thread.Sleep(Streaming_Interval_Milliseconds);
			}
		}
		Is_Capturing = false;
		Frame_Current = null;
		isStreamingChanged = null;
		AppManager.Logger.logMessage($"Остановка видеопотока {_id}.", is_printing: true);
	}
	public void openSettings()		//REVIEW this method is for cameras only, add validation for video files.
	{
		_video_capture.Set(CapProp.Settings, 1);
	}
	#region Methods.System
	private async Task dispose(bool is_explicit)
	{
		if(is_explicit)             //Dispose managed objects, set large fields to null.
		{
			while (!_dict_id_frame_buffer.IsEmpty)  //Wait until all frame buffers become empty
			{
				await Task.Delay(125).ConfigureAwait(false);
			}
		}
		_video_capture.Dispose();	//Dispose unmanaged objects.
	}
	public void Dispose()
	{
		dispose(true).Wait();
		GC.SuppressFinalize(this);
		onDisposed?.Invoke(this, EventArgs.Empty);
	}
	#endregion
	#endregion
	#region Events
	public event EventHandler baseFrameChanged;
	public event EventHandler<bool>? isStreamingChanged;
	public event EventHandler onDisposed;
	#endregion
}
