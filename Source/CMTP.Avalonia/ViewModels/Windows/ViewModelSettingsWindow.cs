using System;
using System.Collections.Generic;

using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Controls.Primitives;

using FCGR.CommonAvalonia.MVVM;
using FCGR.CommonAvalonia.MVVM.Controls;
using CMTP.Avalonia.Views.Controls.Settings;

namespace CMTP.Avalonia.ViewModels.Windows;

/// <summary>
///		Represents view model for <see cref="SettingsWindow"/>
/// </summary>
public class ViewModelSettingsWindow : ViewModel
{
	#region Fields
	private IList<TreeViewNode> _setting_nodes, _setting_nodes_backup;
	private TreeViewNode _setting_node_current;
	private UserControl _user_control_current;
	private string _search_string = "";
	#endregion
	#region Properties
	public IList<TreeViewNode> Setting_Nodes
	{
		get { return _setting_nodes; }
		private set { _setting_nodes=value; OnPropertyChanged(); }
	}
	public TreeViewNode Setting_Node_Current
	{
		get { return _setting_node_current; }
		set		///Called after <c cref="Setting_Nodes"/> is changed. 
		{
			_setting_node_current = value;
			OnPropertyChanged();
			if(_setting_node_current!=null)
				User_Control_Current = _setting_node_current.Subnodes == null ? Activator.CreateInstance(_setting_node_current.Associated_User_Control_Type) as UserControl : Activator.CreateInstance(_setting_node_current.Subnodes[0].Associated_User_Control_Type) as UserControl;      //Maybe not the right but optimised approach
			else
				User_Control_Current=new UserControl();
		}
	}
	public UserControl User_Control_Current
	{
		get { return _user_control_current; }
		set { _user_control_current = value; OnPropertyChanged(); }
	}
	/// <summary>
	///		Searches settings by their displayed name
	/// </summary>
	public string Search_String
	{
		get { return _search_string; }
		set
		{
			_search_string = value;
			if(String.IsNullOrEmpty(_search_string) || String.IsNullOrWhiteSpace(_search_string) || _search_string.Length<2)
			{
				Setting_Nodes=_setting_nodes_backup;
				Setting_Node_Current=_setting_nodes[0];
				return;
			}

			List<TreeViewNode> setting_nodes_with_found_settings = new();
			TreeViewNode? setting_node_with_found_settings=null;
			
			for(int i = 0; i<_setting_nodes.Count; i++)
			{
				UserControl user_control = Activator.CreateInstance(_setting_nodes[i].Associated_User_Control_Type) as UserControl;

				if(_setting_nodes[i].Subnodes!=null)
				{
					AvaloniaList<TreeViewNode>? setting_node_subnodes_with_found_settings = new AvaloniaList<TreeViewNode>();
					for(int j = 0; j<_setting_nodes[i].Subnodes.Count; j++)
					{
						user_control = Activator.CreateInstance(_setting_nodes[i].Subnodes[j].Associated_User_Control_Type) as UserControl;
						if(searchSettingsInUserControl(user_control))
							setting_node_subnodes_with_found_settings.Add(_setting_nodes[i].Subnodes[j]);
					}
					if(setting_node_subnodes_with_found_settings.Count>0)
					{
						setting_node_with_found_settings=_setting_nodes[i].Clone() as TreeViewNode;
						setting_node_with_found_settings.Subnodes=setting_node_subnodes_with_found_settings;
						setting_nodes_with_found_settings.Add(setting_node_with_found_settings);
					}
				}
				else if(searchSettingsInUserControl(user_control))
				{
					setting_node_with_found_settings=_setting_nodes[i].Clone() as TreeViewNode;
					setting_node_with_found_settings.Subnodes=null;
					setting_nodes_with_found_settings.Add(setting_node_with_found_settings);
				}
			}
			Setting_Nodes=setting_nodes_with_found_settings;
			if(_setting_nodes.Count>0)
				Setting_Node_Current=_setting_nodes[0];
		}
	}
	#endregion
	public ViewModelSettingsWindow()
	{
		_setting_nodes = new List<TreeViewNode>() {
			new TreeViewNode("Общие",  typeof(SettingGeneral)),
			new TreeViewNode("Сервер", typeof(SettingServer), new AvaloniaList<TreeViewNode> {
				new TreeViewNode("Основные ", typeof(SettingServer)),	//HACK Added whitespace to Title to avaid UI selection bug
			})
		}.AsReadOnly();
		_setting_nodes_backup=_setting_nodes;
		Setting_Node_Current = _setting_nodes[0];
	}
	#region Methods
	private bool searchSettingsInUserControl(UserControl user_control)
	{
		TextBlock? text_block_searchable;
		string? content_searchable=null;
		foreach(var logical_control in user_control.GetLogicalDescendants())
		{
			if(logical_control is HeaderedContentControl logical_control_as_headered_content_control)   //Handlong HeaderedContentControl.
				content_searchable=logical_control_as_headered_content_control.Header as string;
			else																						//Handling other controls.
			{
				text_block_searchable =logical_control as TextBlock;
				if(text_block_searchable==null)
				{
					ContentControl? logical_control_as_content_control = logical_control as ContentControl;
					if(logical_control_as_content_control!=null)
					{
						text_block_searchable=logical_control_as_content_control.Content as TextBlock;
						if(text_block_searchable==null)
							content_searchable=logical_control_as_content_control.Content as string;
						else
							content_searchable=text_block_searchable.Text;
					}
				}
				else
					content_searchable=text_block_searchable.Text;
			}

			if(content_searchable!=null)
				if(content_searchable.Contains(_search_string, StringComparison.OrdinalIgnoreCase))
					return true;
		}
		return false;
	}
	#endregion
}
