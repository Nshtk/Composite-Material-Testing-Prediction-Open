using Avalonia.Controls;
using Avalonia.Interactivity;

using CMTP.Avalonia.Managers;
using CMTP.Avalonia.ViewModels.Windows;

namespace CMTP.Avalonia.Views.Controls.Project;

public partial class ProjectManagement : UserControl
{
	public ProjectManagement()
	{
		InitializeComponent();

	}
	protected override void OnLoaded(RoutedEventArgs e)
	{
		base.OnLoaded(e);
		button_open_project.Focus();
#if DEBUG
		if(!Design.IsDesignMode)		//HACK Avoiding design-time errors
#endif
		DataContext = AppManager.Windows[nameof(ViewModelProjectWindow)].DataContext;

	}
}
