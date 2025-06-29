using System;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Net;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.Input;

using FCGR.Common.Utilities;
using CMTP.Avalonia.ViewModels.Windows;
using FCGR.Common.Libraries;
using CMTP.Avalonia.Models;

namespace CMTP.Avalonia.Managers;

/// <summary>
///		Handles additional project settings.
/// </summary>
internal sealed class SettingsManager : Model	//NOTE can't be made static because Avalonia can't bind to static properties
{
	#region Definitions
	/// <summary>
	///		Attribute marking individual per <see cref="VideoStream"/> project settings.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)] //TODO... Nothing is here right now, just for visual indication
	public class IndividualSettingAttribute : Attribute
	{
		public IndividualSettingAttribute()
		{ }
	}
	/// <summary>
	///		Attribute marking additional project settings.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)] //Used for marking properties which values are set from project settings
	public class SettingAttribute : Attribute
	{
		public readonly string setting_property_name_linked;

		public SettingAttribute(string setting_property_name_linked)    //NOTE usage of complex types in attribute constructor is prohibited
		{
			this.setting_property_name_linked = setting_property_name_linked;
		}
		public SettingAttribute()    //HACK just a visual indication for enums because they can't be converted
		{ }

		public object? getSettingPropertyValue(Type type)  //Not working because of enum casting errors
		{
			object? value = ((Setting)typeof(SettingsManager).GetProperty(setting_property_name_linked).GetValue(AppManager.Settings_Manager)).Value;
			
			Convert.ChangeType(value, Nullable.GetUnderlyingType(type) ?? type);
			
			return value;
		}
	}
	/// <summary>
	///		Used for initial startup configuration.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class SettingImmutable<T>
	{
		public T Value
		{
			get;
		}

		public SettingImmutable(T value)
		{
			Value = value;
		}
	}
	/// <summary>
	///		Holds information about specific project settings and provides properties for UI.
	/// </summary>
	public class Setting : Model
	{
		#region Fields
		public readonly string title;
		private object? _value;
		private object? _value_default;
		public long position_in_file;
		public readonly bool is_value_complex;
		public Dictionary<int, object?> individual_videoform_values;    //VideoStream.Id, value;
		private Predicate<object>? _validateValueCallback;
		public Action onSave;
		#endregion
		#region Properties
		public object? Value
		{
			get { return _value; }
			set
			{
				if (value != null)
				{
					Type value_default_type = _value_default.GetType();
					if (value.GetType() != value_default_type)
						value = Convert.ChangeType(value, value_default_type);
					if(_validateValueCallback!=null)
						Is_Value_Invalid=!_validateValueCallback(value);
				}
				if (_value == value)
					return;
				_value = value;
				OnPropertyChanged();
			}
		}
		public object? Value_Default
		{
			get { return _value_default; }
		}
		public bool Is_Value_Invalid	//TODO Highlight settings wtih wrong values in red, set them to default in saveSettingsAsync() if user did't correct them 
		{
			get;
			private set;
		} = false;
		public string? Description
		{
			get;
		}
		#endregion
		public Setting(string title, object? value_default, Predicate<object>? validateValueCallback=null, bool can_be_unique_for_every_video_stream = false, Action onSave=null, string? description = null)
		{
			this.title = title;
			_value = value_default;
			_value_default = value_default;
			_validateValueCallback=validateValueCallback;
			Description = description;
			if (can_be_unique_for_every_video_stream)
				individual_videoform_values = new Dictionary<int, object?>();
		}
		#region Methods
		public override int GetHashCode()
		{
			HashCode hashcode = new HashCode();
			hashcode.Add(title);
			hashcode.Add(_value_default);
			return hashcode.ToHashCode();
		}
		#endregion
	}
	/// <summary>
	///		UI theme.
	/// </summary>
	public enum THEME
	{
		[Description("Системная")]      //Attribute for tricky conversion in converter
		SYSTEM = 0,
		[Description("Светлая")]
		LIGHT = 1,
		[Description("Темная")]
		DARK = 2
	}
	public enum BITMAP_STRETCH      //Uses values of Avalonia.Media.Stretch
	{
		[Description("Исходный масштаб")]
		NONE = 0,
		[Description("Сохранять соотношение сторон")]
		UNIFORM = 2,
		[Description("Заполнение")]
		FILL = 1
	}
	#endregion
	#region Fields
	private readonly Dictionary<string, Setting[]> setting_group_name_setting = new Dictionary<string, Setting[]>()		//TODO Settings are not written to project file if their value is null (setting_number_in_group can cause problems when parsing)
	{
		{"Оформление",          new Setting[] {
									new Setting("Тема", THEME.SYSTEM),
								}},
		{"Сервер",              new Setting[] {
									new Setting("IPv6", true, (value)=>	//These 3 settings are circular dependants to each other
									{
										if((bool)AppManager.Settings_Manager.Server_Is_Local.Value)
											AppManager.Settings_Manager.Server_Address.Value = (bool)value==true? "::1" : "127.0.0.1";
										else
											AppManager.Settings_Manager.Server_Address.Value=AppManager.Settings_Manager.Server_Address.Value;	//Invalidate value
										return true;
									}, description: "Использовать IPv6 для соединения с сервером. Может улучшить скорость передачи данных.\nВАЖНО: потребуется изменить адрес сервера на его IPv6 адрес."),
									new Setting("Адрес", "::1", (value)=>
									{
										string value_casted=(string)value;
										bool result = value_casted!="";

										if(!(bool)AppManager.Settings_Manager.Server_Is_Using_IPV6.Value)
											result=Regex.IsMatch(value_casted, "([0-9]{1,3}\\.){3}([0-9]{1,3})");
										if(result)
										{
											ServerManager.Server_Endpoint =new IPEndPoint(IPAddress.Parse(value_casted), (int)AppManager.Settings_Manager.Server_Port.Value);
											if(value_casted=="127.0.0.1" || value_casted=="::1")
												if(!(bool)AppManager.Settings_Manager.Server_Is_Local.Value)
													AppManager.Settings_Manager.Server_Is_Local.Value=true;
										}

										return result;
									}),
									new Setting("Порт", 5001),
									new Setting("Локальный_сервер", true, (value)=>
									{
										if((bool)value)
										{
											Task.Run(()=>		//CAUTION HACK The only way to avoid circular dependency hell (see above comment)
											{
												Task.Delay(10);
												if((bool)AppManager.Settings_Manager.Server_Is_Using_IPV6.Value)
												{
													if(AppManager.Settings_Manager.Server_Address.Value!="::1")
														AppManager.Settings_Manager.Server_Address.Value="::1";
												}
												else
												{
													if(AppManager.Settings_Manager.Server_Address.Value!="127.0.0.1")
														AppManager.Settings_Manager.Server_Address.Value="127.0.0.1";
												}
											});
										}
										else
											AppManager.Settings_Manager.Server_Address.Value="";
										return true;
									}),
								}}
	};
	private RelayCommand? _command_restore_default_settings, _command_reset_individual_settings;
	private AsyncRelayCommand? _command_restart_server;
	#endregion
	#region Properties
	public THEME Theme
	{
		get { return (THEME)setting_group_name_setting["Оформление"][0].Value; }        //There is no generic OrderedDictionary<> in .NET :(
		set
		{
			setting_group_name_setting["Оформление"][0].Value = value;
			themeChanged?.Invoke(this, value);
		}
	}
	public Setting Server_Is_Using_IPV6
	{
		get { return setting_group_name_setting["Сервер"][0]; }
	}
	public Setting Server_Address
	{
		get { return setting_group_name_setting["Сервер"][1]; }
	}
	public Setting Server_Port
	{
		get { return setting_group_name_setting["Сервер"][2]; }
	}
	public Setting Server_Is_Local
	{
		get { return setting_group_name_setting["Сервер"][3]; }
	}
	#region Properties.Avalonia
	public IRelayCommand Command_Restore_Default_Settings
	{
		get
		{
			if (_command_restore_default_settings == null)
				_command_restore_default_settings = new RelayCommand(restoreDefaultSettings, () => { return true; });
			return _command_restore_default_settings;
		}
	}
	public IAsyncRelayCommand Command_Restart_Server
	{
		get
		{
			if (_command_restart_server == null)
				_command_restart_server = new AsyncRelayCommand(() => Task.Run(ServerManager.restartServerAsync), () => true);
			return _command_restart_server;
		}
	}
	#endregion
	#endregion
	static SettingsManager()
	{ }
	#region Methods
	/// <summary>
	///		Sets <see cref="Setting"/> value from string
	/// </summary>
	/// <param name="setting_group_name"></param>
	/// <param name="setting_number_in_group"></param>
	/// <param name="value_to_set"></param>
	public void setSettingValueFromString(string setting_group_name, int setting_number_in_group, string value_to_set)
	{
		if (setting_group_name_setting[setting_group_name][setting_number_in_group].Value is int)  //NOTE No way to convert this to function because dictionary items are passed with temporary address //Switch can be used but I don't know if it's better in this case
			setting_group_name_setting[setting_group_name][setting_number_in_group].Value = Convert.ToInt32(value_to_set);
		else if (setting_group_name_setting[setting_group_name][setting_number_in_group].Value is double)
			setting_group_name_setting[setting_group_name][setting_number_in_group].Value = Convert.ToDouble(value_to_set);
		else if (setting_group_name_setting[setting_group_name][setting_number_in_group].Value is bool)
			setting_group_name_setting[setting_group_name][setting_number_in_group].Value = Convert.ToBoolean(value_to_set);
		else if (setting_group_name_setting[setting_group_name][setting_number_in_group].Value is string)
			setting_group_name_setting[setting_group_name][setting_number_in_group].Value = value_to_set;
		else if (setting_group_name_setting[setting_group_name][setting_number_in_group].Value is THEME)
		{
			THEME tmp = Enum.Parse<THEME>(value_to_set);
			setting_group_name_setting[setting_group_name][setting_number_in_group].Value = tmp;
			Theme = tmp;
		}
		else if (setting_group_name_setting[setting_group_name][setting_number_in_group].Value is BITMAP_STRETCH)
			setting_group_name_setting[setting_group_name][setting_number_in_group].Value = Enum.Parse<BITMAP_STRETCH>(value_to_set);
	}
	/// <summary>
	///		Reads <see cref="Setting"/> values from strings and sets them.
	/// </summary>
	/// <param name="lines"></param>
	/// <returns>Is completed</returns>
	public bool readSettings(List<string> lines)
	{
		Regex regex_settings_group = new Regex("\\[([a-zA-Zа-яА-Я_]+)\\]"), regex_setting = new Regex("([a-zA-Zа-яА-Я0-9_]+)\\s*=\\s*(.+)"), regex_setting_individual = new Regex("(-?[0-9]+):");
		Match match_settings_group = null, match_setting;
		int setting_number_in_group = 0;

		for (int i = 0; i < lines.Count; i++)
		{
			match_setting = regex_setting.Match(lines[i]);
			if (match_setting.Success)
			{
				if (match_settings_group == null)
				{
					AppManager.Logger.printMessage($"Не найдено начало настроек. Рекомендуется пересоздать проект.", MESSAGE_SEVERITY.ERROR);
					break;
				}
				if (!setting_group_name_setting.ContainsKey(match_settings_group.Groups[1].Value))
				{
					AppManager.Logger.printMessage($"Обнаружена неизвестная настройка {lines[i]} в разделе {match_settings_group.Groups[1].Value}, пропуск. Рекомендуется перезаписать файл проекта, сохранив текущие настройки.", MESSAGE_SEVERITY.WARNING);
					continue;
				}
				if (match_setting.Groups[1].Value != setting_group_name_setting[match_settings_group.Groups[1].Value][setting_number_in_group].title)
				{
					AppManager.Logger.printMessage($"Обнаружена неизвестная настройка {lines[i]} в разделе {match_settings_group.Groups[1].Value}, пропуск. Рекомендуется перезаписать файл проекта, сохранив текущие настройки.", MESSAGE_SEVERITY.WARNING);
					continue;
				}

				try
				{

					if (setting_group_name_setting[match_settings_group.Groups[1].Value][setting_number_in_group].individual_videoform_values != null)
						setting_group_name_setting[match_settings_group.Groups[1].Value][setting_number_in_group].individual_videoform_values.Clear();  //Clear individual settings after new project was opened to avoid collisions
					setSettingValueFromString(match_settings_group.Groups[1].Value, setting_number_in_group, match_setting.Groups[2].Value);

				}
				catch (FormatException)
				{
					AppManager.Logger.printMessage($"Не удалось прочитать значение настройки {lines[i]} в разделе {match_settings_group.Groups[1].Value}, будет использовано значение по-умолчанию.", MESSAGE_SEVERITY.WARNING);
					continue;
				}

				setting_number_in_group++;
			}
			else
			{
				match_settings_group = regex_settings_group.Match(lines[i]);
				if (match_settings_group.Success)
				{
					setting_number_in_group = 0;
				}
				else
				{
					AppManager.Logger.printMessage($"Обнаружен неизвестный формат настройки на строке {i + 1} от начала настроек. Рекомендуется создать проект заново.", MESSAGE_SEVERITY.ERROR);
					return false;
				}
			}
		}

		return true;
	}
	/// <summary>
	///		Saves additional project settings to project file.
	/// </summary>
	/// <returns></returns>
	public async Task<bool> saveSettingsAsync()     //Deadlock occurs when calling this method indirectly from STAThread if no ConfigureAwait(false) is used
	{
		long position_in_file = ProjectManager.Project_Options_Last_Position_In_File;

		ProjectManager.clearAdditionalProjectData();

		foreach (var kv in setting_group_name_setting)
		{
			position_in_file = await ProjectManager.writeAdditinalProjectDataAsync(position_in_file, $"[{kv.Key}]").ConfigureAwait(false);
			for (int i = 0; i < kv.Value.Length; i++)
			{
				if(kv.Value[i].Is_Value_Invalid)
				{
					kv.Value[i].Value=kv.Value[i].Value_Default;
					AppManager.Logger.printMessage($"Значение настройки {kv.Value[i].title} не сохранено.", MESSAGE_SEVERITY.WARNING);
				}
				kv.Value[i].position_in_file = position_in_file;
				position_in_file = await ProjectManager.writeAdditinalProjectDataAsync(kv.Value[i].position_in_file, $"{kv.Value[i].title} = {kv.Value[i].Value}").ConfigureAwait(false);
				if (kv.Value[i].individual_videoform_values != null)
					foreach (var kkvv in kv.Value[i].individual_videoform_values)
						position_in_file = await ProjectManager.writeAdditinalProjectDataAsync(position_in_file, $"{kkvv.Key}:{kv.Value[i].title} = {kkvv.Value}").ConfigureAwait(false);
				if(kv.Value[i].onSave!=null)
					kv.Value[i].onSave();
			}
		}
		AppManager.Logger.printMessage("Настройки и данные сохранены.");

		return true;
	}
	/// <summary>
	///		Restores default project settings.
	/// </summary>
	public void restoreDefaultSettings()
	{
		foreach (var kv in setting_group_name_setting)
			foreach (Setting setting in kv.Value)
				setting.Value = setting.Value_Default;
	}
	#endregion
	#region Events
	public static event EventHandler<THEME> themeChanged;
	#endregion
}
