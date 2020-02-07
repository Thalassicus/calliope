
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Threading;
using Newtonsoft.Json;

namespace Rextester{
//
// Toolbox
//
class Logger {
	public static int NONE  = 0;
	public static int ERROR = 1;
	public static int WARN  = 2;
	public static int INFO  = 3;
	public static int TRACE = 4;
	public int level;
	public Logger(int level) {
		this.level = level;
	}
	public void Error(string message, params object[] list) {
		if (level >= ERROR) Console.WriteLine("ERROR: " + message, list);
	}
	public void Warn(string message, params object[] list) {
		if (level >= WARN)  Console.WriteLine("WARN:  " + message, list);
	}
	public void Info(string message, params object[] list) {
		if (level >= INFO)  Console.WriteLine("INFO:  " + message, list);
	}
	public void Trace(string message, params object[] list) {
		if (level >= TRACE) Console.WriteLine("TRACE: " + message, list);
	}
}

static class Tools{
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
		return default;
	}
}
class UILabel<T> {
	int posX, posY;
	string format;
	
	public UILabel(int posX, int posY, string format, T output = default(T)){
		this.posX = posX;
		this.posY = posY;
		this.format = format;
		Update(output);
	}
	public void Update(T output){
		Console.CursorLeft = posX;
		Console.CursorTop = posY;
		Console.Write(format, output);
	}
}

public static class globals {
	public static Random random = new Random();
	public static double m2ft = 0.0328;
	public static double kg2lbs = 2.2;
}

public class ShuffleText {
	private readonly StoryState story;
	private string text;
	public double RepeatChance {get; set;}
	private Dictionary<string, double> statusWeights;
	private Dictionary<string, bool> anyStatus;
	private Dictionary<string, bool> allStatus;
	private Dictionary<string, bool> setStatus;
	private Dictionary<string, ShuffleText<string>> options;
	private Dictionary<string, double> persistantWeights = new Dictionary<string, double>();
	private Dictionary<string, Dictionary<string, double>> baseWeights;
	private double totalBaseWeight;

	public ShuffleText(
			StoryState story,
			string text = null,
			double repeatChance = 1.0,
			Dictionary<string, double> statusWeights = null,
			Dictionary<string, bool> anyStatus = null,
			Dictionary<string, bool> allStatus = null,
			Dictionary<string, bool> setStatus = null,
			Dictionary<string, ShuffleText<string>> options = null){
		this.story = story;
		this.text = text ?? "";
		this.RepeatChance = repeatChance;
		this.statusWeights = statusWeights ?? new Dictionary<string, double>();
		if (!this.statusWeights.ContainsKey("base")) this.statusWeights.Add("base", 1);
		this.anyStatus = anyStatus ?? new Dictionary<string, bool>();
		this.allStatus = allStatus ?? new Dictionary<string, bool>();
		this.setStatus = setStatus ?? new Dictionary<string, bool>();
		this.options = options ?? new Dictionary<string, ShuffleText<string>>();
		foreach (var option in options.Keys){
			double baseValue = options[option].statusWeights["base"];
			baseWeights.Add(option, baseValue);
			persistantWeights.Add(option, baseValue);
			totalBaseWeight += baseValue;
		}
	}

	public double GetWeight(){
		foreach (var condition in allStatus){
			if (story.conditions[condition.Key] != condition.Value) return 0;
		}

		bool anyConditionGood = (anyStatus.Count == 0);
		foreach (var condition in anyStatus){
			if (story.conditions[condition.Key] == condition.Value) anyConditionGood = true;
		}
		if (!anyConditionGood) return 0;

		double result = statusWeights["base"];
		foreach (var condition in statusWeights){
			if (story.conditions[condition.Key] == true){
				result *= statusWeights[condition.key];
			}
		}
		return result;
	}

    public override string ToString() {
		string result = GetNextOption();
		foreach (var condition in setStatus){
			story.conditions[condition.Key] = condition.Value;
		}
		return result;
	}

	private string GetNextOption(){
		if (options.Count == 0) return text;
		
		var currentWeights = new Dictionary<string, double>();
		foreach (var key in persistantWeights.Keys){
			var weight = persistantWeights[key].GetWeight();
			if (weight > 0) currentWeights.Add(key, weight);
		}
		var resultKey = Tools.GetRandomWeighted<string>(currentWeights);
		var resultNewWeight = story.repeatOdds * persistantWeights[resultKey];
		var distributeWeight = (1-story.repeatOdds) * persistantWeights[resultKey];
		foreach (var pair in currentWeights){
			if (EqualityComparer<string>.Default.Equals(pair.Key, resultKey)){
				persistantWeights[pair.Key] = resultNewWeight;
			} else {
				persistantWeights[pair.Key] += distributeWeight * baseWeights[pair.Key]["base"] / (totalBaseWeight - baseWeights[resultKey]["base"]);
			}
		}
		return resultKey;
	}
}

class StoryState{
	public double repeatOdds;
	public HashSet<string> tags;
	public Dictionary<string, bool> conditions;
	public Dictionary<string, ShuffleContainer<string>> shuffle;
	public Dictionary<string, ShuffleContainer<string>> buttons;
	public static JsonSerializerSettings jsonSettings = new JsonSerializerSettings() {
		MissingMemberHandling = MissingMemberHandling.Ignore,
		NullValueHandling = NullValueHandling.Ignore
	};
	
	public StoryState(){
		tags = new HashSet<string>();
		LoadJson(this);
	}

	public static void LoadJson(StoryState me, string path = "en_US.json") {
		if (!FileStyleUriParser.Exists(path)){
			Console.WriteLine("File does not exist:" + path);
			return;
		}
		string readText = File.ReadAllText(path);
		JsonConvert.PopulateObject(readText, me);
	}
	
	public void AddTags(params string[] tagsToAdd){
		foreach (var tag in tagsToAdd){
			tags.Add(tag);
		}
	}
	
	public void RemoveTags(params string[] tagsToRemove){
		foreach (var tag in tagsToRemove){
			tags.Remove(tag);
		}
	}
}

//
// Test Program
//

class Program {
	static object lockInput = new object();
	static object lockQuit = new object();
	static ConsoleKeyInfo userInput;
	static bool quitNow = false;
	static Logger log = new Logger(Logger.TRACE);
	
	static void InitConsole(){
		Console.Title = "Door";
		Console.CursorVisible = false;
		Console.SetWindowSize(100,30);
		Console.SetBufferSize(100,30);
		/*
		Console.ForegroundColor = ConsoleColor.Black;
		Console.BackgroundColor = ConsoleColor.White;
		Console.Clear();
		*/
	}
	
	static void Main(string[] args) {
		InitConsole();

		var story = new StoryState();		
		
		
		
		/*
		Thread inputThread = new Thread(new ThreadStart(InputThreadFunc));
		Thread tickThread  = new Thread(new ThreadStart(TickThreadFunc));
		inputThread.Start();
		Thread.Sleep(1);
		tickThread.Start();
		
		while (true){
			Thread.Sleep(100);
			lock (lockQuit){
				if (quitNow){
					tickThread.Abort();
					inputThread.Abort();
					Thread.Sleep(1);
					Console.ResetColor();
					Console.Clear();
					Console.SetCursorPosition(1, 0);
					Console.WriteLine("Quitting program...");
					break;
				}
			}
		}
		*/
		Console.CursorVisible = true;
		Console.ReadKey();
	}

	static void TickThreadFunc() {
		while (true) {
			Thread.Sleep(250);
		}
	}

	static void InputThreadFunc() {
		while (true) {
			var myInput = Console.ReadKey(true);
			lock (lockInput) {
				userInput = myInput;
			}
			if (myInput.Key == ConsoleKey.Escape) lock (lockQuit){
				quitNow = true;
			}
		}
	}
}
}


//
// Toolbox
//
class Logger {
	public static int NONE  = 0;
	public static int ERROR = 1;
	public static int WARN  = 2;
	public static int INFO  = 3;
	public static int TRACE = 4;
	public int level;
	public Logger(int level) {
		this.level = level;
	}
	public void Error(string message, params object[] list) {
		if (level >= ERROR) Console.WriteLine("ERROR: " + message, list);
	}
	public void Warn(string message, params object[] list) {
		if (level >= WARN)  Console.WriteLine("WARN:  " + message, list);
	}
	public void Info(string message, params object[] list) {
		if (level >= INFO)  Console.WriteLine("INFO:  " + message, list);
	}
	public void Trace(string message, params object[] list) {
		if (level >= TRACE) Console.WriteLine("TRACE: " + message, list);
	}
}

class UILabel<T> {
	int posX, posY;
	string format;
	
	public UILabel(int posX, int posY, string format, T output = default(T)){
		this.posX = posX;
		this.posY = posY;
		this.format = format;
		Update(output);
	}
	public void Update(T output){
		Console.CursorLeft = posX;
		Console.CursorTop = posY;
		Console.Write(format, output);
	}
}

class Colony {
	static int labelWidth = 15;
	
	UILabel<string> landLabel;	
	UILabel<string> capitalLabel;
	UILabel<string> jobsLabel;
	UILabel<string> progressLabel;
	
	public int forest	{ get {return forestData;}	set {forestData = value;	forestLabel.Update(value);} }	UILabel<int> forestLabel;	int forestData;
	public int fields	{ get {return fieldsData;}	set {fieldsData = value;	fieldsLabel.Update(value);} }	UILabel<int> fieldsLabel;	int fieldsData;
	public int people	{ get {return peopleData;}	set {peopleData = value;	peopleLabel.Update(value);} }	UILabel<int> peopleLabel;	int peopleData;
	public int food		{ get {return foodData;}	set {foodData = value;		foodLabel.Update(value);} }		UILabel<int> foodLabel;		int foodData;
	public int farms	{ get {return farmsData;}	set {farmsData = value;		farmsLabel.Update(value);} }	UILabel<int> farmsLabel;	int farmsData;
	public int hunters	{ get {return huntersData;} set {huntersData = value;	huntersLabel.Update(value);} }	UILabel<int> huntersLabel;	int huntersData;
	public int farmers	{ get {return farmersData;} set {farmersData = value;	farmersLabel.Update(value);} }	UILabel<int> farmersLabel;	int farmersData;
	
	public double acresPerEater		{ get {return acresPerEaterData;}	set {acresPerEaterData = value;		acresPerEaterLabel.Update(value);} }	UILabel<double> acresPerEaterLabel;		double acresPerEaterData;
	public double acresPerFarmer	{ get {return acresPerFarmerData;}	set {acresPerFarmerData = value;	acresPerFarmerLabel.Update(value);} }	UILabel<double> acresPerFarmerLabel;	double acresPerFarmerData;
	
	public Colony(){
		Console.Title = "Agora";
		Console.CursorVisible = false;
		Console.SetWindowSize(100,30);
		Console.SetBufferSize(100,30);
		Console.ForegroundColor = ConsoleColor.Black;
		Console.BackgroundColor = ConsoleColor.White;
		Console.Clear();
		int y = 1;
		landLabel		= new UILabel<string> (0, y, "{0, "+labelWidth+"}", "Land │ ");
		forestLabel		= new UILabel<int> (1*labelWidth, y, "Forest: {0,-5}", 0);
		fieldsLabel		= new UILabel<int> (2*labelWidth, y, "Fields: {0,-5}", 0);
		
		y = 2;
		capitalLabel	= new UILabel<string> (0, y, "{0, "+labelWidth+"}", "Capital │ ");
		peopleLabel 	= new UILabel<int> (1*labelWidth, y, "People: {0,-5}", 0);
		farmsLabel 		= new UILabel<int> (2*labelWidth, y, "Farms: {0,-5}", 0);
		foodLabel 		= new UILabel<int> (3*labelWidth, y, "Food: {0,-5}", 0);
		
		y = 3;
		jobsLabel 		= new UILabel<string> (0, y, "{0, "+labelWidth+"}", "Jobs │ ");
		huntersLabel 	= new UILabel<int> (1*labelWidth, y, "Hunters: {0,-5}", 0);
		farmersLabel 	= new UILabel<int> (2*labelWidth, y, "Farmers: {0,-5}", 0);
		
		y = 4;
		progressLabel		= new UILabel<string> (0, y, "{0, "+labelWidth+"}", "Progress │ ");
		acresPerEaterLabel 	= new UILabel<double> (1*labelWidth, y, "Acres/Eater: {0,-5}", 0);
		acresPerFarmerLabel = new UILabel<double> (2*labelWidth, y, "Acres/Farmer: {0,-5}", 0);
	}
}

class ShuffleContainer <T> {
	Dictionary<T, double> weights;
	
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
}

//
// Test Program
//

class Program {
	static object lockInput = new object();
	static object lockQuit = new object();
	static int i = 0;
	static ConsoleKeyInfo userInput;
	static bool quitNow = false;
	static Logger log = new Logger(Logger.TRACE);
	static Colony city = new Colony();

	static void TickThreadFunc() {
		while (true) {
			Thread.Sleep(250);
		}
	}
	
	static void Main(string[] args) {
		city.people = 10;
		city.forest = 20;
		city.fields = 50;
		city.hunters = city.people;
		city.food = city.hunters;
		city.farmers = 0;
		city.farms = 0;
		
		Thread inputThread = new Thread(new ThreadStart(InputThreadFunc));
		Thread tickThread  = new Thread(new ThreadStart(TickThreadFunc));
		inputThread.Start();
		Thread.Sleep(1);
		tickThread.Start();
		
		while (true){
			Thread.Sleep(100);
			lock (lockQuit){
				if (quitNow){
					tickThread.Abort();
					inputThread.Abort();
					Thread.Sleep(1);
					Console.ResetColor();
					Console.Clear();
					Console.SetCursorPosition(1, 0);
					Console.WriteLine("Quitting program...");
					break;
				}
			}
		}
		Console.CursorVisible = true;
	}

	static void InputThreadFunc() {
		while (true) {
			var myInput = Console.ReadKey(true);
			lock (lockInput) {
				userInput = myInput;
			}
			if (myInput.Key == ConsoleKey.Escape) lock (lockQuit){
				quitNow = true;
			}
		}
	}
}