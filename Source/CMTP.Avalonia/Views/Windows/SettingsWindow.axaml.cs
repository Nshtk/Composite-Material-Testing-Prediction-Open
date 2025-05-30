using System;

using Avalonia.Controls;
using Avalonia.Input;

using CMTP.Avalonia.ViewModels.Windows;

namespace CMTP.Avalonia.Views.Windows;

public partial class SettingsWindow : Window
{
	public SettingsWindow()
	{
		InitializeComponent();
		DataContext = new ViewModelSettingsWindow();
	}

	public void treeViewNodeOnPointerPressed(object sender, PointerPressedEventArgs e)
	{
		((TreeViewItem)(sender as Control).Parent).IsExpanded = true;
	}
}
