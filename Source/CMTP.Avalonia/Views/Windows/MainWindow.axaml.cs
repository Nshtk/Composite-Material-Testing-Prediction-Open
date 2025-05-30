using Avalonia.Controls;

using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Interactivity;

using CMTP.Avalonia.ViewModels.Windows;
using CMTP.Avalonia.Managers;
using CMTP.Avalonia.ViewModels.Controls;

namespace CMTP.Avalonia.Views.Windows;

public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		//var tmp = new ViewModelMainWindow();
		//tmp.View_Model_Video_Form_Selected = video_form.DataContext as ViewModelVideoForm;
		//DataContext = tmp;
		DataContext= new ViewModelMainWindow();
#if DEBUG
		if (!Design.IsDesignMode)
#endif
			AppManager.Windows.TryAdd(nameof(ViewModelMainWindow), this);

		button_splitView.Content = new TextBlock()   //HACK Viewmodel Command conflicts with Is_Processing_Enabled so...
		{
			Text = "→",
			FontSize = 13,
			FontWeight = FontWeight.SemiBold
		};
		TextBlock text_block = button_splitView.Content as TextBlock;
		button_splitView.Click += (object? o, RoutedEventArgs e) =>
		{
			splitView.IsPaneOpen = !splitView.IsPaneOpen;
		};
		splitView.PaneOpened += (object? o, RoutedEventArgs e) =>
		{
			text_block.Text = "→";
		};
		splitView.PaneClosed += (object? o, RoutedEventArgs e) =>
		{
			text_block.Text = "←";
		};
	}
}