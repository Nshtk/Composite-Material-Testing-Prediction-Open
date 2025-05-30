using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Collections;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Controls;
using Avalonia.Platform;
using Image = Avalonia.Controls.Image;
using PixelSize = Avalonia.PixelSize;

using ScottPlot;

using CMTP.Avalonia.ViewModels.Windows;
using CMTP.Avalonia.ViewModels.Controls;
using FCGR.CommonAvalonia.Utilities;
using CMTP.Avalonia.Managers;

namespace CMTP.Avalonia.Views.Controls.MainWindow;

/// <summary>
///		Class that represents view.
/// </summary>
public partial class VideoForm : UserControl
{
	#region Fields
	private Size _border_size;
	private bool _is_being_dragged = false;
	private bool _is_control_buttons_shown = false;
	private bool _is_control_buttons_hiding_requested = false;   //HACK Needed to make delay long enough for panel_controls to not hide after pointer exits border and enters panel_controls (wait until panel_contrls.IsPointerOver==true) 
	#endregion
	#region Properties
	public Border Border
	{
		get { return border; }
	}
	private Task? Is_Border_Size_Changing
	{
		get;
		set;
	}
	#endregion
#if DEBUG
	public VideoForm()      //For designer
	{
		InitializeComponent();
		DataContext = new ViewModelVideoForm();
	}
#endif
	public VideoForm(int cameras_number_current, Bitmap bitmap_error)        //NOTE ref parameters cannot be used inside lambdas
	{
		InitializeComponent();
		Resources.Add("bitmap_error", bitmap_error);
		ViewModelVideoForm video_form_view_model = new ViewModelVideoForm(cameras_number_current, bitmap_error);
		DataContext=video_form_view_model;
		if(DataContext is ViewModelVideoForm view_model_video_form)
		{
			view_model_video_form.updateImage = async () => await Dispatcher.UIThread.InvokeAsync(image.InvalidateVisual, DispatcherPriority.Render);
			view_model_video_form.changeCursor = (Cursor cursor) => Dispatcher.UIThread.Post(() => Cursor = cursor);
			view_model_video_form.getTopLevelWindow=() => TopLevel.GetTopLevel(this);
		}

		_border_size = new Size(video_form_view_model.bitmap_default_resize_resolution.Width, video_form_view_model.bitmap_default_resize_resolution.Height);  //Need to launch an async task to get real border size, at this moment in code it's 0.
		Cursor=new Cursor(StandardCursorType.Hand);
		border.SizeChanged += border_onSizeChanged;
	}
	#region Methods
	public async void border_onSizeChanged(object? sender, SizeChangedEventArgs e)
	{
		ViewModelVideoForm view_model_video_form = DataContext as ViewModelVideoForm;
		if(Is_Border_Size_Changing == null && !view_model_video_form.Is_Bitmap_Null && !view_model_video_form.Video_Stream.Is_Capturing_Paused)
		{
			Is_Border_Size_Changing = Task.Run(async () =>
			{
				await Task.Delay(2500).ConfigureAwait(false);
				double change_in_percents = (border.Bounds.Width / _border_size.Width + border.Bounds.Height / _border_size.Height) / 2;
				view_model_video_form.changeBitmapSize(new PixelSize((int)(view_model_video_form.bitmap_default_resize_resolution.Width * change_in_percents), (int)(view_model_video_form.bitmap_default_resize_resolution.Height * change_in_percents)));
				_border_size = new Size(border.Bounds.Width, border.Bounds.Height);
				Is_Border_Size_Changing = null;
			});
			await Is_Border_Size_Changing.ConfigureAwait(false);
			Is_Border_Size_Changing = null;
		}
	}
	#region Events
	#endregion
	#endregion
}