using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using FCGR.Common.Libraries;

namespace CMTP.Avalonia.Models;

public class TestingMachine : Model
{
#region Definitions
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public class SensorDataAttribute : Attribute
	{
		public int Refresh_Rate_Milliseconds
		{
			get;
		}
		public double Value_Min
		{
			get;
		}
		public double Value_Max
		{
			get;
		}

		public SensorDataAttribute(int refresh_rate_milliseconds, double value_min, double value_max)
		{
			Refresh_Rate_Milliseconds = refresh_rate_milliseconds;
			Value_Min = value_min;
			Value_Max = value_max;
		}
	}
	#endregion
	#region Fields
	public readonly int id;
	private int _frequency=1;
	private int _strain=600;
	private int _duration=100000;
	private float _area;
	private float _sm;
	private float _slbu;
	private int _cycle_number_current=0;
	private Task task_update_sensors;
	#endregion
	#region Properties
	public Material Testing_Material
	{
		get;
		private set;
	}
	public int Sensors_Update_Interval
	{
		get;
		private set;
	} = 1000;
    public int Frequency
	{
		get { return _frequency; }
		set 
		{ 
			_frequency = value;
			if(value>0)
				Sensors_Update_Interval = 1000 / _frequency;
			OnPropertyChanged();
		}
	}
	public int Strain
	{
		get { return _strain; }
		set { _strain = value; OnPropertyChanged(); }
	}
	public int Duration
	{
		get { return _duration; }
		set { _duration = value; OnPropertyChanged(); }
	}
	[SensorData(200, 0, 10)]
	public float Area
	{
		get { return _area; }
		set { _area = value; OnPropertyChanged(); }
	}
	[SensorData(200, 0, 3)]
	public float Sm
	{
		get { return _sm; }
		set { _sm = value; OnPropertyChanged(); }
	}
	[SensorData(200, 0, 3)]
	public float SLbu
	{
		get { return _slbu; }
		set { _slbu = value; OnPropertyChanged(); }
	}
	public int Cycle_Number_Current
	{
		get { return _cycle_number_current; }
		set { _cycle_number_current = value; OnPropertyChanged(); }
	}
	public int Sensors_Count
	{
		get { return sensor_properties.Length; }
	}
	private readonly PropertyInfo[] sensor_properties;
	#endregion
	public TestingMachine(int id, Material material)
	{
		this.id = id;
		Testing_Material = material;
		sensor_properties = this.GetType().GetProperties().Where(p => p.GetCustomAttribute<SensorDataAttribute>() != null).ToArray();
    }
	#region Methods
	public void start()
	{
        task_update_sensors = Task.Factory.StartNew(updateDataFromSensors, default, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }
    private void updateDataFromSensorsIndividual()
	{
		long[] properties_time_last_updated=new long[sensor_properties.Length];
		Stopwatch stopwatch = Stopwatch.StartNew();
		Random random = new();

		for (int i=0; i< properties_time_last_updated.Length; i++)
		{
			properties_time_last_updated[i] = 0;
		}

		while (true)
		{
			bool is_updated = false;
			for (int i=0; i< sensor_properties.Length; i++)
			{
				var attribute = sensor_properties[i].GetCustomAttribute<SensorDataAttribute>();
				var milliseconds_passed_current = stopwatch.ElapsedMilliseconds;
				if (attribute != null && attribute.Refresh_Rate_Milliseconds <= milliseconds_passed_current-properties_time_last_updated[i])
				{
					float value = (float)(random.NextDouble() * attribute.Value_Max);
					if (value >= attribute.Value_Min && value < attribute.Value_Max)
					{
						sensor_properties[i].SetValue(this, Convert.ChangeType(value, sensor_properties[i].PropertyType));
					}
					else
					{
						Console.WriteLine($"Sensor {sensor_properties[i].Name} value {value} is out of range ({attribute.Value_Min}-{attribute.Value_Max})");
					}
					is_updated = true;
					properties_time_last_updated[i]=milliseconds_passed_current;
				}
				if (is_updated)
					Cycle_Number_Current++;
			}
			//Task.Delay(property_update_time_min).Wait();
		}
	}
    private async void updateDataFromSensors()
    {
        Random random = new();

        while (true)
        {
            for (int i = 0; i < sensor_properties.Length; i++)
			{
                var attribute = sensor_properties[i].GetCustomAttribute<SensorDataAttribute>();
                float value = (float)(random.NextDouble() * attribute.Value_Max);
                if (attribute != null)
                {
                    if (value >= attribute.Value_Min && value < attribute.Value_Max)
                    {
                        sensor_properties[i].SetValue(this, Convert.ChangeType(value, sensor_properties[i].PropertyType));
                    }
                }
            }
            Cycle_Number_Current++;
            Task.Delay(Sensors_Update_Interval).Wait();
        }
    }
    #endregion
}
