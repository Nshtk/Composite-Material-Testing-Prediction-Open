using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ClosedXML.Excel;

using FCGR.Common.Utilities;

namespace FCGR.Common.Libraries.DataFiles;

public sealed class XLSXFile : DataFile
{
	#region Fields
	public readonly XLWorkbook work_book;
	private Dictionary<string, (char letter, long position_last)> _dict_column_name_properties;
	private LinkedList<char> _column_letters_available;
	#endregion
	public XLSXFile(string file_path) : base(file_path, ".xlsx")
	{
		work_book = new();
		_column_letters_available = new();
		for (char c = 'A'; c < 91; c++)
			_column_letters_available.AddLast(c);
		_dict_column_name_properties = new();
	}	
	#region Methods
	public bool tryCreateColumn(string column_name, char? column_letter=null)
	{
		if(String.IsNullOrEmpty(column_name))
		{
			Tracer.traceMessage($"Can't add column to {File.FullName}: {column_name} is invalid", MESSAGE_SEVERITY.ERROR, Tracer.TRACE_FLAG.EXCEPTION | Tracer.TRACE_FLAG.PRINT);
			return false;
		}
		if (_dict_column_name_properties.ContainsKey(column_name))
		{
			Tracer.traceMessage($"Can't add column to {File.FullName}: column {column_name} already exists", MESSAGE_SEVERITY.ERROR, Tracer.TRACE_FLAG.EXCEPTION | Tracer.TRACE_FLAG.PRINT);
			return false;
		}
		if (column_letter == null)
		{
			column_letter = _column_letters_available.First.Value;
			_column_letters_available.RemoveFirst();
		}
		else if (Char.IsSymbol(column_letter.Value))
		{
			var element_previous = _column_letters_available.First;
			for (int i=0; i<_column_letters_available.Count; i++)
			{
				var element_next = element_previous.Next;
				if (element_next.Value == column_letter.Value)
				{
					_column_letters_available.Remove(element_next);
					break;
				}
				element_previous = element_next;
			}
		}
		else
		{
			Tracer.traceMessage($"Can't add column to {File.FullName}: {column_letter} is not a symbol", MESSAGE_SEVERITY.ERROR, Tracer.TRACE_FLAG.EXCEPTION | Tracer.TRACE_FLAG.PRINT);
			return false;
		}
		_dict_column_name_properties.Add(column_name, new(column_letter.Value, 0));
		
		return true;
	}
	public bool writeToColumn(string column_name, string value)
	{
		(char letter, long position_last) letter_posisiton_last = new ();
		if (_dict_column_name_properties.TryGetValue(column_name, out letter_posisiton_last))
		{
			work_book.Cell(column_name+(letter_posisiton_last.position_last+2).ToString()).Value=value;
			return true;
		}
		return false;
	}
	public void tryWriteToColumn(string column_name)
	{
		
	}
	public Task save()
	{
		work_book.SaveAs(File.FullName);
		return Task.CompletedTask;
	}
	protected override void dispose(bool is_explicit)
	{
		base.dispose(is_explicit);
		if (is_explicit)
		{

		}
		work_book.Dispose();
	}
	#endregion
}
