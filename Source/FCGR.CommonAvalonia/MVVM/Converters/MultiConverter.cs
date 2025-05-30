using System;
using System.Text;
using System.Collections.Generic;
using System.Globalization;

using Avalonia.Data;
using Avalonia.Data.Converters;

namespace FCGR.CommonAvalonia.MVVM;
//NOTE Multibinding: TwoWay is not allowed in Avalonia :(; FallbackValue doesn't work
/// <summary>
///		Base class for multi-value converters.
/// </summary>
/// <typeparam name="TMultiConverter"></typeparam>
public abstract class MultiValueConverterBase<TMultiConverter> : IMultiValueConverter
	where TMultiConverter : class, new()
{
	private static TMultiConverter Instance = new();

	public virtual object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
	public virtual object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
/// <summary>
///		Class that returns custom string based on <see cref="VideoStream.Type"/> value.
/// </summary>
public sealed class MultiConverterVideoStreamTypeString : MultiValueConverterBase<MultiConverterVideoStreamTypeString>
{
	public override object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
	{
		if(values[0] is Enum && targetType.IsAssignableTo(typeof(string)))
			return (int)System.Convert.ChangeType(values[0], Enum.GetUnderlyingType(values[0].GetType())) == 0 ? "Камера:" : "Файл:";
		else if(values[0] is string)		//HACK
			return values[0];
		return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
	}
}
/// <summary>
///		Class that combines frame width and frame height properties to string.
/// </summary>
public sealed class MultiConverterWidthHeightResolution : MultiValueConverterBase<MultiConverterWidthHeightResolution>
{
	public override object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
	{
		StringBuilder string_builder = new StringBuilder();
		foreach(object? value in values)
		{
			if(value is not int || !targetType.IsAssignableTo(typeof(string)))
				return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
		}
		return string_builder.AppendFormat("{0}x{1}p", values[0], values[1]).ToString();
	}
}
/// <summary>
///		Class that limits the scroll viewer height to window height minus value.
/// </summary>
public sealed class MultiConverterScrollViewerHeight : MultiValueConverterBase<MultiConverterScrollViewerHeight>
{
	public override object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
	{
		if(values[0] is not double windows_height || Double.IsNaN(windows_height))
			return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
		for(int i = 1; i<values.Count; i++)
		{
			if(values[i] is not double control_height)
				return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
			windows_height -= control_height;
		}
		return windows_height-115;			//REVIEW Magic number
	}
}
/// <summary>
///		Performs logical and operation on bool properties.
/// </summary>
public sealed class MultiConverterBoolsAnd : MultiValueConverterBase<MultiConverterBoolsAnd>
{
	public override object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
	{
		if(values[0] is not bool value_initial)
			return false;

		bool result=value_initial;

		for(int i=1; i<values.Count; i++)
		{
			if(values[i] is not bool value)
				return false;
			result&=value;
		}

		return result;
	}
}