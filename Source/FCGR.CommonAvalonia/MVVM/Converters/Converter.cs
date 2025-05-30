using System;
using System.Globalization;
using System.Linq;

using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

using FCGR.Common.Utilities;

namespace FCGR.CommonAvalonia.MVVM;
//NOTE Need to unbox boxed type to be able to cast it
/// <summary>
///		Base class for value converters.
/// </summary>
/// <typeparam name="TConverter"></typeparam>
public abstract class ValueConverterBase<TConverter> : IValueConverter
	where TConverter : class, new()
{
	public static readonly TConverter Instance=new();

	public virtual object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)	//NOTE virtual instead of abstract is intended
	{
		throw new NotSupportedException();
	}
	public virtual object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}
/// <summary>
///		Returns false if the object is null.
/// </summary>
public sealed class ConverterObjectBool : ValueConverterBase<ConverterObjectBool>
{
	public override object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value == null ? false : true;
	}
	public override object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value;
	}
}
/// <summary>
///		Returns custom string passed as parameter to tooltip if the host control is disabled.
/// </summary>
public sealed class ConverterIsEnabledTooltip : ValueConverterBase<ConverterIsEnabledTooltip>
{
	public override object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is bool val)
			return val == false ? parameter : null;
		return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
	}
}
/// <summary>
///		Used for <see cref="VideoForm"/> select/deselect.
/// </summary>
public sealed class ConverterBoolColor : ValueConverterBase<ConverterBoolColor>
{
	public override object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is bool val)
			return val==true ? Brushes.CornflowerBlue : Brushes.Gray;
		return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
	}
}
/// <summary>
///		Used for <see cref="Setting"/> value validation.
/// </summary>
/*public sealed class ConverterBoolColorSetting : ValueConverterBase<ConverterBoolColorSetting>
{
	public override object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if(value is bool val)
			return val==false ? Brushes.Red : Brushes.;
		return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
	}
}*/
/// <summary>
///		Converts raw enum to Enumerable<string>.
/// </summary>
public sealed class ConverterEnumEnumerable : ValueConverterBase<ConverterEnumEnumerable>
{
	public override object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is Enum)
			return Enum.GetValues(value.GetType()).Cast<Enum>().Select((Enum enm) => enm.getDescription()).ToList();
		return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
	}
}
/// <summary>
///		Converts raw enum to string.
/// </summary>
public sealed class ConverterEnumString : ValueConverterBase<ConverterEnumString>
{
	public override object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is Enum)
			return Enum.GetValues(value.GetType()).Cast<Enum>().Select((Enum enm) => enm.getDescription()).ToList()[0];
		return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
	}
}
/// <summary>
///		Converts enum to int.
/// </summary>
public class ConverterEnumInt : ValueConverterBase<ConverterEnumInt>
{
	public override object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is Enum val)
			return System.Convert.ToInt32(val);

		return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
	}
	public override object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (targetType.IsEnum)
			return Enum.ToObject(targetType, value);
		return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
	}
}
/// <summary>
///		Converts <see cref="VideoStream.TYPE"/> to bool.
/// </summary>
public sealed class ConverterVideoStreamTypeBool : ValueConverterBase<ConverterVideoStreamTypeBool>
{
	public override object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is Enum && targetType.IsAssignableTo(typeof(bool)))
			return (int)System.Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType())) == 0;	//Is camera
		return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
	}
}

/*public sealed class ConverterEnumBool : ValueConverterBase<ConverterEnumBool>	//UNUSED
{
	public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return value?.Equals(parameter);
	}

	public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return value?.Equals(true) == true ? parameter : Binding.DoNothing;
	}
}*/
