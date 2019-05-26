	// Character creation engine by Victor Isbell
	// Compiler version 4.0.30319.17929 for Microsoft (R) .NET Framework 4.5

	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Drawing;
	using System.Runtime.InteropServices;
	using System.Dynamic;
	using Newtonsoft.Json;

	namespace Rextester {

	//
	// Toolbox
	//
	public static class Tools {
		public static double GetPolarDistance(double r1, double r2, double t1, double t2){
			return Math.Sqrt(r1*r1 + r2*r2 - 2*r1*r2*Math.Cos(t1-t1));
		}
		public static void CartesianToPolar(double x, double y, ref double radius, ref double theta){
			radius = Math.Sqrt(x*x + y*y);
			theta = Math.Atan(y/x);
			if (x<0) {
				theta += Math.PI;
			}else if (y < 0) {
				theta += 2*Math.PI;
			}
		}
		public static string GetRandomWeighted(Dictionary<string, double> weights) {
			var totalWeights = new Dictionary<string, double>();

			double totalWeight = 0.0;
			foreach (KeyValuePair<string, double> weight in weights) {
				if (weight.Value <= 0) continue;
				totalWeight += weight.Value;
				totalWeights.Add(weight.Key, totalWeight);
			}

			double randomTotalWeight = globals.random.NextDouble() * totalWeight;
			foreach (KeyValuePair<string, double> weight in totalWeights) {
				if (weight.Value >= randomTotalWeight) {
					return weight.Key;
				}
			}
			return "none";
		}
		public static string GetRandomWeighted(Dictionary<string, int> weights) {
			var totalWeights = new Dictionary<string, int>();

			int totalWeight = 0;
			foreach (var weight in weights) {
				if (weight.Value <= 0) continue;
				totalWeight += weight.Value;
				totalWeights.Add(weight.Key, totalWeight);
			}

			int randomTotalWeight = (int)(globals.random.NextDouble() * totalWeight);
			foreach (var weight in totalWeights) {
				if (weight.Value >= randomTotalWeight) {
					return weight.Key;
				}
			}
			return "none";
		}
		public static double Constrain(double min, double x, double max) {
			return Math.Max(min, Math.Min(max, x));
		}
		public static int RollDice(int quantity, int sides) {
			int total = 0;
			for (int i = 0; i < quantity; i++) {
				total += globals.random.Next(1, sides + 1);
			}
			return total;
		}
		// Convert an HLS value into an RGB value.
		public static Color HlsToRgb(double h, double l, double s) {
			double p2;
			if (l <= 0.5) p2 = l * (1 + s);
			else p2 = l + s - l * s;

			double p1 = 2 * l - p2;
			double double_r, double_g, double_b;
			if (s == 0) {
				double_r = l;
				double_g = l;
				double_b = l;
			} else {
				double_r = QqhToRgb(p1, p2, h + 120);
				double_g = QqhToRgb(p1, p2, h);
				double_b = QqhToRgb(p1, p2, h - 120);
			}
			return Color.FromArgb(
				(int)(double_r * 255.0),
				(int)(double_g * 255.0),
				(int)(double_b * 255.0));
		}

		private static double QqhToRgb(double q1, double q2, double hue) {
			if (hue > 360) hue -= 360;
			else if (hue < 0) hue += 360;

			if (hue < 60) return q1 + (q2 - q1) * hue / 60;
			if (hue < 180) return q2;
			if (hue < 240) return q1 + (q2 - q1) * (240 - hue) / 60;
			return q1;
		}
	}

	public abstract class Probability {
		public double min, max, med, range;
		protected double shiftExponent;
		protected bool shifted;
		public abstract double NextDouble();
		public Probability(double min = 0, double max = 1) {
			this.min = min;
			this.max = max;
			this.range = max-min;
			this.med = min + range / 2.0;
			this.shiftExponent = 1;
		}
		public Probability(double min, double med, double max, double? stDev = null, double? filterMin = null, double? filterMax = null) {
			this.min = min;
			this.max = max;
			this.range = max-min;
			this.med = Tools.Constrain(min + 0.01*range, med, max);
			this.shiftExponent = Math.Log((this.med-min)/range, 0.5);
		}
		protected double ShiftResult(double input) {
			var result = min + range * Math.Pow(input, shiftExponent);
			return result;
		}
		public double Percentile(double input) {
			return (input - min) / range;
		}
		public double PercentFromMedian(double input) {
			if (input < med) {
				return Math.Abs(med - input) / (med - min);
			} else {
				return Math.Abs(input - med) / (max - med);
			}
		}
	}
	public class GaussP : Probability {
		public double filterMin, filterMax, stDev, normDev;
		private double mid, truncateExponent;
		private bool extremeMin, extremeMax;
		public GaussP(double min = 0, double max = 1) : base(min, max) {
			this.filterMin = min;
			this.filterMax = max;
			this.stDev = range/6.0;
			this.normDev = 1.0/6.0;
		}
		public GaussP(double min, double med, double max, double? stDev = null, double? filterMin = null, double? filterMax = null) : base(min, med, max) {
			this.filterMin = filterMin ?? min;
			this.filterMax = filterMax ?? max;
			this.mid = min + range/2.0;
			if (filterMin > mid + 2 * stDev) extremeMin = true;
			if (filterMax < mid - 2 * stDev) extremeMax = true;
			if (stDev == null){
				this.stDev = range/6.0;
				normDev = 1.0/6.0;
			}else{
				this.stDev = (double)stDev;
				normDev = this.stDev/range;
			}
			truncateExponent = Math.Exp(-0.5 * Math.Pow(0.5/normDev, 2));
		}

		public override double NextDouble() {
			double result;
			if (extremeMin) {
				// extreme filter with >97% reject rate
				if (filterMax != max) max = Math.Min(max, (double)filterMax);
				min = Math.Max(min, (double)filterMin);
				result = min + range * globals.random.NextDouble();
				//Console.WriteLine("Filter override {0}, {1:n2}, {2:n2}, {3:n2}, {4:n2}", result, min, med, max, stDev);
				return result;
			}
			if (extremeMax) {
				// extreme filter with >97% reject rate
				max = Math.Min(max, (double)filterMax);
				if (filterMin != min) min = Math.Max(min, (double)filterMin);
				result = min + range * globals.random.NextDouble();
				//Console.WriteLine("Filter override {0}, {1:n2}, {2:n2}, {3:n2}, {4:n2}", result, min, med, max, stDev);
				return result;
			}
			do {
				result = ShiftResult(NextGaussian());
			} while (result < filterMin || result > filterMax);
			//Console.WriteLine("GetFromGaussP {0}, {1:n2}, {2:n2}, {3:n2}, {4:n2}", result, min, med, max, stDev);
			Console.WriteLine("{0:n3}", result);
			return result;
		}
		
		protected double NextGaussian() {
			double u1 = globals.random.NextDouble() * (1 - truncateExponent) + truncateExponent;
			double u2 = globals.random.NextDouble();
			double result = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
			result = 0.5 + normDev * result;
			Console.Write("{0:n3},", result);
			if (double.IsNaN(result)) {
				Console.WriteLine("{0}, {1:n2}, {2:n2}, {3:n2}", result, u1, u2, truncateExponent);
			}
			return result;
		}
	}
	public class UniformP : Probability {
		public UniformP(double min = 0, double max = 1) : base(min, max) { }
		public UniformP(double min, double med, double max) : base(min, med, max) { }

		public override double NextDouble() {
			double result = globals.random.NextDouble();
			Console.Write("{0:n3},", result);
			result = ShiftResult(result);
			Console.WriteLine("{0:n3}", result);
			return result;
		}
	}
	public class DiscreteP : Probability {
		Dictionary<double, int> odds;
		public DiscreteP(Dictionary<double, int> odds, double med = 0.5) : base(0, med, 1) { this.odds = odds; }

		public override double NextDouble() {
			double result = min + range * globals.random.NextDouble();
			if (shifted) result = ShiftResult(result);
			foreach (var pair in odds) {
				if (result <= pair.Key) return pair.Value;
			}
			return odds[odds.Count - 1];
		}
	}

	//
	// Data
	//

	public static class globals {
		public static Random random = new Random();
		public static double m2ft = 0.0328;
		public static double kg2lbs = 2.2;
		public static JsonSerializerSettings jsonSettings = new JsonSerializerSettings() {
			MissingMemberHandling = MissingMemberHandling.Ignore,
			NullValueHandling = NullValueHandling.Ignore
		};
	}
	
    public class Point{
        public double x, y;
        public Point(){
            do {
                x = 1 - 2 * globals.random.NextDouble();
                y = 1 - 2 * globals.random.NextDouble();
            } while (Math.Sqrt(x*x + y*y) > 1);
        }
    }
    public class Program
    {
        public static void Main(string[] args)
        {
			var codeCulture = new System.Globalization.CultureInfo("en-US");
			System.Threading.Thread.CurrentThread.CurrentCulture = codeCulture;
			System.Threading.Thread.CurrentThread.CurrentUICulture = codeCulture;
			
			var ages			= new GaussP(18.0,  30.0, 107.0, 20.0);
			var heights			= new GaussP(54.0, 170.0, 272.0,  7.0);
			var densities		= new GaussP( 8.0,  13.0,  25.0,  4.0);
			var iqs				= new GaussP(50.0, 100.0, 150.0, 15.0);
			var skinLums		= new UniformP(0.05, 0.45, 0.85);
			var skinHues		= new GaussP(28.0, 36.0);
			var brownHairLums	= new UniformP(0.05, 0.6, 0.95);
			var greyHairSats	= new UniformP(0.0, 0.1d);
			var redHairLums		= new GaussP(0.30, 0.42, 0.50, 0.02);
			var redHairSats		= new GaussP(0.20, 0.40, 0.60, 0.05);
			var redHairHues		= new GaussP(5.00,15.0, 25.0,  3.0);
			
            for (int i = 0; i < 500; i++){
                (new GaussP(0, 0.25, 1, 0.2)).NextDouble();
            }
        }
    }
	}