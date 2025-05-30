using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Diagnostics;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace CMTP.Avalonia.Views.Controls.MainWindow;
/// <summary>
///		UI Control that handles logging.
/// </summary>
public partial class LogBox : UserControl		//Use one TextBlock with multiple Run instead of multiple TextBox
{
	#region Fields
	public static LogBox Instance;
	private FontFamily _font_family = new FontFamily("SegoeUI");
	private long _scroll_viewer_height=0;
	private bool _is_scrollbar_visible=false;	 //No ComputedScrollBarVisibility in Avalonia
	public string message_last;
	public int message_last_times_printed_count=1;
	#endregion
	public LogBox()
	{
		InitializeComponent();
		DataContext = this;
		Instance = this;
	}
	#region Methods
	/// <summary>
	///		Append custom message to UI control.
	/// </summary>
	/// <param name="message"></param>
	/// <param name="message_additional"></param>
	/// <param name="font_size"></param>
	/// <param name="font_style"></param>
	/// <param name="font_weight"></param>
	/// <param name="brush"></param>
	/// <param name="text_decorations"></param>
	public void appendMessage(string message, string message_additional, int font_size = 13, FontStyle font_style = FontStyle.Normal, FontWeight font_weight = FontWeight.Normal, SolidColorBrush? brush = null, TextDecorationCollection? text_decorations=null)
	{
		WrapPanel stack_panel_message_holder = new WrapPanel()
		{
			Orientation = Orientation.Horizontal,	
		};
		TextBlock text_block_message = new TextBlock()
		{
			Text = message,
			Foreground = brush,
			FontFamily = _font_family,
			FontSize = font_size,
			FontStyle = font_style,
			FontWeight = font_weight,
			TextDecorations = text_decorations,
			TextWrapping=TextWrapping.Wrap
		};
		if(message_additional != null)
		{
			TextBlock text_block_message_additional = new TextBlock()
			{
				Text = message_additional,
				Foreground = Brushes.White,
				FontFamily = _font_family,
				FontSize = 14,
				Margin = new Thickness(0, 0, 5, 0)
			};
			stack_panel_message_holder.Children.Add(text_block_message_additional);
		}
		stack_panel_message_holder.Children.Add(text_block_message);
		stackPanel_log.Children.Add(stack_panel_message_holder);

		text_block_message.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
		text_block_message.Arrange(new Rect(text_block_message.DesiredSize));
		_scroll_viewer_height += (long)text_block_message.Bounds.Bottom;
		if(!_is_scrollbar_visible && _scroll_viewer_height > Bounds.Height-10)
		{
			_is_scrollbar_visible = true;
			_scroll_viewer_height = 0;
		}

		if(_scroll_viewer_height-scrollViewer.Offset.Y<50)
		{
			Task.Run(() =>	//HACK Needed to properly scroll to the end... Oof
			{
				Thread.Sleep(25);
				Dispatcher.UIThread.Post(() => scrollViewer.ScrollToEnd());
			});
		}
	}
	/// <summary>
	///		Modiefies last message in UI control
	/// </summary>
	/// <param name="is_appending"></param>
	/// <param name="text"></param>
	/// <param name="position"></param>
	/// <param name="offset"></param>
	public void modifyLastMessage(bool is_appending, string text, int position=0, int offset=0)		//TODO... add animation (enlarge added symbols?); Separate to two TextBox in one horizontal StackPanel/Grid
	{
		WrapPanel wrap_panel_message_holder = stackPanel_log.Children[stackPanel_log.Children.Count - 1] as WrapPanel;
		TextBlock text_block=(wrap_panel_message_holder.Children.Count>1 ? wrap_panel_message_holder.Children[1] : wrap_panel_message_holder.Children[0]) as TextBlock;
		StringBuilder string_builder=new StringBuilder(text_block.Text);

		if(!is_appending)
		{
			if (position == 0)
				string_builder.Remove(string_builder.Length - text.Length+offset, text.Length-offset);
			else
				string_builder.Remove(position, string_builder.Length - position);
		}
		string_builder.Append(text);
		text_block.Text = string_builder.ToString();
		//text_block.InvalidateVisual();
	}
	#endregion
}
