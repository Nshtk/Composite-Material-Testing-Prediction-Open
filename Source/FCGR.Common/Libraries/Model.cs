using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FCGR.Common.Libraries;

/// <summary>
///		Base class for model classes interacting with UI.
/// </summary>
public abstract class Model : ObservableObject
{
	public Model() 
	{ }
	protected void onPropertiesChanged(params string[] properties)
	{
		foreach (string property in properties)
			OnPropertyChanged(property);
	}
}
