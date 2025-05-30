using System;
using System.Collections.Generic;
using System.Management;

namespace FCGR.Common.Libraries.System;
/// <summary>
///		Holds information about USB device.
/// </summary>
public class USBDevice
{
    public readonly string id;
    public readonly string name;
    public readonly string description;
    /// <summary>
    ///		Is currently in use.
    /// </summary>
    public bool Is_Reserved
    {
        get;
        set;
    }

    public USBDevice(string id, string name, string description)
    {
        this.id = id;
        this.name = name;
        this.description = description;
        Is_Reserved = false;
    }
}
/// <summary>
///		Holds information about system and connected devices.
/// </summary>
public static class SystemDevices
{
    private static List<USBDevice> _Devices_usb = new List<USBDevice>();
	/// <summary>
	///		Get all connected USB devices.
	/// </summary>
	public static void getUSBDevices()
	{
		using ManagementObjectSearcher? management_object_searcher = new ManagementObjectSearcher(@"SELECT * FROM Win32_USBHub");   //Uses COM
		using ManagementObjectCollection management_objects = management_object_searcher.Get();

		foreach (ManagementBaseObject management_object in management_objects)
		{
			_Devices_usb.Add(new USBDevice((string)management_object.GetPropertyValue("DeviceID"), (string)management_object.GetPropertyValue("PNPDeviceID"), (string)management_object.GetPropertyValue("Description")));      //CAUTION Linux
		}
	}
	/// <summary>
	/// Gets connected USB camera device by id.
	/// </summary>
	/// <remarks>Pass -1 to return first unreserved device.</remarks>
	/// <param name="camera_id"></param>
	/// <returns>Connected USB device.</returns>
	public static USBDevice? getCameraDevice(int camera_id = -1)
    {
        int i = 0;

        if (camera_id < 0)
        {
            for (; i < _Devices_usb.Count; i++)
                if (!_Devices_usb[i].Is_Reserved)
                    if (_Devices_usb[i].description.Contains("cam", StringComparison.OrdinalIgnoreCase))
                        return _Devices_usb[i];
        }
        else
        {
            for (; i < _Devices_usb.Count; i++)
            {
                if (_Devices_usb[i].description.Contains("cam", StringComparison.OrdinalIgnoreCase))
                {
                    camera_id--;
                    if (camera_id < 0)
                        return _Devices_usb[i];
                }
            }
        }

        return null;
    }
}
