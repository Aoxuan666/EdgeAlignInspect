using System;

namespace EdgeAlignInspect
{
	[Serializable]
	public sealed class CaliperParameters
	{
		public const string NegativeTransition = "negative";

		public const string PositiveTransition = "positive";

		public const string AllTransition = "all";

		public int NumMeasures { get; set; } = 30;

		public double MeasureLength { get; set; } = 30.0;

		public double MeasureWidth { get; set; } = 5.0;

		public double Sigma { get; set; } = 1.0;

		public double Threshold { get; set; } = 20.0;

		public double SearchOutward { get; set; } = 6.0;

		public double LineFitNormalRatio { get; set; } = 0.60;

		public string MeasureInterpolation { get; set; } = "bicubic";

		public string MeasureSelect { get; set; } = "first";

		public string Transition { get; set; } = NegativeTransition;

		public static bool TryNormalizeTransition(string transition, out string normalized)
		{
			string value = (transition ?? "").Trim();
			if (string.Equals(value, NegativeTransition, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(value, "white-to-black", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(value, "white_to_black", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(value, "light-to-dark", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(value, "light_to_dark", StringComparison.OrdinalIgnoreCase)
				|| value == "白找黑")
			{
				normalized = NegativeTransition;
				return true;
			}
			if (string.Equals(value, PositiveTransition, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(value, "black-to-white", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(value, "black_to_white", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(value, "dark-to-light", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(value, "dark_to_light", StringComparison.OrdinalIgnoreCase)
				|| value == "黑找白")
			{
				normalized = PositiveTransition;
				return true;
			}
			if (string.Equals(value, AllTransition, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(value, "any", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(value, "either", StringComparison.OrdinalIgnoreCase)
				|| value == "任意")
			{
				normalized = AllTransition;
				return true;
			}
			normalized = null;
			return false;
		}

		public static string NormalizeTransition(string transition, string fallback)
		{
			string normalized;
			if (TryNormalizeTransition(transition, out normalized))
			{
				return normalized;
			}
			if (TryNormalizeTransition(fallback, out normalized))
			{
				return normalized;
			}
			return NegativeTransition;
		}

		public CaliperParameters DeepClone()
		{
			return new CaliperParameters
			{
				NumMeasures = NumMeasures,
				MeasureLength = MeasureLength,
				MeasureWidth = MeasureWidth,
				Sigma = Sigma,
				Threshold = Threshold,
				SearchOutward = SearchOutward,
				LineFitNormalRatio = LineFitNormalRatio,
				MeasureInterpolation = MeasureInterpolation,
				MeasureSelect = MeasureSelect,
				Transition = Transition
			};
		}
	}
}
