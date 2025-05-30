using System;
using System.Text;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

using TorchSharp;
using TorchSharp.Modules;

using FCGR.Common.Utilities;

namespace FCGR.Common.Libraries.AI.Models;

/// <summary>
///		Provides methods for training, testing and using the PyTorch model.
/// </summary>
/// <typeparam name="TModel">PyTorch model.</typeparam>
public abstract class AIModelBaseTorchSharp<TModel> : IDisposable
	where TModel : torch.nn.Module<torch.Tensor, torch.Tensor>
{
	#region Definitions
	/// <summary>
	///		Base class for loading data specific to each model.
	/// </summary>
	public abstract class DataLoader
	{
		#region Definitions
		public abstract class Augmentation
		{
			[Flags]
			public enum TYPE 
			{
				NONE = 0,
				CONTRAST_BRIGHTNESS = 1,
				NORMALIZE = 2,
				RESIZE = 4,
				CROP = 8,
				INVERT = 16,
				ROTATE = 32,
				TRANSLATE = 64,
			}
			public TYPE type;

			public Augmentation(TYPE type)
			{
				this.type = type;
			}
		}
		#endregion
		#region Properties
		public torch.Device Device
		{
			get;
		}
		#endregion
		public DataLoader(DeviceType device_type)
		{
			Device=new torch.Device(device_type);
		}
		#region Methods
		//public abstract torch.Tensor getElementFromFile(FileInfo file);
		/// <summary>
		///		Loads data from all subdirectories of the specified directory.
		/// </summary>
		/// <param name="path">Directory containing subdirectories with data.</param>
		/// <param name="label_file_name">Name of the label file with extension.</param>
		/// <param name="fraction_data_to_read"></param>
		/// <returns></returns>
		public abstract IEnumerable<List<Tuple<torch.Tensor, torch.Tensor>>> loadData(string path, string label_file_name, int batch_size, Func<torch.Tensor> createX, Func<torch.Tensor> createY, float fraction_data_to_read = 1f, int data_part_size = 0, int data_read_offset = 0, bool is_reading_in_random_order = false, Dictionary<string, bool> directory_names_to_ignore = null, Augmentation augmentation = null);	//NOTE ValueTuple is not eligible for IEnumerable<> iterator return
		public (List<Tuple<torch.Tensor, torch.Tensor>>, List<Tuple<torch.Tensor, torch.Tensor>>) split(List<Tuple<torch.Tensor, torch.Tensor>> data, float data_proportion_1, float data_proportion_2)
		{
			List<Tuple<torch.Tensor, torch.Tensor>> data_part_1 = new List<Tuple<torch.Tensor, torch.Tensor>>(), data_part_2 = new List<Tuple<torch.Tensor, torch.Tensor>>();

			int i;
			for(i=0; i<data.Count*data_proportion_1; i++)
				data_part_1.Add(data[i]);
			int end = Math.Min((int)(i+data.Count*data_proportion_2), data.Count);
			for(; i<end; i++)
				data_part_2.Add(data[i]);

			return (data_part_1, data_part_2);
		}
		public void shuffle(ref List<Tuple<torch.Tensor, torch.Tensor>> data)
		{
			int data_size = data.Count;

			for(int i=0, j; i<data_size-1; i++)
			{
				j = Random.Shared.Next(i, data_size);
				if(j != i)
				{
					Tuple<torch.Tensor, torch.Tensor> temp = data[i];
					data[i] = data[j];
					data[j] = temp;
				}
			}
		}
		#endregion
	}
	#endregion
	#region Properties
	public torch.Device Device
	{
		get;
		protected set;
	}
	#endregion
	#region Fields
	protected TModel _model;
	public readonly long batch_size;
	public long[] x_shape, y_shape;
	public long x_size = 1, y_size = 1;
	protected SummaryWriter? _summary_writer = null;
	#endregion
	#region Properties
	public readonly string model_name;
	#endregion
	public AIModelBaseTorchSharp(string name, long batch_size, DeviceType device_type, string? log_path = null)
	{
		model_name=name;
		this.batch_size= batch_size;
		Device =new torch.Device(device_type);
		torch.set_num_threads(Environment.ProcessorCount);

		if(log_path!=null)
			_summary_writer = torch.utils.tensorboard.SummaryWriter(log_path, createRunName: true);
	}
	~AIModelBaseTorchSharp()
	{
		dispose(false);
	}
	#region Methods
	protected void initialise(DeviceType device_type)
	{
		for (int i = 0; i < x_shape.Length; i++)
			x_size *= x_shape[i];
		for (int i = 0; i < y_shape.Length; i++)
			y_size *= y_shape[i];
		if (device_type != DeviceType.CPU)
			_model.to(Device);
	}
	public torch.Tensor createX()
	{
		return torch.tensor(new long[batch_size * x_size], x_shape, torch.ScalarType.Float32);
	}
	public torch.Tensor createY()
	{
		return torch.tensor(new long[batch_size * y_size], y_shape, torch.ScalarType.Float32);
	}
	/// <summary>
	///		Train model on dataset.
	/// </summary>
	/// <param name="data">Data for training.</param>
	/// <param name="epochs_count">Count of epochs.</param>
	/// <returns></returns>
	public void train(List<Tuple<torch.Tensor, torch.Tensor>> data, int epochs_count, Loss<torch.Tensor, torch.Tensor, torch.Tensor> loss_layer, float learning_rate)
	{
		Stopwatch stopwatch_train_epoch = new Stopwatch();
		TimeSpan time_elapsed_total = new TimeSpan();

		_model.train();
#if DEBUG
		torch.autograd.set_detect_anomaly(true);
#endif
		using(var optimizer = torch.optim.Adam(_model.parameters(), learning_rate))
		{
			torch.optim.lr_scheduler.LRScheduler scheduler = torch.optim.lr_scheduler.StepLR(optimizer, 20, 0.95);
			float loss_epoch_average_previous = 1;
			for (int i = 1; i <= epochs_count; i++)
			{
				float loss_epoch = 0, accuracy_epoch = 0;
				float loss_epoch_average;
				long ii = 0;
				stopwatch_train_epoch.Restart();
				foreach (Tuple<torch.Tensor, torch.Tensor> data_batch_x_y in data)
				{
					float loss_as_float, accuracy;

					using (DisposeScope dispose_scope = torch.NewDisposeScope())
					{
						torch.Tensor prediction, loss;

						prediction = _model.forward(data_batch_x_y.Item1);
						loss = loss_layer.forward(prediction, data_batch_x_y.Item2);
						_model.zero_grad();
						loss.backward();
						optimizer.step();
						loss_as_float = loss.item<float>();
						accuracy = (data_batch_x_y.Item2[0, 0] == prediction[0, 0]).to_type(torch.ScalarType.Float32).mean().item<Single>();
					}
					loss_epoch += loss_as_float;
					accuracy_epoch += accuracy;
					Tracer.traceMessage($"Batch: {++ii}/{data.Count}, loss: {loss_as_float}, accuracy: {accuracy}.", flags: Tracer.TRACE_FLAG.NO_CALLER_ATTRIBUTES);
				}
				scheduler.step();
				stopwatch_train_epoch.Stop();
				time_elapsed_total += stopwatch_train_epoch.Elapsed;
				loss_epoch_average = loss_epoch / ii;
				Tracer.traceMessage($"Epoch {i}/{epochs_count}, average loss: {loss_epoch_average}, average accuracy: {accuracy_epoch / ii}, difference: {loss_epoch_average_previous - loss_epoch_average} elapsed: {stopwatch_train_epoch.Elapsed.ToString(@"hh\:mm\:ss")}.", flags: Tracer.TRACE_FLAG.NO_CALLER_ATTRIBUTES);
				loss_epoch_average_previous = loss_epoch_average;
			}
		}
	}
	/// <summary>
	///		Test model on datatset.
	/// </summary>
	/// <param name="data"></param>
	/// <param name="epochs_count"></param>
	/// <returns></returns>
	public void test(List<Tuple<torch.Tensor, torch.Tensor>> data, Loss<torch.Tensor, torch.Tensor, torch.Tensor> loss_layer)
	{
		_model.eval();
		using(IDisposable no_grad = torch.no_grad())
		{
			float loss_epoch = 0, loss_average;
			float accuracy_epoch = 0, accuracy_average;
			long i = 0;
			
			foreach(Tuple<torch.Tensor, torch.Tensor> data_batch_x_y in data)
			{
				Tracer.traceMessage($"Processing batch: {++i}/{data.Count}, ", flags: Tracer.TRACE_FLAG.NO_CALLER_ATTRIBUTES, is_writing_line: false);
				float loss_as_float, accuracy;

				using (DisposeScope dispose_scope = torch.NewDisposeScope())
				{
					torch.Tensor prediction = _model.forward(data_batch_x_y.Item1);
					torch.Tensor loss = loss_layer.forward(prediction, data_batch_x_y.Item2);
					/*for (int i = 0; i < data_batch_x_y.Item1.shape[0]; i++)
					{
						Helper.showMat(Helper.getMatFromTensor<float>(data_batch_x_y.Item1[i][0][0]), "input", 1, false);
						Helper.showMat(Helper.getMatFromTensor<float>(prediction[i]), "prediction", 1, false);
						Helper.showMat(Helper.getMatFromTensor<float>(data_batch_x_y.Item2[i][0]), "label", 0, false);
						CvInvoke.DestroyAllWindows();
					}*/

					loss_as_float = loss.item<float>();
					accuracy = (data_batch_x_y.Item2[0, 0] == (prediction[0, 0] > 0.0)).mean().item<Single>();
				}
				loss_epoch += loss_as_float;
				accuracy +=accuracy;
				Tracer.traceMessage($"loss: {loss_as_float}, accuracy: {accuracy}.", flags: Tracer.TRACE_FLAG.NO_CALLER_ATTRIBUTES);
			}
			loss_average=loss_epoch/i;
			accuracy_average=accuracy_epoch/i;
			Tracer.traceMessage($"Average loss: {loss_average}, average accuracy: {accuracy_average}.", flags: Tracer.TRACE_FLAG.NO_CALLER_ATTRIBUTES);
		}
	}
	/// <summary>
	///		Evaluate and get result.
	/// </summary>
	/// <param name="input"></param>
	/// <returns></returns>
	public torch.Tensor predict(torch.Tensor input)
	{
		torch.Tensor output;

		_model.eval();
		using(IDisposable no_grad = torch.no_grad())
		{
			output=_model.forward(input);
		}

		return output[0];
	}
	
	/// <summary>
	///		Load weights and biases.
	/// </summary>
	/// <param name="file_name_full"></param>
	/// <returns></returns>
	public bool loadWeights(string file_name_full)  //NOTE _model.load_state_dict(torch.load(path)), torch.save(_model.state_dict(), path) are not working
	{
		string file_extension = file_name_full.Substring(file_name_full.Length - 4);
		try
		{
			if (file_extension == ".dat")
				_model.load(file_name_full);
			/*else if(file_extension==".pth")		//FIXME TorchSharp.PyBridge is gone?
				_model.load_py(file_name_full);*/
			else
				return false;
		}
		catch
		{
			return false;
		}

		return true;
	}
	/// <summary>
	///		Save weights and biases.
	/// </summary>
	/// <param name="path"></param>
	/// <param name="is_pytorch_format"></param>
	/// <returns></returns>
	public bool saveWeights(string path, bool is_pytorch_format = false)
	{
		StringBuilder string_builder = new StringBuilder($"{path}{_model.GetName()}-model");

		if(!Directory.Exists(path))
			Directory.CreateDirectory(path);

		try
		{
			if(is_pytorch_format)
			{
				string_builder.Append(".pth");
				//_model.save_py(string_builder.ToString());	//FIXME TorchSharp.PyBridge is gone?
			}
			else
			{
				string_builder.Append(".dat");
				_model.save(string_builder.ToString());
			}
		}
		catch(UnauthorizedAccessException e)
		{
			Tracer.traceMessage(e.Message);
			return false;
		}

		return true;
	}
	/// <summary>
	///		Print model summary.
	/// </summary>
	public void printSummary(bool is_printing_biases = false)
	{
		long parameters_trainable_count_total = 0;

		foreach((string name, Parameter parameter) name_parameter in _model.named_parameters())
		{
			if(name_parameter.parameter.requires_grad)
			{
				var parameter_trainable_count = name_parameter.parameter.numel();
				parameters_trainable_count_total+=parameter_trainable_count;
				Tracer.traceMessage($"{name_parameter.name}\t\t {name_parameter.parameter}, parameters = {parameter_trainable_count}");
			}
		}
		Tracer.traceMessage($"\nTrainable parameters total: {parameters_trainable_count_total}");
	}
	protected virtual void dispose(bool is_explicit)
	{
		if (is_explicit)
		{
		}
	}
	public void Dispose()
	{
		dispose(is_explicit: true);
		GC.SuppressFinalize(this);
	}
	#endregion
}
