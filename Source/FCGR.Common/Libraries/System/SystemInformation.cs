using System;
using System.Management;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.IO;
using System.Runtime.CompilerServices;
using System.Linq;

using Microsoft.Win32;

namespace FCGR.Common.Libraries.SSystem;

/// <summary>
///		Receives this PC specs, OS and resources information.
/// </summary>
public static class SystemInformation
{
    #region Definitions
    /// <summary>
    ///		Holds iformation about physical, virtual and pagefile memory.
    /// </summary>
    public ref struct MemoryInformation
    {
#pragma warning disable IDE1006 // Naming Styles
		public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
		public ulong ullAvailPageFile;
		public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
#pragma warning restore IDE1006

        public MemoryInformation()
        {
            dwLength = checked((uint)Marshal.SizeOf(typeof(MemoryInformation)));
        }
    }
    #endregion
    #region Properties
    /// <summary>
    ///		OS culture and language.
    /// </summary>
    public static CultureInfo Culture_Information
    {
        get { return CultureInfo.InstalledUICulture; }
    }
    #endregion
    static SystemInformation()
    { }
    #region Methods
#if OS_WINDOWS
    [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryInformation lpBuffer);
#endif
    /// <summary>
    ///		Gets OS distribution name.
    /// </summary>
    /// <returns></returns>
    public static string getDistributionName()
    {
#if OS_WINDOWS
        int caption_index;
        string caption_label = "Caption=";
        string? output;

        using (Process process = new Process())
        {
            process.StartInfo.FileName = $"wmic";
            process.StartInfo.Arguments = $"os get /format:list";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
        }
        caption_index = output.IndexOf(caption_label) + caption_label.Length;

        return output.Substring(caption_index, output.IndexOf("\r\n", caption_index) - caption_index - 1);
#elif OS_LINUX
	return null;
#else
	throw new PlatformNotSupportedException();
#endif
    }
#if OS_LINUX
	private static long getBytesFromLine(string token)
	{
		const string KbToken = "kB";
		var memTotalLine = File.ReadAllLines("/proc/meminfo").FirstOrDefault(x => x.StartsWith(token))?.Substring(token.Length);
		if (memTotalLine != null && memTotalLine.EndsWith(KbToken) && long.TryParse(memTotalLine.Substring(0, memTotalLine.Length - KbToken.Length), out var memKb))
			return memKb * 1024;
		throw new Exception();
	}
	private static string getValueFromLine(String token)
	{
		return File.ReadAllLines("/proc/cpuinfo").FirstOrDefault(x => x.StartsWith(token))?.Substring(token.Length).Trim().Substring(1).Trim();
	}
#endif
    /// <summary>
    ///		Gets memory information.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Win32Exception"></exception>
    public static MemoryInformation getMemoryInformation()      //CAUTION 32-bit support is not implemented
    {
		MemoryInformation memory_information;
#if OS_WINDOWS
        memory_information = new MemoryInformation();

        if (!GlobalMemoryStatusEx(ref memory_information))
            throw new Win32Exception("Could not obtain memory information due to internal error.");
#elif OS_LINUX
		memory_information = new MemoryInformation()
		{
			ullTotalPhys	=	(ulong)getBytesFromLine("MemTotal:"),
			ullAvailPhys	=	(ulong)getBytesFromLine("MemFree:"),
			ullTotalVirtual =	(ulong)getBytesFromLine("SwapTotal:"),
			ullAvailVirtual =	(ulong)getBytesFromLine("SwapFree:"),
		};
#else
		throw new PlatformNotSupportedException();
#endif
		return new();
    }
    /// <summary>
    /// Gets cpu vendor name.
    /// </summary>
    /// <returns></returns>
    public static string getCPUVendorName()
    {
#if OS_WINDOWS
        try
        {
            foreach (ManagementObject moProcessor in new ManagementObjectSearcher("SELECT Manufacturer FROM Win32_Processor").Get())
                if (moProcessor["Manufacturer"] != null)
                    return moProcessor["Manufacturer"].ToString().Trim();
        }
        catch
        {
            throw;
        }
        return null;
#elif OS_LINUX
		return null;
#endif
    }
    /// <summary>
    ///		Gets CPU model name.
    /// </summary>
    /// <returns></returns>
    public static string getCPUName()
    {
#if OS_WINDOWS
        try
        {
            foreach (ManagementObject moProcessor in new ManagementObjectSearcher("SELECT NAME FROM Win32_Processor").Get())
                if (moProcessor["Name"] != null)
                    return moProcessor["Name"].ToString().Trim();
        }
        catch
        {
            throw;
        }
        return null;
#elif OS_LINUX
		return null;
#endif
    }
    #endregion
}
