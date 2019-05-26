	// Character creation engine by Victor Isbell
	// Compiler version 4.0.30319.17929 for Microsoft (R) .NET Framework 4.5

	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Drawing;
	using System.Runtime.InteropServices;
	using System.Dynamic;
	using System.Threading;
	using System.Threading.Tasks;
	using Newtonsoft.Json;

	namespace Rextester {

	//
	// Toolbox
	//
	public static class Tools {
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
		public static JsonSerializerSettings jsonSettings = new JsonSerializerSettings() {
			MissingMemberHandling = MissingMemberHandling.Ignore,
			NullValueHandling = NullValueHandling.Ignore
		};
	}
	public static class LOCALE {
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
			"Self Direction",
			"Universalism",
			"Benevolence",
			"Tradition",
			"Security",
			"Power",
			"Achievement",
			"Stimulation",
			"Neutral"
		};
		public static string[] PHIL_ACRONYM = {
			"Slf",
			"Unv",
			"Ben",
			"Trd",
			"Sec",
			"Pow",
			"Ach",
			"Stm",
			"Neu"
		};
	}
	public enum PHIL {
		SELF_DIRECTION,
		UNIVERSALISM,
		BENEVOLENCE,
		TRADITION,
		SECURITY,
		POWER,
		ACHIEVEMENT,
		STIMULATION,
		NEUTRAL,
		COUNT,
	}

	//
	// Generator
	//
	public delegate void AddInitializer(params string[] names);
	public class ThingMaker {
		public Dictionary<string, ExpandoObject> patternCache = new Dictionary<string, ExpandoObject>();
		public Dictionary<string, Action<dynamic>> tagRegistry = new Dictionary<string, Action<dynamic>>();
		public ThingMaker() {
		}
		public void RegisterTags(Dictionary<string, Action<dynamic>> tags) {
			foreach (var tag in tags) {
				tagRegistry.Add(tag.Key, tag.Value);
			}
		}
		public List<T> Create<T>(string tagString, int quantity, ref int id) where T : Thing, new() {
			return Create<T>(tagString.Split(','), quantity, ref id);
		}
		public List<T> Create<T>(string[] tags, int quantity, ref int id) where T : Thing, new() {
			string tagString = String.Join(",", tags);

			// build pattern
			dynamic pattern;
			List<T> things = new List<T>();
			if (patternCache.ContainsKey(tagString)) {
				pattern = patternCache[tagString];
			} else {
				pattern = new ExpandoObject();
				pattern.create = new Dictionary<string, EventHandler>();
				pattern.create.Add("thing", null);
				pattern.AddInitializers = (AddInitializer)((names) => {
					foreach (var name in names){
						pattern.create.Add(name, null);
					}
				});
				foreach (var tag in tags) {
					if (tagRegistry.ContainsKey(tag)) {
						tagRegistry[tag].DynamicInvoke(pattern);
					}
				}
				patternCache.Add(tagString, pattern);
			}

			// make things
			T thing;
			for (int i = 0; i < quantity; i++) {
				thing = new T();
				thing.SetID(id++);
				thing.SetPattern(pattern);
				things.Add(thing);
			}
			return things;
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
		//public Society society;
		public Mind mind;
		public Body body;
		public Social social;
		public Family family;
		public double age;
		//public Outfit outfit = new Outfit();

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
		public double age, density, skinLum, fitness;
		public Color skinColor, hairFrontColor, hairBackColor, eyeColor;
		public string eyeColorText;

		public Body() { }
	}
	public class Mind {
		Person me;
		public int iq, stress, flaw, goal, mood, phil;
		public double confidence, friendMult, education, knowledge, philRadius, philAngle;
		public static double neutralRadius = Math.Sqrt(1.0/((double)PHIL.COUNT-1.0));

		public Mind(Person person) { this.me = person; }
		public double PhilDistance(Person other) {
			return Tools.GetPolarDistance(philRadius, other.mind.philRadius, philAngle, other.mind.philAngle);
		}
		public void SetPhil(double philRadius, double philAngle){
			this.philRadius = philRadius;
			this.philAngle = philAngle;
			if (philRadius < neutralRadius){
				phil = (int)PHIL.NEUTRAL;
			}else{
				phil = (int)Math.Round(philAngle * ((double)PHIL.COUNT-1.0) / (2.0*Math.PI));
				phil %= (int)PHIL.COUNT-1;
			}
			//Console.WriteLine($"philRadius:{philRadius} philAngle:{philAngle} phil:{phil}");
		}
		public void PickPhil(){
			double agePercent = 0.25 - 1 * me.pattern.ages.Percentile(me.body.age);
			double x, y;
			do {
				x = (new UniformP(-1, agePercent, 1)).NextDouble();
				y = (new UniformP(-1, 0, 1)).NextDouble();
			} while (Math.Sqrt(x*x + y*y) > 1);
			Tools.CartesianToPolar(x, y, ref philRadius, ref philAngle);
			if (philRadius < neutralRadius){
				phil = (int)PHIL.NEUTRAL;
			}else{
				phil = (int)Math.Round(philAngle * ((double)PHIL.COUNT-1.0) / (2.0*Math.PI));
				phil %= (int)PHIL.COUNT-1;
			}
			/*
			philAngle = 0.5*Math.PI * Math.Cos(prob.NextDouble()*Math.PI) + 0.5*Math.PI;
			if (globals.random.Next(2) == 1){
				philAngle = 2.0*Math.PI - philAngle;
			}
			*/
		}
		/*
		public int PhilDistance(int philA, int philB = -1) {
			if (philB == -1) philB = phil;
			int distance = Math.Abs(philA - philB) % (int)PHIL.COUNT;
			if (distance > (int)PHIL.COUNT / 2) {
				distance = (int)PHIL.COUNT - distance;
			}
			return distance;
		}
		*/
	}
	public class Family {
		Person me;
		public double socialClass, income, wealth;
		public Family(Person person) { this.me = person; }
	}
	public class Cluster{
		public List<Person> people = new List<Person>();
		public bool political;
		public int[] phil;
		public const int SEARCHING = -2;
		public const int OUTLIER = -1;
		public const int UNCLASSIFIED = 0;
		public Cluster(){ }
		public Cluster(List<Person> people, bool political = true){
			this.people = people;
			this.political = political;
			if (political) phil = new int [(int)PHIL.COUNT];
		}
		public void Add(Person p){
			people.Add(p);
			if (this.political) phil[p.mind.phil]++;
		}
	}
	public class Social {
		Person me;
		public double socialClass, income, wealth;
		public int factionID;
		public int friendGroupID;
		public Dictionary<Person, double> friends = new Dictionary<Person, double>();
		public double maxFriends;
		public Person farFriend;
		public double farFriendDistance = 0;
		public Person bestFriend;
		public int maxCount;
		public Social(Person person) { this.me = person; }
		public Tuple<Person, double> GetBestFriend(List<Person> people) {
			Person maxPerson = null, minPerson = null;
			double minDistance = double.PositiveInfinity;
			double maxDistance = 0;
			double distance;
			foreach (Person other in people) {
				if (other == me) continue;
				distance = Social.GetFriendDistance(me, other);
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
		public static double GetStatDistance(double a, double b, Probability p){
			return Math.Abs(a - b) / p.range;
		}
		public static double GetFriendDistance(Person me, Person other) {
			double distance = 0;
			distance += 1.0 * 4 * Math.Abs(me.body.age - other.body.age) / (me.body.age + other.body.age);
			distance += 1.0 * Math.Abs(me.body.skinLum - other.body.skinLum);
			distance += 1.0 * GetStatDistance(me.body.density, other.body.density, me.pattern.densities);
			distance += 1.0 * GetStatDistance(me.mind.iq, other.mind.iq, me.pattern.iqs);
			distance += 1.0 * (2 - me.mind.confidence - other.mind.confidence);
			distance += 0.5 * me.mind.PhilDistance(other);
			distance /= 5.5 * 0.2;
			return distance;
		}
		public static double GetPoliticalDistance(Person me, Person other) {
			double distance = 0;
			//distance += 1.0 * 4 * Math.Abs(me.body.age - other.body.age) / (me.body.age + other.body.age);
			//distance += 1.0 * Math.Abs(me.body.skinLum - other.body.skinLum);
			//distance += 1.0 * GetStatDistance(me.body.density, other.body.density, me.pattern.densities);
			//distance += 1.0 * GetStatDistance(me.mind.iq, other.mind.iq, me.pattern.iqs);
			//distance += 1.0 * (2 - me.mind.confidence - other.mind.confidence);
			distance += 10.0 * me.mind.PhilDistance(other);
			distance /= 10.0 * 0.1;
			return distance;
		}
		public static List<Cluster> GetClusters(List<Person> people, double epsilon, int minPts, Func<Person, Person, double> distance){
			if (people == null) return null;
			List<Cluster> clusters = new List<Cluster>();
			epsilon *= epsilon;
			int clusterID = 1;
			for (int i = 0; i < people.Count; i++){
				Person p = people[i];
				if (p.social.factionID == Cluster.UNCLASSIFIED){
					if (ExpandCluster(people, p, clusterID, epsilon, minPts, distance)) clusterID++;
				}
			}
			// sort out people into their clusters, if any
			int maxclusterID = people.OrderBy(p => p.social.factionID).Last().social.factionID;
			if (maxclusterID < 1) return clusters; // no clusters, so list is empty
			for (int i = 0; i < maxclusterID; i++) clusters.Add(new Cluster());
			foreach (Person p in people){
				if (p.social.factionID > 0) clusters[p.social.factionID - 1].Add(p);
			}
			return clusters;
		}
		public static List<Person> GetRegion(List<Person> people, Person p, double epsilon, Func<Person, Person, double> distance){
			List<Person> region = new List<Person>();
			for (int i = 0; i < people.Count; i++){
				double distSquared = Math.Pow(distance(p, people[i]), 2);
				if (distSquared <= epsilon) region.Add(people[i]);
			}
			return region;
		}
		public static bool ExpandCluster(List<Person> people, Person p, int clusterID, double epsilon, int minPts, Func<Person, Person, double> distance){
			List<Person> seeds = GetRegion(people, p, epsilon, distance);
			if (seeds.Count < minPts) { // no core person
				p.social.factionID = Cluster.OUTLIER;
				return false;
			}
			else { // all people in seeds are density reachable from person 'p'
				for (int i = 0; i < seeds.Count; i++) seeds[i].social.factionID = clusterID;
				seeds.Remove(p);
				while (seeds.Count > 0){
					Person currentP = seeds[0];
					List<Person> result = GetRegion(people, currentP, epsilon, distance);
					if (result.Count >= minPts){
						for (int i = 0; i < result.Count; i++){
							Person resultP = result[i];
							if (resultP.social.factionID == Cluster.UNCLASSIFIED || resultP.social.factionID == Cluster.OUTLIER){
								if (resultP.social.factionID == Cluster.UNCLASSIFIED) seeds.Add(resultP);
								resultP.social.factionID = clusterID;
							}
						}
					}
					seeds.Remove(currentP);
				}
				return true;
			}
		}
		/*  Find Network OPTION A
		Mark this node as visited
		Search non-visited nodes for 4-(current edges) closest nodes
		Create edges to the closest nodes
		*/
		public static void FindFriendNetwork(List<Person> people){
			var friendNetwork = new List<Person>();
			foreach (var me in people){			
				AddToFriendNetwork(ref friendNetwork, me);
			}
			var sizeRoot = Math.Sqrt(people.Count);
			for (int i=0; i<10; i++){
				for (int personID=0; personID<friendNetwork.Count; personID++){
					var me = friendNetwork[personID];
					AddToFriendNetwork(ref people, me);
					
					var myMaxFriends = 1 + Math.Round(sizeRoot * me.mind.friendMult);
					if (me.social.friends.Count >= myMaxFriends) {
						friendNetwork.Remove(me);
					}
				}
			}
			foreach (var me in friendNetwork){
				var myMaxFriends = 1 + Math.Round(sizeRoot * me.mind.friendMult);
				Console.WriteLine("{0,10} {1}, friends.Count={2}, maxFriends={3}",
					me.firstName, 
					me.lastName.Substring(0,1),
					me.social.friends.Count,
					myMaxFriends
					);
			}
		}
		public static void AddToFriendNetwork(ref List<Person> friendNetwork, Person me, double overfill = 0.0){	
			var sizeRoot = Math.Sqrt(friendNetwork.Count);
			var myMaxFriends = 1 + Math.Round(sizeRoot * me.mind.friendMult);
			/*
			Console.WriteLine("{0} {1}, myMaxFriends={2}, friendNetwork.Count={3}",
				me.firstName, 
				me.lastName.Substring(0,1),
				myMaxFriends,
				friendNetwork.Count
				);
			//*/
			var kickedFriends = new List<Person>();
			foreach (var other in friendNetwork){
				if (me == other) continue;
				if (me.social.friends.ContainsKey(other)) continue;
				var otherMaxFriends = 1 + Math.Round(sizeRoot * other.mind.friendMult);
				var distance = GetFriendDistance(me, other);
				//double iWantFriends = (myMaxFriends - me.social.friends.Count) + me.social.farFriendDistance;
				//double theyWantFriends = (otherMaxFriends - other.social.friends.Count) + other.social.farFriendDistance;
				bool iLikeThem  = me.social.friends.Count < myMaxFriends || distance < me.social.farFriendDistance;
				bool theyLikeMe = other.social.friends.Count < otherMaxFriends || distance < other.social.farFriendDistance;
				
				/*
				Console.WriteLine("{0} {1},{2} {3}, myMax={4}, otherMax={5}, dist={6:n2}, iLikeThem={7}, theyLikeMe={8}, meFarDis={9:n2}, otherFarDis={10:n2}",
					me.firstName, 
					me.lastName.Substring(0,1),
					other.firstName, 
					other.lastName.Substring(0,1),
					myMaxFriends,
					otherMaxFriends,
					distance,
					iLikeThem,
					theyLikeMe,
					me.social.farFriendDistance,
					other.social.farFriendDistance
				);
				//*/
				if (!iLikeThem && !theyLikeMe) continue;
				
				if (iLikeThem && theyLikeMe) {
					if (me.social.friends.Count >= myMaxFriends){
						me.social.RemoveFriend(me.social.farFriend);
					}
					if (other.social.friends.Count >= otherMaxFriends){
						other.social.RemoveFriend(other.social.farFriend);
					}
					me.social.AddFriend(other, distance);
					continue;
				}
				
				/*
				double sum = me.social.friends.Count/myMaxFriends + Math.Pow(other.social.friends.Count/otherMaxFriends, 2) * distance;
				if (sum < overfill){
					me.social.AddFriend(other, distance);
				}
				//*/
				/*
				if (overfill == 0 && ((iLikeThem && me.mind.confidence > 2 * other.mind.confidence)
						|| (theyLikeMe && other.mind.confidence > me.mind.confidence))){
					if (me.social.friends.Count >= myMaxFriends){
						//Console.Write("{0} {1}", me.firstName, me.lastName.Substring(0,1));
						//Console.Write(" removes ");
						//Console.WriteLine("{0} {1}", me.social.farFriend.firstName, me.social.farFriend.lastName.Substring(0,1));
						//kickedFriends.Add(me.social.farFriend);
						me.social.RemoveFriend(me.social.farFriend);
					}
					if (other.social.friends.Count >= otherMaxFriends){
						//Console.Write("{0} {1}", other.firstName, other.lastName.Substring(0,1));
						//Console.Write(" removes ");
						//Console.WriteLine("{0} {1}", other.social.farFriend.firstName, other.social.farFriend.lastName.Substring(0,1));
						//kickedFriends.Add(other.social.farFriend);
						other.social.RemoveFriend(other.social.farFriend);
					}
					
					//Console.WriteLine("Add {0} {1}", other.firstName, other.lastName.Substring(0,1));
					me.social.AddFriend(other, distance);
				}
				//*/
			}
			if (!friendNetwork.Contains(me)) friendNetwork.Add(me);
			/*
			foreach (var person in kickedFriends){
				AddToFriendNetwork(ref friendNetwork, person, false);
			}
			*/
		}
		public void AddFriend(Person other, double distance){
			if (me.social.friends.ContainsKey(other)){
				Console.WriteLine("{0} {1} is already a friend of {2} {3}",
					other.firstName,
					other.lastName.Substring(0,1),
					me.firstName,
					me.lastName.Substring(0,1)
				);
				foreach (var friend in me.social.friends){
					Console.WriteLine("{0} {1} {2}",
						friend.Key.firstName,
						friend.Key.lastName.Substring(0,1),
						friend.Value
					);
				}
				return;
			}
			me.social.friends.Add(other, distance);
			other.social.friends.Add(me, distance);
			//Console.WriteLine("{0} {1:n2} {2:n2}", distance, me.social.farFriendDistance, other.social.farFriendDistance);
			if (distance > me.social.farFriendDistance){
				me.social.farFriend = other;
				me.social.farFriendDistance = distance;
			}
			if (distance > other.social.farFriendDistance){
				other.social.farFriend = me;
				other.social.farFriendDistance = distance;
			}
		}
		public void RemoveFriend(Person other){
			if (!me.social.friends.ContainsKey(other)){
				Console.WriteLine("{0} {1} is not a friend of {2} {3}",
					other.firstName,
					other.lastName.Substring(0,1),
					me.firstName,
					me.lastName.Substring(0,1)
				);
				foreach (var friend in me.social.friends){
					Console.WriteLine("{0} {1} {2}",
						friend.Key.firstName,
						friend.Key.lastName.Substring(0,1),
						friend.Value
					);
				}
				return;
			}
			var distance = me.social.friends[other];
			me.social.friends.Remove(other);
			other.social.friends.Remove(me);
			if (distance == me.social.farFriendDistance){
				me.social.farFriend = null;
				me.social.farFriendDistance = 0;
				foreach (var friend in me.social.friends){
					if (friend.Value > me.social.farFriendDistance){
						me.social.farFriend = friend.Key;
						me.social.farFriendDistance = friend.Value;
					}
				}
				/*
				if (me.social.farFriend != null){
					Console.WriteLine("my new far friend {0} {1} {2:n2}",
						me.social.farFriend.firstName,
						me.social.farFriend.lastName.Substring(0,1),
						me.social.farFriendDistance
					);
				}
				//*/
			}
			if (distance == other.social.farFriendDistance){
				other.social.farFriend = null;
				other.social.farFriendDistance = 0;
				foreach (var friend in other.social.friends){
					if (friend.Value > other.social.farFriendDistance){
						other.social.farFriend = friend.Key;
						other.social.farFriendDistance = friend.Value;
					}
				}
				/*
				if (other.social.farFriend != null){
					Console.WriteLine("ex new far friend {0} {1} {2:n2}",
						other.social.farFriend.firstName,
						other.social.farFriend.lastName.Substring(0,1),
						other.social.farFriendDistance
					);
				}
				//*/
			}
		}
	}



	//
	// Tags
	//
	public static class Human {
		public static string name = "human";
		public static void RegisterTag(dynamic pattern) {
			pattern.ages			= new GaussP(18.0,  30.0, 107.0, 20.0);
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
			pattern.AddInitializers("body", "mind", "family", "social");
			//pattern.CreateThing		+= new EventHandler(OnCreateThing);
			
			pattern.create["body"]	+= new EventHandler(OnCreateBody);
			pattern.create["mind"]	+= new EventHandler(OnCreateMind);
			pattern.create["family"]+= new EventHandler(OnCreateFamily);
			pattern.create["social"]+= new EventHandler(OnCreateSocial);
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
			p.age				= p.body.age;
			p.body.density		= p.pattern.densities.NextDouble();
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
				//Console.WriteLine("Red hair  : {0:n0} {1:n2} {2:n2} {3:n2} {4:n2}", p.age, p.body.skinLum, hue, lum, sat);
			} else {
				// brown hair
				lum = p.pattern.brownHairLums.NextDouble() * p.body.skinLum / 0.9d + 0.05d;
				sat = Tools.Constrain(0.0, 0.25d + 0.75d * Math.Pow(lum, 2) + (-0.05d + 0.1d * globals.random.NextDouble()), 1.0);

				double hueRange = 2.0 + 20.0 * Math.Pow(1.0 - lum, 2);
				hue = 20.0 + 30.0*lum + hueRange*globals.random.NextDouble();
				//Console.WriteLine("Brown hair: {0:n0} {1:n2} {2:n2} {3:n2} {4:n2}", p.age, p.body.skinLum, hue, lum, sat);
			}

			if (p.pattern.canGrey && (p.age > p.pattern.agingEnd || (p.age > p.pattern.agingStart && globals.random.NextDouble() < (p.age - p.pattern.agingStart) / (p.pattern.agingEnd - p.pattern.agingStart)))) {
				// grey hair
				lum = 0.5d + 0.5d * lum;
				sat = p.pattern.greyHairSats.NextDouble();
				//Console.WriteLine("Grey hair : {0:n0} {1:n2} {2:n2} {3:n2} {4:n2}", p.age, p.body.skinLum, hue, lum, sat);
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

			var confidenceProb = new UniformP(0.1, p.body.fitness, 1.0);
			p.mind.confidence = confidenceProb.NextDouble();
			p.mind.confidence += (p.age - ages.min) / (ages.max - ages.min);
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
	}
	public static class American {
		public static string firstName = "american";
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
		public static string firstName = "japanese";
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

	//
	// Test Program
	//
	public class Program {
		public static void PrintClusters(List<Person> people, List<Cluster> clusters){
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
					Console.WriteLine("{0} {1}", LOCALE.PHIL_ACRONYM[philID], clusters[i].phil[philID]);
				}
				Console.WriteLine();
			}
			total = people.Count - total;
			if (total > 0) {
				string plural = (total != 1) ? "people" : "person";
				string verb = (total != 1) ? "are" : "is";
				
				int[] phil = new int [(int)PHIL.COUNT];
				string visual = "";
				foreach (var p in people){
					if (p.social.factionID == Cluster.OUTLIER) {
						phil[p.mind.phil]++;
						visual += "+";
					}
				}
				Console.WriteLine();
				Console.WriteLine(visual);
				Console.WriteLine("{0} {1} {2} outliers.", total, plural, verb);
				for (int philID=0; philID<phil.Length; philID++){
					Console.WriteLine("{0} {1}", LOCALE.PHIL_ACRONYM[philID], phil[philID]);
				}
				Console.WriteLine();
			}
		}
		public static void PrintPoliticalDistanceChart(List<Person> people) {
			Console.WriteLine("=== POLITICAL DISTANCE ===");
			foreach (var person in people) {
				string output = "";
				var bestFriendStats = person.social.GetBestFriend(people);
				var bestFriend = bestFriendStats.Item1;
				var bestFriendDistance = bestFriendStats.Item2;
				output += String.Format(
					"{0} {1}.,{2:n0},{3:n1},{4:n2},{5:n0},{6},{7}",
					person.firstName,
					person.lastName.Substring(0,1),
					person.body.age,
					person.mind.iq,
					person.mind.philRadius,
					person.mind.philAngle*(180/Math.PI),
					LOCALE.PHIL_ACRONYM[(int)person.mind.phil],
					person.social.factionID
					);
				output += String.Format(",{0:n3}", person.mind.PhilDistance(bestFriend));
				output += String.Format(",{0:n3}", bestFriendDistance);
				output += String.Format(
					",{0} {1}.,{2:n0},{3:n1},{4:n2},{5:n0},{6},{7}",
					bestFriend.firstName,
					bestFriend.lastName.Substring(0,1),
					bestFriend.body.age,
					bestFriend.mind.iq,
					bestFriend.mind.philRadius,
					bestFriend.mind.philAngle*(180/Math.PI),
					LOCALE.PHIL_ACRONYM[(int)bestFriend.mind.phil],
					bestFriend.social.factionID
					);
				Console.WriteLine(output);
			}
		}
		public static void PrintSocialDistanceChart(List<Person> people) {
			Console.WriteLine("=== SOCIAL DISTANCE ===");
			foreach (var person in people) {
				string output = "";
				var closestStats = person.social.GetBestFriend(people);
				var closest = closestStats.Item1;
				var closestDistance = closestStats.Item2;
				output += String.Format(
					"{0} {1}.,{2:n0},{3:n2},{4:n0},{5:n2},{6:n2},{7:n2},{8},{9}",
					person.firstName,
					person.lastName.Substring(0,1),
					person.body.age,
					person.body.skinLum,
					person.mind.iq,
					person.body.density,
					person.body.fitness,
					person.mind.confidence,
					LOCALE.PHIL_ACRONYM[(int)person.mind.phil],
					person.social.factionID
					);
				output += String.Format(",{0:n3}", person.mind.PhilDistance(closest));
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
					LOCALE.PHIL_ACRONYM[(int)closest.mind.phil],
					closest.social.factionID
					);
				Console.WriteLine(output);
			}
			Console.Write("\n");
		}
		public static void PrintToneChart(List<Person> people) {
			Console.WriteLine("=== TONES ===");
			foreach (var person in people) {
				Console.WriteLine(
					"{0},{1:n0},{2:n1},{3:n0},{4},{5},{6},{7},{8},{9}",
					person.firstName,
					person.body.age,
					person.body.height * globals.m2ft,
					person.body.weight * globals.kg2lbs,
					person.body.skinColor.R,
					person.body.skinColor.G,
					person.body.skinColor.B,
					person.body.hairFrontColor.R,
					person.body.hairFrontColor.G,
					person.body.hairFrontColor.B
					);
			}
		}
		public static void PrintFriends(List<Person> people){
			Console.WriteLine("=== FRIENDS ===");
			foreach (var person in people) {
				if (person.social.friends.Count > 0){
					double minDistance = double.PositiveInfinity;
					double maxDistance = 0;
					double average = 0;
					foreach (var friend in person.social.friends){
						if (friend.Value < minDistance) minDistance = friend.Value;
						if (friend.Value > maxDistance) maxDistance = friend.Value;
						average += friend.Value;
					}
					average /= person.social.friends.Count;
					Console.Write("{0} {1}, {2:n2}, {3}, {4:n2}, {5:n2}, {6:n2}",
						person.firstName,
						person.lastName.Substring(0,1),
						person.mind.confidence,
						person.social.friends.Count,
						average,
						minDistance,
						maxDistance
					);
				} else {
					Console.Write("{0} {1}, {2:n2}, {3}",
						person.firstName,
						person.lastName.Substring(0,1),
						person.mind.confidence,
						person.social.friends.Count
					);
				}
				Console.Write("\n");
			}
			Console.Write("\n");
		}
		public static int GetFriendGroupID(Person p, List<Person> people){
			if (p.social.friendGroupID == Cluster.UNCLASSIFIED){
				p.social.friendGroupID = Cluster.SEARCHING;
				foreach (var friend in p.social.friends){
					int friendGroupID = GetFriendGroupID(friend.Key, people);
					if (friendGroupID > 0) {
						// found an existing cluster
						p.social.friendGroupID = friendGroupID;
					}
				}
			}
			return p.social.friendGroupID;
		}
		public static void SetFriendGroupID(Person p, List<Person> people, int id){
			p.social.friendGroupID = id;
			foreach (var friend in p.social.friends){
				if (friend.Key.social.friendGroupID != id) {
					SetFriendGroupID(friend.Key, people, id);
				}
			}
		}
		public static void PrintFriendClusters(List<Person> people){
			if (people == null) return;
			int nextClusterID = 1;
			foreach (var p in people){
				if (p.social.friendGroupID == Cluster.UNCLASSIFIED){
					SetFriendGroupID(p, people, nextClusterID++);
				}
			}
			if (nextClusterID == 1) return;
			var clusters = new List<List<Person>>();
			for (int i=1; i<nextClusterID; i++){
				clusters.Add(new List<Person>());
			}
			foreach (var p in people){
				if (p.social.friendGroupID > clusters.Count){
					Console.WriteLine("{0} {1}", p.social.friendGroupID, clusters.Count);
				}
				clusters[p.social.friendGroupID-1].Add(p);
			}
			
			clusters.Sort((a, b) => b.Count - a.Count);
			for (int i=0; i<clusters.Count; i++){
				Console.WriteLine("cluster {0,2} has {1,3} people", i, clusters[i].Count);
			}
			Console.WriteLine("clustering coefficient = {0:n2}", GetClusteringCoefficient(clusters[0]));
		}
		public static double GetClusteringCoefficient(List<Person> people){
			var copy = new Dictionary<Person,double>[people.Count];
			foreach (var p in people){
				copy[p.id] = p.social.friends;
			}
			int totalTriangles = 0;
			Parallel.ForEach(
				copy, () => 0, (mySet, _, __) => {
					int triangles = 0;
					if (mySet.Count > 1) {
						foreach (var neigh1 in mySet) {
							triangles += mySet.Count(neigh2 => neigh1.Key != neigh2.Key && copy[neigh1.Key.id].ContainsKey(neigh2.Key));
						}
					}
					return triangles;
				}, i => Interlocked.Add(ref totalTriangles, i)
			);
			return totalTriangles;
		}
		public static void PrintFriendCount(List<Person> people){
			Console.WriteLine("=== FRIEND COUNT ===");
			var friendCount = new int[25];
			foreach (var person in people) {
				if (friendCount.Length <= person.social.friends.Count + 1) {
					System.Array.Resize(ref friendCount, person.social.friends.Count + 1);
				}
				friendCount[person.social.friends.Count]++;
			}
			for (int i=0; i<friendCount.Length; i++){
				Console.WriteLine("{0},{1}", i, friendCount[i]);
			}
			Console.Write("\n");
		}
		public static void Main(string[] args) {
			var codeCulture = new System.Globalization.CultureInfo("en-US");
			System.Threading.Thread.CurrentThread.CurrentCulture = codeCulture;
			System.Threading.Thread.CurrentThread.CurrentUICulture = codeCulture;
			
			var maker = new ThingMaker();
			maker.RegisterTags(new Dictionary<string, Action<dynamic>>(){{
				Human.name,
				Human.RegisterTag},
				{American.firstName, American.RegisterTag},
				{Japanese.firstName, Japanese.RegisterTag},
			});

			int maxPersonID = 0;
			var people = maker.Create<Person>(new[] { "human", "american" }, 150, ref maxPersonID);
			people.AddRange(maker.Create<Person>(new[] { "human", "japanese" }, 150, ref maxPersonID));
			
			foreach (var person in people) {
				person.Create("body","mind","social");
			}
			
			Social.FindFriendNetwork(people);
			PrintFriendClusters(people);
			//PrintFriendCount(people);
			//PrintFriends(people);
			//PrintSocialDistanceChart(people);
			//PrintPoliticalDistanceChart(people);
			//var clusters = Social.GetClusters(people, 1.4, 4, Social.GetPoliticalDistance);
			//PrintClusters(people, clusters);
			//Console.ReadKey();
		}
	}

	}