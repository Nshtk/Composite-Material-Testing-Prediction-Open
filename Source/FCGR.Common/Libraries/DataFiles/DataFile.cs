using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FCGR.Common.Libraries.DataFiles;

public abstract class DataFile : IDisposable
{
	#region Fields
	#endregion
	#region Properties
	public FileInfo File
	{
		get;
		protected set;
	}
	#endregion
	protected DataFile(string file_path, string extension)
	{
		File = new FileInfo(file_path);
		initialise(extension);
	}
	protected DataFile(FileInfo file, string extension)
	{
		File = file;
		initialise(extension);
	}
	~DataFile()
	{
		dispose(false);
	}
	#region Methods
	private void initialise(string extension)
	{
		if (!File.Exists)
		{
			if (!File.Directory.Exists)
				Directory.CreateDirectory(File.Directory.FullName);
			File.Create().Dispose();
		}
		if (File.Extension != extension)
			throw new Exception("Wrong extension!");
	}
	protected virtual void dispose(bool is_explicit)
	{
		onDispose?.Invoke(this, EventArgs.Empty);
		if(is_explicit)
		{

		}
	}
	public void Dispose()
	{
		dispose(true);
		GC.SuppressFinalize(this);
	}
	#endregion
	#region Events
	public event EventHandler onDispose;
	#endregion
}
