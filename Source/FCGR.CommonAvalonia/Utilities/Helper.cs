using System;

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using Emgu.CV;

namespace FCGR.CommonAvalonia.Utilities;

public static class Helper
{
	/// <summary>
	///		Finds the parent element of specified type of the child element recursively.
	/// </summary>
	/// <typeparam name="T">Specified type.</typeparam>
	/// <param name="child_element">Child element.</param>
	/// <returns>Closest parent of the specified type.</returns>
	public static T? findParentByType<T>(StyledElement child_element)
		where T : StyledElement 
	{
		if(child_element.Parent == null)
			return null;
		else if(child_element.Parent is T parent_casted)
			return parent_casted;
		else 
			return findParentByType<T>(child_element.Parent);
	}
	/// <summary>
	///		Creates an Avalonia-compatible bitmap from <see cref="Mat"/> object by sharing memory.
	/// </summary>
	/// <remarks>
	///		See Emgu.Cv.Bitmap.Mat.ToBitmap() method to further improve this method.
	/// </remarks>
	/// <param name="mat"></param>
	/// <returns>Avalonia writeable bitmap.</returns>
	/// <exception cref="NotImplementedException"></exception>
	/// <exception cref="NotSupportedException"></exception>
	public static WriteableBitmap? getBitmapFromMat(Mat mat)
	{
		if (mat.Dims > 3)
			return null;

		System.Drawing.Size size = new System.Drawing.Size(mat.Size.Width, mat.Size.Height);	//Avoiding mat.Size recalculation.
		int channels_count = mat.NumberOfChannels;

		switch (channels_count)
		{
			case 1:
				throw new NotImplementedException();	//TODO
			case 3:
				return new WriteableBitmap(PixelFormats.Rgb24, AlphaFormat.Opaque, mat.DataPointer, new PixelSize(size.Width, size.Height), new Vector(96, 96), mat.Step);
			default:
				throw new NotSupportedException("Unknown color type");
		}
	}
}
