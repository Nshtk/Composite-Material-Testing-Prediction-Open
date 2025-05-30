using System;
using System.Collections.Generic;

using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace FCGR.Common.Libraries.AI.Modules;

public class CrackForecastingNet : Module<Tensor, Tensor>
{
	#region Definitions
	public class PositionalEncoderLayer : Module<Tensor, Tensor>
	{
		private readonly Tensor buffer;
		
		public PositionalEncoderLayer(long dModel, long maxLen = 5000) : base("pos-encoder")
		{
			Tensor position = arange(0, maxLen, dtype: torch.float32).unsqueeze(1);
			Tensor div_term = exp(arange(0, dModel, 2, dtype: torch.float32) *(-Math.Log(10000.0) / dModel));
			buffer = zeros(maxLen, dModel);
			buffer[TensorIndex.Colon, TensorIndex.Slice(0, null, 2)] =sin(position * div_term);
			buffer[TensorIndex.Colon, TensorIndex.Slice(1, null, 2)] =cos(position * div_term);
			buffer = buffer.unsqueeze(0);
			register_buffer("buffer", buffer);
		}
		
		public override Tensor forward(Tensor input)
		{
			return input + buffer.narrow(1, 0, input.size(1));  //or buffer[TensorIndex.Colon, TensorIndex.Slice(stop: input.size(0))];
		}
	}
	#endregion
	#region Fields
	public readonly int features_number=20;		//dont use const because of type name qualification
	private readonly int _embedding_dimensions=64;
	private readonly int _attention_heads_count =4;
	private readonly int _layers_transformation_count =3;
	private readonly double _dropout_rate =0.1d;
	public readonly int forecasting_horizon =5;
	public readonly int output_variables_count = 3;
	public readonly int timesteps_count=3;

	private readonly Linear _embedding_layer;
	private readonly PositionalEncoderLayer _positional_encoder_layer;
	private readonly TransformerEncoder _transformer_encoder;
	private readonly Linear _output_head;
	private readonly Softplus _output_softplus;
	#endregion
	public CrackForecastingNet(Device? device) : base("crack-forecasting-net")
	{
		_embedding_layer=nn.Linear(features_number, _embedding_dimensions, device: device);
		_positional_encoder_layer=new PositionalEncoderLayer(_embedding_dimensions);
		_transformer_encoder= nn.TransformerEncoder(nn.TransformerEncoderLayer(_embedding_dimensions, _attention_heads_count, dropout: _dropout_rate), _layers_transformation_count);
		_output_head=nn.Linear(_embedding_dimensions, forecasting_horizon*output_variables_count, device: device);
		_output_softplus=nn.Softplus();
		RegisterComponents();
	}
	#region Methods
	public override Tensor forward(Tensor input) 
	{
		Tensor output;
		
		input=_embedding_layer.forward(input);
		input=_positional_encoder_layer.forward(input);
		input = _transformer_encoder.forward(input, null, null);
		output=input[.., ^1, ..];
		output =_output_head.forward(output);
		output= _output_softplus.forward(output);
		output = output.view(-1, forecasting_horizon, output_variables_count);

		return output;
	}
	#endregion
}