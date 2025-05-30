using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FCGR.Common.Libraries;
using FCGR.Common.Libraries.DataFiles;

namespace CMTP.Avalonia.Models;

public class SensorStream : IDisposable
{
	#region Fields
	private bool _is_running;

	public TestingMachine Testing_Machine
	{
		get;
	}
	#endregion
	public SensorStream(TestingMachine testing_machine)
	{
		Testing_Machine = testing_machine;
	}
    #region Methods 
    public void start()
    {
        _is_running = true;
    }
    public void stop()
	{
		_is_running = false;
	}
	public async IAsyncEnumerable<(float[] x, float[] y)> streamDataAsync(string path_files, CancellationToken ct)
	{
		(List<float[]> x, List<float[]> y) x_y = new();
		try
		{
			x_y.x = await new CSVFile($"{path_files}X.csv", ';', new()).read();
			x_y.y = await new CSVFile($"{path_files}Y.csv", ';', new()).read();
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error reading CSV: {ex.Message}");
		}
																									   //int columns_count = column_names_x.Length;
		for (int i=0; i<x_y.x.Count && _is_running && !ct.IsCancellationRequested; i++)
		{
			yield return (x_y.x[i], x_y.y[i]);
			await Task.Delay(Testing_Machine.Sensors_Update_Interval);
		}
		yield break;
	}
	public async IAsyncEnumerable<(float[], float[])> streamDataFromMachineAsync(CancellationToken ct)
	{
		while (_is_running && !ct.IsCancellationRequested)
		{
			yield return (new float[20] { (float)Testing_Machine.Testing_Material.Type, (float)Testing_Machine.Testing_Material.Reinforcement_Type, (float)Testing_Machine.Testing_Material.Sigma_C_B, (int)Testing_Machine.Testing_Material.E, Testing_Machine.Testing_Material.E_C, Testing_Machine.Testing_Material.Tau_M, Testing_Machine.Testing_Material.Tau__12M, Testing_Machine.Testing_Material.G__12, Testing_Machine.Testing_Material.Eps, Testing_Machine.Testing_Material.Mu, (int)Testing_Machine.Testing_Material.H__P, Testing_Machine.Testing_Material.V, Testing_Machine.Testing_Material.T__Gdry, (float)Testing_Machine.Testing_Material.Climate_Conditions, Testing_Machine.Testing_Material.Climate_Conditions_Time_Spent, Testing_Machine.Testing_Material.S__Max, Testing_Machine.Testing_Material.Sigma__B_Min, Testing_Machine.Testing_Material.Sigma__B_Max, Testing_Machine.Testing_Material.Sigma__B_Mean, Testing_Machine.Cycle_Number_Current }, new float[] { Testing_Machine.Area, Testing_Machine.Sm, Testing_Machine.SLbu });
			await Task.Delay(Testing_Machine.Sensors_Update_Interval);
		}
		yield break;
	}
	public void Dispose()
	{
		stop();
	}
	#endregion
	#region Events
	public event EventHandler<float[]> SensorDataReceived;
	#endregion
}
