// Character creation engine by Victor Isbell

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Dynamic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Rextester {


//
// Toolbox
//
public static class Tools {
	public static double GetCartesianDistance(double x1, double x2, double y1, double y2){
		return Math.Sqrt(Math.Pow(x1-x2, 2) + Math.Pow(y1-y2, 2));
	}
	public static double GetPolarDistance(double r1, double r2, double t1, double t2){
		return Math.Sqrt(r1*r1 + r2*r2 - 2*r1*r2*Math.Cos(t2-t1));
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
	public static void PolarToCartesian(double radius, double theta, ref double x, ref double y){
		x = radius * Math.Cos(theta);
		y = radius * Math.Sin(theta);
	}
	public static T GetRandomWeighted<T>(Dictionary<T, double> weights) {
		var totalWeights = new Dictionary<T, double>();

		double totalWeight = 0.0;
		foreach (var weight in weights) {
			if (weight.Value <= 0) continue;
			totalWeight += weight.Value;
			totalWeights.Add(weight.Key, totalWeight);
		}

		double randomTotalWeight = globals.random.NextDouble() * totalWeight;
		foreach (var weight in totalWeights) {
			if (weight.Value >= randomTotalWeight) {
				return weight.Key;
			}
		}
		return default(T);
	}
	public static T GetRandomWeighted<T>(Dictionary<T, int> weights) {
		var totalWeights = new Dictionary<T, int>();

		int totalWeight = 0;
		foreach (var weight in weights) {
			if (weight.Value <= 0) continue;
			totalWeight += weight.Value;
			totalWeights.Add(weight.Key, totalWeight);
		}

		int randomTotalWeight = (int)(globals.random.NextDouble() * (double)totalWeight);
		foreach (var weight in totalWeights) {
			if (weight.Value >= randomTotalWeight) {
				return weight.Key;
			}
		}
		return default(T);
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
		if (this.filterMin > mid + 2 * stDev) extremeMin = true;
		if (this.filterMax < mid - 2 * stDev) extremeMax = true;
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
		return result;
	}
	
	protected double NextGaussian() {
		double u1 = globals.random.NextDouble() * (1 - truncateExponent) + truncateExponent;
		double u2 = globals.random.NextDouble();
		double result = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
		result = 0.5 + normDev * result;
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
		return ShiftResult(globals.random.NextDouble());
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
	public static int minFriends = 3;
	public static JsonSerializerSettings jsonSettings = new JsonSerializerSettings() {
		MissingMemberHandling = MissingMemberHandling.Ignore,
		NullValueHandling = NullValueHandling.Ignore
	};
}
public static class Locale {
	public static string HUMAN = "human";
	public static string AVERAGE = "average";
	public static string VERY_SHORT = "very short";
	public static string SHORT = "short";
	public static string TALL = "tall";
	public static string VERY_TALL = "very tall";
	public static string VERY_THIN = "very thin";
	public static string THIN = "thin";
	public static string THICK = "thick";
	public static string VERY_THICK = "very thick";
	public static string[] PHILOSOPHY = {
		"Centrism",
		"Self Direction",
		"Universalism",
		"Benevolence",
		"Tradition",
		"Security",
		"Power",
		"Achievement",
		"Stimulation"
	};
	public static string[] PHIL_ACRONYM = {
		"Cen",
		"Slf",
		"Unv",
		"Ben",
		"Trd",
		"Sec",
		"Pow",
		"Ach",
		"Stm"
	};
	public static string[] MAJORS = {
		"Business",
		"Healthcare",
		"Social Science",
		"Psychology",
		"Engineering",
		"Biology",
		"Art",
		"Education",
		"Communication",
		"Security",
		"Computing",
		"Recreation",
		"Multidisciplinary",
		"Literature",
		"Liberal Arts",
		"Natural Resources",
		"Government",
		"Physical Sciences",
		"Family",
		"Math",
		"Foreign Language",
		"Philosophy",
		"Theology",
		"Architecture",
		"Minority Studies",
		"Telecom",
		"Transportation",
		"Law",
		"Count"
	};
	public static string[] HOBBIES = {
		"TV",
		"Family",
		"Music",
		"Friends",
		"Read",
		"Shopping",
		"Games",
		"Social Media",
		"Fitness",
		"Cooking",
	};

}


//
// Generator
//

public delegate void AddInitializer(params string[] names);
public class ThingMaker<T> where T : Thing, new() {
	private int nextID = 0;
	public Dictionary<string, ExpandoObject> patternCache = new Dictionary<string, ExpandoObject>();
	public Dictionary<string, Action<dynamic>> tagRegistry = new Dictionary<string, Action<dynamic>>();
	public ThingMaker() {}
	public void RegisterTags(Dictionary<string, Action<dynamic>> tags) {
		foreach (var tag in tags) {
			tagRegistry.Add(tag.Key, tag.Value);
		}
	}
	public void Create(Dictionary<int, T> things, int quantity, params string[] tags) {
		string tagString = String.Join(",", tags);
		T thing;

		// build pattern
		dynamic pattern;
		if (patternCache.ContainsKey(tagString)) {
			pattern = patternCache[tagString];
		} else {
			pattern = new ExpandoObject();
			pattern.create = new Dictionary<string, EventHandler>();
			pattern.create.Add("thing", null);
			pattern.AddInitializers = (AddInitializer)((names) => {
				foreach (var name in names){
					if (!pattern.create.ContainsKey(name)) pattern.create.Add(name, null);					
				}
			});
			foreach (var tag in tags) {
				if (tagRegistry.ContainsKey(tag)) {
					tagRegistry[tag].DynamicInvoke(pattern);
				} else {
					thing = new T();
					Console.WriteLine("ERROR: {0} not a valid tag to create {1}", tag, thing.GetType());
				}
			}
			patternCache.Add(tagString, pattern);
		}

		// make things
		for (int i = 0; i < quantity; i++) {
			thing = new T();
			thing.SetID(nextID);
			thing.SetPattern(pattern);
			things.Add(nextID, thing);
			nextID++;
		}
	}
}
public class Thing {
	public int id;
	public dynamic pattern;
	public Thing() { }
	public void SetID(int id) { this.id = id; }
	public void SetPattern(dynamic pattern) {
		this.pattern = pattern;
		EventHandler handler = pattern.create["thing"];
		if (handler != null) handler(this, EventArgs.Empty);
	}
}


//
// Person
//

public class Person : Thing {
	public string firstName = " ";
	public string lastName = " ";
	public Mind mind;
	public Body body;
	public Social social;
	public Family family;
	public Friends friends;
	public Person() : base() { }
	
	public void Create(params string[] traits){
		foreach (var trait in traits){
			EventHandler handler = pattern.create[trait];
			if (handler != null) handler(this, EventArgs.Empty);
		}
	}
}
public class Body {
	public int height, weight, health, hairFrontType, hairBackType, eyeType, extra1, extra2;
	public double age, density, skinLum, fitness, densityDistanceFactor;
	public Color skinColor, hairFrontColor, hairBackColor, eyeColor;
	public string eyeColorText;

	public Body() { }
}
public class Mind {
	Person me;
	public int iq, stress, flaw, goal, mood, phil;
	public double confidence, friendMult, education, knowledge, iqDistanceFactor, philRadius, philAngle, philX, philY;
	public static double neutralRadius = Math.Sqrt(1.0/(double)PHIL.COUNT);
	public static double[,] philCenterDistance = new double[,]{
		{0.00,0.75,0.75,0.75,0.75,0.75,0.75,0.75,0.75},
		{0.75,0.00,0.57,1.05,1.38,1.49,1.38,1.05,0.57},
		{0.75,0.57,0.00,0.57,1.05,1.38,1.49,1.38,1.05},
		{0.75,1.05,0.57,0.00,0.57,1.05,1.38,1.49,1.38},
		{0.75,1.38,1.05,0.57,0.00,0.57,1.05,1.38,1.49},
		{0.75,1.49,1.38,1.05,0.57,0.00,0.57,1.05,1.38},
		{0.75,1.38,1.49,1.38,1.05,0.57,0.00,0.57,1.05},
		{0.75,1.05,1.38,1.49,1.38,1.05,0.57,0.00,0.57},
		{0.75,0.57,1.05,1.38,1.49,1.38,1.05,0.57,0.00}
	};
	public enum PHIL {
		CENTRISM,
		SELF_DIRECTION,
		UNIVERSALISM,
		BENEVOLENCE,
		TRADITION,
		SECURITY,
		POWER,
		ACHIEVEMENT,
		STIMULATION,
		COUNT,
	}

	public Mind(Person p) { this.me = p; }
	public double PhilDistance(Person other, bool exact = true) {
		if (exact) return Tools.GetPolarDistance(philRadius, other.mind.philRadius, philAngle, other.mind.philAngle);
		return philCenterDistance[phil, other.mind.phil];
	}
	public void PickPhil(){
		double agePercent = 0.25 - 1 * me.pattern.ages.Percentile(me.body.age);
		do {
			philX = (new UniformP(-1, agePercent, 1)).NextDouble();
			philY = (new UniformP(-1, 0, 1)).NextDouble();
		} while (Math.Sqrt(philX*philX + philY*philY) > 1);
		Tools.CartesianToPolar(philX, philY, ref philRadius, ref philAngle);
		if (philRadius < neutralRadius){
			phil = (int)PHIL.CENTRISM;
		}else{
			phil = (int)Math.Round(philAngle * ((double)PHIL.COUNT-1.0) / (2.0*Math.PI));
			phil %= (int)PHIL.COUNT-1;
			phil += 1;
		}
	}
	public struct Point{
		public double x, y;
		public Point(double x, double y){
			this.x = x;
			this.y = y;
		}
	}
	public static void PrintPhilCenterDistances(){
		Point[] centroid = new Point[(int)PHIL.COUNT];
		centroid[0].x = 0;
		centroid[0].y = 0;
		double radius = Math.Sqrt(0.5 + 0.5/centroid.Length);
		for (int i=1; i<centroid.Length; i++){
			double angle = (i-1) * (2*Math.PI / (centroid.Length-1));
			centroid[i].x = radius * Math.Cos(angle);
			centroid[i].y = radius * Math.Sin(angle);
		}
		var adjacency = new double[centroid.Length, centroid.Length];
		for (int i=0; i<centroid.Length; i++){
			for (int j=0; j<centroid.Length; j++){
				adjacency[i,j] = Tools.GetCartesianDistance(centroid[i].x, centroid[j].x, centroid[i].y, centroid[j].y);
				if (j==0) Console.Write("{");
				Console.Write("{0:n2}", adjacency[i,j]);
				if (j==centroid.Length-1) Console.Write("}");
				Console.Write(",");
			}
			Console.Write("\n");
		}
	}
}
public class Family {
	Person me;
	public double socialClass, income, wealth;
	public Family(Person p) { this.me = p; }
}
public class Cluster {
	public Dictionary<int, Person> people = new Dictionary<int, Person>();
	public bool political;
	public int[] phil;
	public const int SEARCHING = -2;
	public const int OUTLIER = -1;
	public const int UNCLASSIFIED = 0;
	public Cluster(){ }
	public Cluster(Dictionary<int, Person> people, bool political = true){
		this.people = people;
		this.political = political;
		if (political) phil = new int [(int)Mind.PHIL.COUNT];
	}
	public void Add(Person p){
		people.Add(p.id, p);
		if (this.political) phil[p.mind.phil]++;
	}
}
public class Social {
	Person me;
	public double socialClass, income, wealth;
	public int factionID, maxCount, majorID;
	public int major;
	public static int maxClassSize = 40;
	public class Major {
		public int id;
		public string type;
		public int weight;
		public Major(int id, string type, int weight) {
			this.id = id;
			this.type = type;
			this.weight = weight;
		}
	}
	public class Hobby {
		public int id;
		public string type;
		public double prob;
		public bool socialEvent;
		public Hobby(int id, string type, double prob, bool socialEvent) {
			this.id = id;
			this.type = type;
			this.prob = prob;
			this.socialEvent = socialEvent;
		}
	}
	public HashSet<Major> majors = new HashSet<Major>();
	public HashSet<Hobby> hobbies = new HashSet<Hobby>();
	
	public Social(Person p) {
		this.me = p;
	}
	public static HashSet<int> GetGroups(Dictionary<int, Person> people){
		var results = new HashSet<int>();
		
		// create class list
		var majorCount = 28;
		var classes = new List<HashSet<int>>[majorCount];
		var majorStudents = new HashSet<int>[majorCount];
		for (int majorID=0; majorID<classes.Length; majorID++){
			classes[majorID] = new List<HashSet<int>>();
			majorStudents[majorID] = new HashSet<int>();			
		}
		
		// assign people to classes
		foreach (var p in people.Values){
			//p.social.myHobbies.Add(Tools.GetRandomWeighted<Hobby>(p.social.hobbyWeights));
			var majorArray = p.social.GetMajors();
			var majorWeights = new Dictionary<int, double>();
			for (int id=0; id<majorArray.Length; id++){
				majorWeights[id] = majorArray[id].weight;
			}
			int majorID = Tools.GetRandomWeighted<int>(majorWeights);
			p.social.majors.Add(majorArray[majorID]);
			majorStudents[majorID].Add(p.id);
		}
		for (int majorID=0; majorID<classes.Length; majorID++){
			int classSize = (int)Math.Ceiling((double)majorStudents[majorID].Count / maxClassSize);
			var currentClass = new HashSet<int>();
			foreach (var personID in majorStudents[majorID]){
				if (currentClass.Count >= classSize){
					classes[majorID].Add(currentClass);
					currentClass = new HashSet<int>();
				}
				currentClass.Add(personID);
			}
		}
		return results;
	}
	public Major[] GetMajors(){
		int id = 0;
		return new Major[]{
			new Major(id++, "Business",			192),
			new Major(id++, "Healthcare",		114),
			new Major(id++, "Social Science",	88),
			new Major(id++, "Psychology",		62),
			new Major(id++, "Engineering",		61),
			new Major(id++, "Biology",			58),
			new Major(id++, "Art",				51),
			new Major(id++, "Education",		48),
			new Major(id++, "Communication",	48),
			new Major(id++, "Security",			33),
			new Major(id++, "Computing",		31),
			new Major(id++, "Recreation",		26),
			new Major(id++, "Multidisciplinary",25),
			new Major(id++, "Literature",		24),
			new Major(id++, "Liberal Arts",		23),
			new Major(id++, "Natural Resources",19),
			new Major(id++, "Government",		18),
			new Major(id++, "Physical Sciences",16),
			new Major(id++, "Family",			13),
			new Major(id++, "Math",				12),
			new Major(id++, "Foreign Language",	10),
			new Major(id++, "Philosophy",		6),
			new Major(id++, "Theology",			5),
			new Major(id++, "Architecture",		5),
			new Major(id++, "Minority Studies",	4),
			new Major(id++, "Telecom",			3),
			new Major(id++, "Transportation",	2),
			new Major(id++, "Law",				2),
		};
	}
	public Hobby[] GetHobbies(){
		int id = 0;
		return new Hobby[]{
			new Hobby(id++, "TV",			0.55, false),
			new Hobby(id++, "Family",		0.50, false),
			new Hobby(id++, "Music",		0.40, false),
			new Hobby(id++, "Friends",		0.40, true),
			new Hobby(id++, "Read",			0.40, false),
			new Hobby(id++, "Shopping",		0.35, false),
			new Hobby(id++, "Games",		0.30, true),
			new Hobby(id++, "Social Media",	0.30, true),
			new Hobby(id++, "Fitness",		0.20, false),
			new Hobby(id++, "Cooking", 		0.20, false)
		};
	}
}
public class Friends{
	Person me;
	public Dictionary<int, double> adjacency = new Dictionary<int, double>();
	public int Count{get{return adjacency.Count;}}
	public double max, farDistance;
	public int groupID, farFriendID;
	public Person closest;
	
	
	public Friends(Person p) { this.me = p; }
	public static void FindNetwork(Dictionary<int, Person> people){
		Console.WriteLine("\n\n=== Friend Network ===");
		bool debug = true;
		
		double sizeRoot = Math.Sqrt(people.Count);
		int needFriends = people.Count;
		if (people.Count > 500) Console.WriteLine(">>> OVERSIZE <<<\n");
		int totalIterations = 20;
		int startClustering = 0;
		int startRelaxing = 18;
		var stopWatch = new Stopwatch();
		long startTime = 0;
        stopWatch.Start();
		Console.WriteLine("delay step needFriends");
		for (int i=0; i<totalIterations; i++){
			startTime = stopWatch.ElapsedMilliseconds;
			int maxFriendsCap = 1000;
			// gradually growing friend networks results in slower and less realistic behavior
			//int maxFriendsCap = 1 + (int)Math.Pow(2, i<20 ? i:20);
			needFriends = 0;
			foreach (var me in people.Values){
				var myMaxFriends = Math.Min(maxFriendsCap, globals.minFriends + Math.Round(sizeRoot * me.mind.friendMult));
				if (i >= startRelaxing) myMaxFriends--;
				if (me.friends.Count < myMaxFriends || i==startClustering) {
					needFriends++;
				}
			}
			if (debug && i!=0 && i==startClustering) Console.WriteLine("Forming friend groups...");
			if (debug && i==startRelaxing) Console.WriteLine("Relaxing friendship demands...");
			foreach (var me in people.Values){
				var myMaxFriends = Math.Min(maxFriendsCap, globals.minFriends + Math.Round(sizeRoot * me.mind.friendMult));
				if (i >= startRelaxing) myMaxFriends--;
				if (me.friends.Count < myMaxFriends || i==startClustering) {
					AddToNetwork(ref people, me, i>=startClustering, maxFriendsCap);
				}
			}
			if (debug) Console.WriteLine("{0,-5} {1,-4} {2}", stopWatch.ElapsedMilliseconds - startTime, i, needFriends);
		}
		if (debug) Console.WriteLine("\n{0} total milliseconds", stopWatch.ElapsedMilliseconds);
		stopWatch.Stop();
	}
	public static void AddToNetwork(ref Dictionary<int, Person> people, Person me, bool clustering, int maxFriendsCap){	
		double sizeRoot = Math.Sqrt(people.Count);
		int myMaxFriends = Math.Min(maxFriendsCap, globals.minFriends + (int)Math.Round(sizeRoot * me.mind.friendMult));
		for (int i=0; i<people.Count; i++){
			var other = people[i];
			if (me == other) continue;
			if (me.friends.adjacency.ContainsKey(other.id)) continue;
			int otherMaxFriends = Math.Min(maxFriendsCap, globals.minFriends + (int)Math.Round(sizeRoot * other.mind.friendMult));
			double distance = GetDistance(me, other, false, clustering);
			bool iLikeThem  = me.friends.Count < myMaxFriends || distance < me.friends.farDistance;
			bool theyLikeMe = other.friends.Count < otherMaxFriends || distance < other.friends.farDistance;
			
			if (iLikeThem && theyLikeMe) {
				if (me.friends.Count >= myMaxFriends){
					me.friends.Remove(people[me.friends.farFriendID]);
				}
				if (other.friends.Count >= otherMaxFriends){
					other.friends.Remove(people[other.friends.farFriendID]);
				}
				me.friends.Add(other, distance);
			}
		}
		if (!people.ContainsKey(me.id)) people.Add(me.id, me);
	}
	public static double GetDistance(Person me, Person other, bool exact = false, bool clustering = true) {
		double distance = 0;
		distance += 4 * Math.Abs(me.body.age - other.body.age) / (me.body.age + other.body.age);
		distance += Math.Abs(me.body.skinLum - other.body.skinLum);
		distance += Math.Abs(me.body.density - other.body.density) * me.body.densityDistanceFactor;
		distance += Math.Abs(me.mind.iq - other.mind.iq) * me.mind.iqDistanceFactor;
		distance += 1 - Math.Abs(me.mind.confidence - other.mind.confidence);
		//distance += me.social.hobby == other.social.hobby ? 0 : 1;
		//distance += me.social.major == other.social.major ? 0 : 1;
		distance += me.mind.PhilDistance(other, exact);
		double numFriends = 0;
		if (clustering) {
			foreach (var myFriendID in me.friends.adjacency.Keys){
				if (other.friends.adjacency.ContainsKey(myFriendID)) numFriends += 1;
				//if (other.friends.adjacency.ContainsKey(myFriendID)) numFriends += 1-other.mind.confidence;
			}
		}
		distance *= 0.0 + Math.Pow(0.8, numFriends);
		distance += 0;
		return distance;
	}
	public Tuple<Person, double> GetClosest(Dictionary<int, Person> people) {
		Person maxPerson = null, minPerson = null;
		double minDistance = double.PositiveInfinity;
		double maxDistance = 0;
		double distance;
		foreach (Person other in people.Values) {
			if (other == me) continue;
			distance = Friends.GetDistance(me, other);
			if (distance > maxDistance) {
				maxPerson = other;
				maxDistance = distance;
			} else if (distance < minDistance) {
				minPerson = other;
				minDistance = distance;
			}
		}
		if (maxPerson == null) {
			Console.WriteLine("maxPerson == null");
			maxDistance = 0;
			maxPerson = me;
		}
		if (minPerson == null) {
			Console.WriteLine("maxPerson == null");
			minDistance = 0;
			minPerson = me;
		}
		return new Tuple<Person, double>(minPerson, minDistance);
	}
	public void Add(Person other, double distance){
		if (me.friends.adjacency.ContainsKey(other.id)){
			Console.WriteLine("WARNING: {0} {1} is already a friend of {2} {3}",
				other.firstName,
				other.lastName.Substring(0,1),
				me.firstName,
				me.lastName.Substring(0,1)
			);
			foreach (var friend in me.friends.adjacency){
				Console.WriteLine("id={0,3} dist={2:n2}",
					friend.Key,
					friend.Value
				);
			}
			return;
		}
		me.friends.adjacency.Add(other.id, distance);
		other.friends.adjacency.Add(me.id, distance);
		if (distance > me.friends.farDistance){
			me.friends.farFriendID = other.id;
			me.friends.farDistance = distance;
		}
		if (distance > other.friends.farDistance){
			other.friends.farFriendID = me.id;
			other.friends.farDistance = distance;
		}
	}
	public void Remove(Person other){
		if (!me.friends.adjacency.ContainsKey(other.id)){
			Console.WriteLine("{0} {1} is not a friend of {2} {3}",
				other.firstName,
				other.lastName.Substring(0,1),
				me.firstName,
				me.lastName.Substring(0,1)
			);
			foreach (var friend in me.friends.adjacency){
				Console.WriteLine("id={0,3} dist={2:n2}",
					friend.Key,
					friend.Value
				);
			}
			return;
		}
		var distance = me.friends.adjacency[other.id];
		me.friends.adjacency.Remove(other.id);
		other.friends.adjacency.Remove(me.id);
		if (distance == me.friends.farDistance){
			me.friends.farFriendID = -1;
			me.friends.farDistance = 0;
			foreach (var friend in me.friends.adjacency){
				if (friend.Value > me.friends.farDistance){
					me.friends.farFriendID = friend.Key;
					me.friends.farDistance = friend.Value;
				}
			}
		}
		if (distance == other.friends.farDistance){
			other.friends.farFriendID = -1;
			other.friends.farDistance = 0;
			foreach (var friend in other.friends.adjacency){
				if (friend.Value > other.friends.farDistance){
					other.friends.farFriendID = friend.Key;
					other.friends.farDistance = friend.Value;
				}
			}
		}
	}
	public static int GetFriendGroupID(Person p, Dictionary<int, Person> people){
		if (p.friends.groupID == Cluster.UNCLASSIFIED){
			p.friends.groupID = Cluster.SEARCHING;
			foreach (var friend in p.friends.adjacency){
				int groupID = GetFriendGroupID(people[friend.Key], people);
				if (groupID > 0) {
					// found an existing cluster
					p.friends.groupID = groupID;
				}
			}
		}
		return p.friends.groupID;
	}
	public static void SetFriendGroupID(Person p, Dictionary<int, Person> people, int id){
		p.friends.groupID = id;
		foreach (var friend in p.friends.adjacency){
			if (people[friend.Key].friends.groupID != id) {
				SetFriendGroupID(people[friend.Key], people, id);
			}
		}
	}
	public static void PrintFriendClusters(Dictionary<int, Person> people){
		if (people == null) return;
		int nextClusterID = 1;
		foreach (var p in people.Values){
			if (p.friends.groupID == Cluster.UNCLASSIFIED){
				SetFriendGroupID(p, people, nextClusterID++);
			}
		}
		if (nextClusterID == 1) return;
		var clusters = new List<Dictionary<int, Person>>();
		for (int i=1; i<nextClusterID; i++){
			clusters.Add(new Dictionary<int, Person>());
		}
		foreach (var p in people.Values){
			if (p.friends.groupID > clusters.Count){
				Console.WriteLine("{0} {1}", p.friends.groupID, clusters.Count);
			}
			clusters[p.friends.groupID-1].Add(p.id, p);
		}
		
		clusters.Sort((a, b) => b.Count - a.Count);
		for (int i=0; i<clusters.Count; i++){
			Console.WriteLine("cluster {0,2} has {1,3} people", i, clusters[i].Count);
		}
	}
	public static void PrintFriendCount(Dictionary<int, Person> people){
		Console.WriteLine("\n\n=== Friend Count ===");
		var friendCount = new int[25];
		foreach (var p in people.Values) {
			if (friendCount.Length <= p.friends.Count + 1) {
				System.Array.Resize(ref friendCount, p.friends.Count + 1);
			}
			friendCount[p.friends.Count]++;
		}
		for (int i=0; i<friendCount.Length; i++){
			Console.WriteLine("{0},{1}", i, friendCount[i]);
		}
		Console.Write("\n");
	}
	public static void PrintFriends(Dictionary<int, Person> people){
		Console.WriteLine("\n=== Friends ===");
		var degreeChart = new Dictionary<int, Dictionary<int, int>>();
		foreach (var p in people.Values){
			double avgDistance=0, avgCount=0;
			int medCount=0;
			if (p.friends.Count > 0){
				var friendCounts = new int[p.friends.Count];
				int i = 0;
				foreach (var friend in p.friends.adjacency){
					avgDistance += friend.Value;
					avgCount += people[friend.Key].friends.Count;
					friendCounts[i++] = people[friend.Key].friends.Count;
				}
				avgDistance /= p.friends.Count;
				avgCount /= p.friends.Count;
				Array.Sort(friendCounts);
				if (friendCounts.Length % 2 == 1){
					medCount = friendCounts[(int)Math.Floor((double)friendCounts.Length / 2)];
				} else {
					medCount = friendCounts[friendCounts.Length / 2];
					medCount += friendCounts[friendCounts.Length / 2 - 1];
					medCount = (int)Math.Round(medCount / 2.0);
				}
			}
			Console.Write("{0} {1}, {2:n3}, {3}, {4:n3}, {5}",
				p.firstName,
				p.lastName.Substring(0,1),
				p.mind.confidence,
				medCount,
				avgDistance,
				p.friends.Count
			);
			Console.Write("\n");
			if (!degreeChart.ContainsKey(medCount)){
				degreeChart.Add(medCount, new Dictionary<int,int>());
			}
			if (!degreeChart[medCount].ContainsKey(p.friends.Count)){
				degreeChart[medCount].Add(p.friends.Count, 0);
			}
			degreeChart[medCount][p.friends.Count]++;
		}
		Console.Write("\n");
		Console.WriteLine("=== Count of Degree per Median Friends' Degree ===");
		
		foreach (var medCount in degreeChart.Keys){
			foreach (var degree in degreeChart[medCount].Keys){
				Console.WriteLine("{0},{1},{2}",
					degree,
					medCount,
					degreeChart[medCount][degree]
				);
			}
		}
	}
	public static double GetClusteringCoefficient(Dictionary<int, Person> people){
		var friendGraph = new Dictionary<int, int[]>();
		// convert adjacency dictionaries to arrays for iteration
		foreach (var p in people){
			int personID = p.Key;
			var friends = p.Value.friends.adjacency;
			friendGraph.Add(personID, new int[friends.Count]);
			int index = 0;
			foreach (var friendID in friends.Keys){
				friendGraph[personID][index++] = friendID;
			}
		}
		int closedTriplets = 0;
		int totalTriplets = 0;
        foreach (var p in friendGraph){
			int personID = p.Key;
			var friends = p.Value;
			for (int i=0; i<friendGraph[personID].Length; i++){
				for (int k=i+1; k<friendGraph[personID].Length; k++){
					// k=i+1 to prevent re-scanning edges
					totalTriplets++;
					if (people[friends[i]].friends.adjacency.ContainsKey(friends[k])){
						closedTriplets++;
					}
				}
			}
        }
		Console.Write("\n{0} closed triplets, out of {1} total triplets", closedTriplets, totalTriplets);
		
		return (double)closedTriplets / (double)totalTriplets;
	}
}


//
// Tags
//

public static class Human {
	public static string tagName = "human";
	public static void RegisterTag(dynamic pattern) {
		pattern.ages			= new GaussP(18.0,  30.0, 107.0, 15.0);
		pattern.heights			= new GaussP(54.0, 170.0, 272.0,  7.0);
		pattern.densities		= new GaussP( 8.0,  13.0,  25.0,  4.0);
		pattern.iqs				= new GaussP(50.0, 100.0, 150.0, 15.0);
		pattern.skinLums		= new UniformP(0.05, 0.45, 0.85);
		pattern.skinHues		= new GaussP(28.0, 36.0);
		pattern.brownHairLums	= new UniformP(0.05, 0.6, 0.95);
		pattern.greyHairSats	= new UniformP(0.0, 0.1d);
		pattern.redHairLums		= new GaussP(0.30, 0.42, 0.50, 0.02);
		pattern.redHairSats		= new GaussP(0.20, 0.40, 0.60, 0.05);
		pattern.redHairHues		= new GaussP(5.00,15.0, 25.0,  3.0);
		pattern.hairFrontTypes	= 10;
		pattern.hairBackTypes	= 10;
		pattern.redThreshold	= 0.8;
		pattern.redChance		= 0.5;
		pattern.agingStart		= 30.0;
		pattern.agingEnd		= 60.0;
		pattern.canGrey			= false;
		pattern.AddInitializers("body", "mind", "family", "social", "friends");
		//pattern.CreateThing		+= new EventHandler(OnCreateThing);
		
		pattern.create["body"]		+= new EventHandler(OnCreateBody);
		pattern.create["mind"]		+= new EventHandler(OnCreateMind);
		pattern.create["family"]	+= new EventHandler(OnCreateFamily);
		pattern.create["social"]	+= new EventHandler(OnCreateSocial);
		pattern.create["friends"]	+= new EventHandler(OnCreateFriends);
		pattern.educationTiers	= new DiscreteP(new Dictionary<double, int>{{0.03, 25},{0.13, 20},{0.34, 18},{0.61, 14},{0.89, 12},{1.00,  8}
		});
	}
	public static void OnCreateThing(object sender, EventArgs e) {
		Person p = (Person)sender;
	}

	//
	// Create Body
	//
	public static void OnCreateBody(object sender, EventArgs e) {
		Person p = (Person)sender;
		p.body				= new Body();
		p.body.age			= p.pattern.ages.NextDouble();
		p.body.density		= p.pattern.densities.NextDouble();
		p.body.densityDistanceFactor = 1.0 / p.pattern.densities.range;
		p.body.height		= (int)p.pattern.heights.NextDouble();
		p.body.weight		= (int)(p.body.density * Math.Pow((double)p.body.height / 100.0, 3.0));
		p.body.skinColor	= SkinColor(p);
		//p.body.eyeColor	= EyeColor(p);
		var fitnessProb		= new UniformP(0, 0.45 * (1 - p.pattern.densities.PercentFromMedian(p.body.density)), 1);
		p.body.fitness		= fitnessProb.NextDouble();
		p.body.hairFrontColor	= HairFrontColor(p);
		p.body.hairBackColor	= p.body.hairFrontColor;
	}
	public static Color SkinColor(Person p) {
		p.body.skinLum = p.pattern.skinLums.NextDouble();
		double skinHue = p.pattern.skinHues.NextDouble();
		double skinSat = Tools.Constrain(0.5d, 0.5d * Math.Pow(p.body.skinLum, 2.0) + 0.5d, 1.0d);
		return Tools.HlsToRgb(skinHue, p.body.skinLum, skinSat);
	}
	public static Color HairFrontColor(Person p) {
		double lum, sat, hue;

		if (p.body.skinLum > p.pattern.redThreshold && globals.random.NextDouble() < p.pattern.redChance) {
			// red hair
			lum = p.pattern.redHairLums.NextDouble();
			sat = p.pattern.redHairSats.NextDouble();
			hue = p.pattern.redHairHues.NextDouble();
			//Console.WriteLine("Red hair");
			//Console.WriteLine("Red hair  : {0:n0} {1:n2} {2:n2} {3:n2} {4:n2}", p.body.age, p.body.skinLum, hue, lum, sat);
		} else {
			// brown hair
			lum = p.pattern.brownHairLums.NextDouble() * p.body.skinLum / 0.9d + 0.05d;
			sat = Tools.Constrain(0.0, 0.25d + 0.75d * Math.Pow(lum, 2) + (-0.05d + 0.1d * globals.random.NextDouble()), 1.0);

			double hueRange = 2.0 + 20.0 * Math.Pow(1.0 - lum, 2);
			hue = 20.0 + 30.0*lum + hueRange*globals.random.NextDouble();
			//Console.WriteLine("Brown hair: {0:n0} {1:n2} {2:n2} {3:n2} {4:n2}", p.body.age, p.body.skinLum, hue, lum, sat);
		}

		if (p.pattern.canGrey && (p.body.age > p.pattern.agingEnd || (p.body.age > p.pattern.agingStart && globals.random.NextDouble() < (p.body.age - p.pattern.agingStart) / (p.pattern.agingEnd - p.pattern.agingStart)))) {
			// grey hair
			lum = 0.5d + 0.5d * lum;
			sat = p.pattern.greyHairSats.NextDouble();
			//Console.WriteLine("Grey hair : {0:n0} {1:n2} {2:n2} {3:n2} {4:n2}", p.body.age, p.body.skinLum, hue, lum, sat);
		}
		return Tools.HlsToRgb(hue, lum, sat);
	}
	public static Color EyeColor(Person p) {
		var eyeColorWeights = new Dictionary<string, double>() {{"brown",   70.0}, // 70&{"hazel",   16.0}, // 16%{"blue",	80.0*p.body.skinLum}, // 8%{"green",   20.0*p.body.skinLum}, // 2%{"gray",	30.0*p.body.skinLum}, // 3%{"amber",   10.0*p.body.skinLum}, // 1%
		};
		p.body.eyeColorText = Tools.GetRandomWeighted(eyeColorWeights);
		// TODO: convert to color
		return new Color();
	}

	//
	// Create Mind
	//
	public static void OnCreateMind(object sender, EventArgs e) {
		Person p = (Person)sender;
		if (p.body == null) OnCreateBody(sender, e);
		var ages = p.pattern.ages;

		p.mind = new Mind(p);
		p.mind.iq = (int)p.pattern.iqs.NextDouble();
		p.mind.iqDistanceFactor = 1.0 / p.pattern.iqs.range;

		var confidenceProb = new UniformP(0.1, p.body.fitness, 1.0);
		p.mind.confidence = confidenceProb.NextDouble();
		p.mind.confidence += (p.body.age - ages.min) / (ages.max - ages.min);
		p.mind.confidence /= 2.0;
		p.mind.friendMult = Math.Pow(2.0*p.mind.confidence, 3) / 2.0;
		p.mind.education = p.pattern.educationTiers.NextDouble();
		p.mind.PickPhil();
	}

	//
	// Create Family
	//
	public static void OnCreateFamily(object sender, EventArgs e) {
		Person p = (Person)sender;
		if (p.mind == null) OnCreateMind(sender, e);
		p.family = new Family(p);
		var s = GetSocialClass();
		p.family.socialClass = s.Item1;
		p.family.income = s.Item2;
		p.family.wealth = s.Item3;
	}
	public static Tuple<double, double, double> GetSocialClass(double med = 0.5, bool uniform = false) {
		double p, wealth, income;
		if (uniform) {
			var prob = new UniformP(0, med, 1);
			p = prob.NextDouble();
		} else {
			var prob = new GaussP(0, med, 1, 0.1);
			p = prob.NextDouble();
		}
		if (p < 0.8) {
			wealth = 1000000 * Math.Pow(p, 3.3) - 1000;
		} else if (p < 0.99999) {
			wealth = 100000 / (1 - p);
		} else {
			wealth = 100000 / (0.00001);
		}
		income = 30000 * (2 * p + 1 / Math.Pow(1 - p, 0.5) - 1);
		return new Tuple<double, double, double>(p, income, wealth);
	}

	//
	// Create Social
	//
	public static void OnCreateSocial(object sender, EventArgs e) {
		Person p = (Person)sender;
		if (p.family == null) OnCreateFamily(sender, e);
		p.social = new Social(p);
		var s = GetSocialClass(p.family.socialClass, true);
		p.social.socialClass = s.Item1;
		p.social.income = s.Item2;
		p.social.wealth = s.Item3;
	}
	
	//
	// Create Social
	//
	public static void OnCreateFriends(object sender, EventArgs e) {
		Person p = (Person)sender;
		if (p.family == null) OnCreateFriends(sender, e);
		p.friends = new Friends(p);
	}
}
public static class American {
	public static string tagName = "american";
	public static void RegisterTag(dynamic a) {
		a.densities = new GaussP(8.0, 15.0, 25.0, 4.0);
		a.firstNames = new string[] { "Ray", "Angel", "Gene", "Rowan", "Leslie", "Mell", "Sam", "Danni", "Angel", "Bev", "Riley", "Steff", "Denny", "Phoenix", "Ashley", "Kerry", "Ashton", "Jordan", "Maddox", "Aubrey", "Mel", "Dane", "Eli", "Willy", "Steff", "Rory", "Will", "Cameron", "Ashton", "Clem", "Reed", "Val", "Bret", "Jess", "Harley", "Clem", "Tanner", "Rory", "Brice", "Ray" };
		a.lastNames = new string[] { "Knight", "Barrett", "Barker", "Hamilton", "Miller", "Hernandez", "Bennett", "Kelley", "Gamble", "Huber", "Thompson", "Willis", "Mills", "Mills", "Fox", "Jacobson", "Larson", "Slater", "Osborn", "Nieves", "Bailey", "Barnes", "Ryan", "Holmes", "Sharp", "Vincent", "Wagner", "Meadows", "Weber", "Mejia", "Ryan", "Chambers", "Willis", "Murphy", "Lawson", "Stephenson", "Dixon", "Mullen", "Mullen", "Guthrie" };
		a.create["thing"] += new EventHandler(OnCreateThing);
	}
	public static void OnCreateThing(object sender, EventArgs e) {
		Person p = (Person)sender;
		p.firstName = p.pattern.firstNames[globals.random.Next(p.pattern.firstNames.Length)];
		p.lastName = p.pattern.lastNames[globals.random.Next(p.pattern.lastNames.Length)];
	}
}
public static class Japanese {
	public static string tagName = "japanese";
	public static void RegisterTag(dynamic a) {
		a.skinLums = new UniformP(0.05, 0.70, 0.85);
		a.heights = new GaussP(54.0, 150.0, 272.0, 7.0);
		a.redChance = 0.1;
		a.firstNames = new string[] { "Kiya", "Kirishima", "Kabuto", "Amari", "Kamiya", "Kurata", "Misaki", "Shirai", "Ushioda", "Okimoto", "Sasagawa", "Akagi", "Minami", "Ishida", "Yagami", "Ishikura", "Tada", "Ichioka", "Sekino", "Zakaza", "Minami", "Yamazaki", "Kono", "Tsukamoto", "Teramoto", "Masuda", "Yanagi", "Jinnouchi", "Wakatsuchi", "Uyehara", "Muramoto", "Suto", "Nishiyama", "Gima", "Hoashi", "Yamaha", "Kawashima", "Ogasawara", "Aomine", "Karasu" };
		a.lastNames = new string[] { "Fumiya", "Naora", "Chisato", "Suzu", "Romi", "Nanako", "Marise", "Himeka", "Inari", "Akeno", "Yoko", "Suko", "Riku", "Yukiji", "Romi", "Kao", "Kisa", "Nara", "Hairi", "Riko", "Soshitsu", "Nobuyori", "Matsu", "Sadao", "Moromao", "Okakura", "Mabuchi", "Ichibei", "Toson", "Keita", "Katsuhito", "Kanezane", "Junnosuke", "Ippei", "Danno", "Kyoichi", "Ryoko", "Katsumi", "Naoko", "Gennosuke" };
		a.create["thing"] += new EventHandler(OnCreateThing);
	}
	public static void OnCreateThing(object sender, EventArgs e) {
		Person p = sender as Person;
		p.firstName = p.pattern.firstNames[globals.random.Next(p.pattern.firstNames.Length)];
		p.lastName = p.pattern.lastNames[globals.random.Next(p.pattern.lastNames.Length)];
	}
}
public static class Student {
	public static string tagName = "student";
	public static void RegisterTag(dynamic pattern) {
		pattern.ages				= new GaussP(18.0,  22.0, 50.0);
		pattern.AddInitializers("social");
		//pattern.CreateThing		+= new EventHandler(OnCreateThing);
		
		//pattern.create["body"]	+= new EventHandler(OnCreateBody);
		//pattern.create["mind"]	+= new EventHandler(OnCreateMind);
		//pattern.create["family"]	+= new EventHandler(OnCreateFamily);
		pattern.create["social"]	+= new EventHandler(OnCreateSocial);
		//pattern.create["friends"]	+= new EventHandler(OnCreateFriends);
	}
	public static void OnCreateSocial(object sender, EventArgs e) {
		Person p = (Person)sender;
		//p.social.major = Tools.GetRandomWeighted(Social.majors);
		//p.social.hobby = Tools.GetRandomWeighted(Social.hobbies);
	}
}

//
// Test Program
//

public class Program {
	public static void PrintClusters(Dictionary<int, Person> people, List<Cluster> clusters){
		Console.WriteLine("=== CLUSTERS ===");
		int total = 0;
		for (int i = 0; i < clusters.Count; i++) {
			int count = clusters[i].people.Count;
			total += count;
			
			string visual = "";
			for (int j=0; j<count; j++) visual += "+";
			Console.WriteLine();
			Console.WriteLine(visual);
			
			string plural = (count != 1) ? "s" : "";
			Console.WriteLine("Cluster {0} consists of {1} people.", i + 1, count, plural);
			for (int philID=0; philID<clusters[i].phil.Length; philID++){
				if (clusters[i].phil[philID] == 0) continue;
				Console.WriteLine("{0} {1}", Locale.PHIL_ACRONYM[philID], clusters[i].phil[philID]);
			}
			Console.WriteLine();
		}
		total = people.Count - total;
		if (total > 0) {
			string plural = (total != 1) ? "people" : "p";
			string verb = (total != 1) ? "are" : "is";
			
			int[] phil = new int [(int)Mind.PHIL.COUNT];
			string visual = "";
			foreach (var p in people.Values){
				if (p.social.factionID == Cluster.OUTLIER) {
					phil[p.mind.phil]++;
					visual += "+";
				}
			}
			Console.WriteLine();
			Console.WriteLine(visual);
			Console.WriteLine("{0} {1} {2} outliers.", total, plural, verb);
			for (int philID=0; philID<phil.Length; philID++){
				Console.WriteLine("{0} {1}", Locale.PHIL_ACRONYM[philID], phil[philID]);
			}
			Console.WriteLine();
		}
	}
	public static void PrintPoliticalDistanceChart(Dictionary<int, Person> people) {
		Console.WriteLine("=== POLITICAL DISTANCE ===");
		foreach (var p in people.Values) {
			string output = "";
			var bestFriendStats = p.friends.GetClosest(people);
			var closest = bestFriendStats.Item1;
			var bestFriendDistance = bestFriendStats.Item2;
			output += String.Format(
				"{0} {1}.,{2:n0},{3:n1},{4:n2},{5:n0},{6},{7}",
				p.firstName,
				p.lastName.Substring(0,1),
				p.body.age,
				p.mind.iq,
				p.mind.philRadius,
				p.mind.philAngle*(180/Math.PI),
				Locale.PHIL_ACRONYM[(int)p.mind.phil],
				p.social.factionID
				);
			output += String.Format(",{0:n3}", p.mind.PhilDistance(closest));
			output += String.Format(",{0:n3}", bestFriendDistance);
			output += String.Format(
				",{0} {1}.,{2:n0},{3:n1},{4:n2},{5:n0},{6},{7}",
				closest.firstName,
				closest.lastName.Substring(0,1),
				closest.body.age,
				closest.mind.iq,
				closest.mind.philRadius,
				closest.mind.philAngle*(180/Math.PI),
				Locale.PHIL_ACRONYM[(int)closest.mind.phil],
				closest.social.factionID
				);
			Console.WriteLine(output);
		}
	}
	public static void PrintSocialDistanceChart(Dictionary<int, Person> people) {
		Console.WriteLine("=== SOCIAL DISTANCE ===");
		foreach (var p in people.Values) {
			string output = "";
			var closestStats = p.friends.GetClosest(people);
			var closest = closestStats.Item1;
			var closestDistance = closestStats.Item2;
			output += String.Format(
				"{0} {1}.,{2:n0},{3:n2},{4:n0},{5:n2},{6:n2},{7:n2},{8},{9}",
				p.firstName,
				p.lastName.Substring(0,1),
				p.body.age,
				p.body.skinLum,
				p.mind.iq,
				p.body.density,
				p.body.fitness,
				p.mind.confidence,
				Locale.PHIL_ACRONYM[(int)p.mind.phil],
				p.social.factionID
				);
			output += String.Format(",{0:n3}", p.mind.PhilDistance(closest));
			output += String.Format(",{0:n3}", closestDistance);
			output += String.Format(
				",{0} {1}.,{2:n0},{3:n2},{4:n0},{5:n2},{6:n2},{7:n2},{8},{9}",
				closest.firstName,
				closest.lastName.Substring(0,1),
				closest.body.age,
				closest.body.skinLum,
				closest.mind.iq,
				closest.body.density,
				closest.body.fitness,
				closest.mind.confidence,
				Locale.PHIL_ACRONYM[(int)closest.mind.phil],
				closest.social.factionID
				);
			Console.WriteLine(output);
		}
		Console.Write("\n");
	}
	public static void PrintToneChart(Dictionary<int, Person> people) {
		Console.WriteLine("=== TONES ===");
		foreach (var p in people.Values) {
			Console.WriteLine(
				"{0},{1:n0},{2:n1},{3:n0},{4},{5},{6},{7},{8},{9}",
				p.firstName,
				p.body.age,
				p.body.height * globals.m2ft,
				p.body.weight * globals.kg2lbs,
				p.body.skinColor.R,
				p.body.skinColor.G,
				p.body.skinColor.B,
				p.body.hairFrontColor.R,
				p.body.hairFrontColor.G,
				p.body.hairFrontColor.B
				);
		}
	}
	public static void Main(string[] args) {
		var codeCulture = new System.Globalization.CultureInfo("en-US");
		System.Threading.Thread.CurrentThread.CurrentCulture = codeCulture;
		System.Threading.Thread.CurrentThread.CurrentUICulture = codeCulture;
		
		var peopleMaker = new ThingMaker<Person>();
		peopleMaker.RegisterTags(new Dictionary<string, Action<dynamic>>(){
			{Human.tagName, Human.RegisterTag},
			{American.tagName, American.RegisterTag},
			{Japanese.tagName, Japanese.RegisterTag},
			{Student.tagName, Student.RegisterTag},
		});

		var people = new Dictionary<int, Person>();
		peopleMaker.Create(people, 250, "human", "student", "american");
		peopleMaker.Create(people, 250, "human", "student", "japanese");
		
		foreach (var p in people.Values) {
			p.Create("body","mind","social","friends");
		}
		
		Social.GetGroups(people);
		
		//Friends.FindNetwork(people);
		//Console.WriteLine("\nFriend network clustering coefficient = {0:n3}", Friends.GetClusteringCoefficient(people));
		if (people.Count > 500) Console.WriteLine(">>> OVERSIZE <<<\n");
		//Friends.PrintFriendCount(people);
		//Friends.PrintFriends(people);
	}
}

}