using System;

using Avalonia.Collections;
using Avalonia.Media.Imaging;

namespace FCGR.CommonAvalonia.MVVM.Controls;

public class TreeViewNode : ICloneable
{
	public string Title
	{ 
		get;
	}
	public Bitmap? Icon
	{
		get;
	}
	public Type Associated_User_Control_Type
	{
		get;
	}
	public AvaloniaList<TreeViewNode>? Subnodes
	{
		get;
		set;
	}

	public TreeViewNode(string title, Type associated_user_control_type, AvaloniaList<TreeViewNode>? subnodes=null, Bitmap? icon = null)
	{
		Title = title;      //NOTE Auto properties with only get accessor are readonly (can be set in ctor)
		Associated_User_Control_Type = associated_user_control_type;
		Icon = icon;
		Subnodes = subnodes;
	}

	public override bool Equals(object? obj)
	{
		if (obj == null) 
			return false;
		TreeViewNode tree_view_node = obj as TreeViewNode;
		if (tree_view_node.Title != Title)
			return false;
		return true;
	}
	public override int GetHashCode()
	{
		return Title.GetHashCode();
	}

	public object Clone()
	{
		AvaloniaList<TreeViewNode>? subnodes_cloned = null;
		if(Subnodes!=null)
		{
			subnodes_cloned=new AvaloniaList<TreeViewNode>();
			for(int i = 0; i<Subnodes.Count; i++)
				subnodes_cloned.Add(Subnodes[i].Clone() as TreeViewNode);
		}
		Bitmap icon_cloned = null;
		if(Icon!=null)
			icon_cloned=Icon.CreateScaledBitmap(Icon.PixelSize);	//HACK cloning

		return new TreeViewNode(Title.Clone() as string, Associated_User_Control_Type, subnodes_cloned, icon_cloned);
	}
}
