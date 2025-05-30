using System;

public static class Metrics
{
	public static float calculateMAPE(float actual, float predicted)
	{
		if (actual == 0)
			throw new DivideByZeroException("Actual value cannot be zero.");

		return Math.Abs((actual - predicted) / actual) * 100.0f;
	}
	public static float CalculateWeightedRMSE(float[] actual, float[] predicted, float weight)
	{
		if (actual == null || predicted == null)
			throw new ArgumentNullException("Input arrays cannot be null.");

		if (actual.Length != predicted.Length)
			throw new ArgumentException("Actual and predicted arrays must have the same length.");

		if (weight < 0)
			throw new ArgumentOutOfRangeException("Weight must be non-negative.");

		int n = actual.Length;
		float weightedSquaredErrorSum = 0f;

		for (int i = 0; i < n; i++)
		{
			float error = actual[i] - predicted[i];
			weightedSquaredErrorSum += weight * error * error;
		}

		// Since weight is constant per point, total weight = weight * n
		float meanWeightedSquaredError = weightedSquaredErrorSum / (weight * n);

		return (float)Math.Sqrt(meanWeightedSquaredError);
	}
	public static float calculateMAE(float actual, float predicted)
	{
		return Math.Abs(actual - predicted);
	}
	public static float calculateRSquared(float[] actual, float[] predicted)
	{
		if (actual.Length != predicted.Length)
			throw new ArgumentException("Arrays must be of the same length.");

		int n = actual.Length;
		float sumActual = 0f;
		foreach (float f in actual) sumActual += f;
		float meanActual = sumActual / n;

		float ssRes = 0f;
		float ssTot = 0f;

		for (int i = 0; i < n; i++)
		{
			float diffRes = actual[i] - predicted[i];
			float diffTot = actual[i] - meanActual;
			ssRes += diffRes * diffRes;
			ssTot += diffTot * diffTot;
		}

		if (ssTot == 0)
			return ssRes == 0 ? 1.0f : float.NaN; // Avoid division by zero

		float r2 = 1f - (ssRes / ssTot);
		return r2;
	}
}

