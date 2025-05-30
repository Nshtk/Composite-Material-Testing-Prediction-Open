using System;

using TorchSharp;

namespace FCGR.Common.Libraries.AI.Modules;

public class IoULoss : Loss<torch.Tensor, torch.Tensor, torch.Tensor>	 //Penalises mistakes more than Dice loss, it removes worst predictions but overall prediction quality is lower
{
	protected const float _smooth = 1e-6f;

	public IoULoss() : base(torch.nn.Reduction.Mean)
	{ }

	public override torch.Tensor forward(torch.Tensor input, torch.Tensor target)
	{
		input = input.view(-1);
		target = target.view(-1);

		torch.Tensor intersection = (input * target).sum();	 //Intersection is equivalent to TP count
		torch.Tensor union = (input + target).sum() - intersection;     //Union is the mutually inclusive area of all labels and predictions 

		return 1-(intersection + _smooth) / (union + _smooth);
	}
}

public class DiceLoss : Loss<torch.Tensor, torch.Tensor, torch.Tensor>
{
	protected const float _smooth = 1e-6f;

	public DiceLoss() : base(torch.nn.Reduction.None)
	{ }

	public override torch.Tensor forward(torch.Tensor input, torch.Tensor target)
	{
		input = input.view(-1);
		target = target.view(-1);

		torch.Tensor intersection = (input * target).sum();

		return 1 - (2f * intersection + _smooth) / (input.sum() + target.sum() + _smooth);
	}
}

public class DiceBCELoss : Loss<torch.Tensor, torch.Tensor, torch.Tensor>
{
	protected const float _smooth = 1e-6f;

	public DiceBCELoss() : base(torch.nn.Reduction.None)
	{ }

	public override torch.Tensor forward(torch.Tensor input, torch.Tensor target)
	{
		input = input.view(-1);
		target = target.view(-1);

		torch.Tensor intersection = (input * target).sum();

		return (1 - (2f * intersection + _smooth) / (input.sum() + target.sum() + _smooth)) + torch.nn.functional.binary_cross_entropy(input, target, reduction: torch.nn.Reduction.Mean);
	}
}