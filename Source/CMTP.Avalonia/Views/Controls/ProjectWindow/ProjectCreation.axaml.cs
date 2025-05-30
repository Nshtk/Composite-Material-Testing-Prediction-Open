using Avalonia.Controls;

using CMTP.Avalonia.Managers;
using CMTP.Avalonia.ViewModels.Windows;

namespace CMTP.Avalonia.Views.Controls.Project;

public partial class ProjectCreation : UserControl
{
	public ProjectCreation()
	{
		InitializeComponent();
#if DEBUG
		if(!Design.IsDesignMode)
			DataContext = AppManager.Windows[nameof(ViewModelProjectWindow)].DataContext;
#elif RELEASE
		DataContext = WindowManager.Windows[nameof(ViewModelProjectWindow)].DataContext;
#endif
	}
}
