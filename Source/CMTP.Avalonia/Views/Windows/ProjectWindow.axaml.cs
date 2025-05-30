using Avalonia.Controls;
using Avalonia.Interactivity;

using CMTP.Avalonia.ViewModels.Windows;
using CMTP.Avalonia.Views.Controls.Project;

namespace CMTP.Avalonia.Views.Windows;

public partial class ProjectWindow : Window
{
	public ProjectWindow()
	{
		InitializeComponent();
		DataContext = new ViewModelProjectWindow();
		((ViewModelProjectWindow)DataContext).User_Control_Current = new ProjectManagement();
	}

	protected override void OnLoaded(RoutedEventArgs e)		//REVIEW? delete animation
	{
		base.OnLoaded(e);
#if DEBUG
		//if(!Design.IsDesignMode)        //HACK Avoiding design-time errors
#endif
		//((ViewModelProjectWindow)DataContext).User_Control_Current = new ProjectManagement();
	}
}
