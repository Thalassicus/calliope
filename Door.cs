
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Threading;
using Newtonsoft.Json;

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
	
	public UILabel(int posX, int posY, string format, T output = default){
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
class Colony {
	static int labelWidth = 15;
	
	UILabel<string> landLabel;
	
	public int forest	{ get {return forestData;}	set {forestData = value;	forestLabel.Update(value);} }	UILabel<int> forestLabel;	int forestData;
	
	public Colony(){
		int y = 1;
		landLabel		= new UILabel<string> (0, y, "{0, "+labelWidth+"}", "Land │ ");
		forestLabel		= new UILabel<int> (1*labelWidth, y, "Forest: {0,-5}", 0);
	}
}

/*
Target end-user syntax
Goal: {text.weather}

"en_US": {
  "windStrength": {
    "strong": {
      "text": "Strong",
      "tagWeights": {"intense": 8}
    },
    "light": {
      "text": "Light",
      "tagWeights": {"intense": 0}
    },
  },
  "wind": {
    "text": "{windStrength} winds push at you from the {cardinalDirection}.",
    "tagWeights": {
      "base": 2,
      "start": 8,
    }
  },
}

 */

public class ShuffleObject<T> {
	public string key;
	public string text;
	public Dictionary<string, double> tagWeights;

	public override string ToString() {
		return text;
	}
	public ShuffleObject(string key, string text, Dictionary<string, double> tagWeights) {
		this.key = key;
		this.text = text;
		this.tagWeights = new Dictionary<string, double>() {
			{"base", 2}
		};
		foreach (var tag in tagWeights) {
			this.tagWeights[tag.Key] = tag.Value;
		}
		this.tagWeights = tagWeights;
	}
}
public static class weather {
	public static ShuffleObject<string> drizzle = new ShuffleObject<string>(
		"drizzle",
		"A light drizzle drifts down from the sky.",
		new Dictionary<string, double>(){
			{"base", 4},
			{"end", 8},
		}
	);
	public static ShuffleObject<string> downpour = new ShuffleObject<string>(
		"downpour",
		"A heavy downpour drenches the landscape.",
		new Dictionary<string, double>(){
			{"start", 0.2},
			{"end", 0.2},
			{"intense", 8},
		}
	);
	public static ShuffleObject<string> lightning = new ShuffleObject<string>(
		"lightning",
		"Lightning flashes, and thunder rolls through the air.",
		new Dictionary<string, double>(){
			{"start", 8},
			{"intense", 8},
		}
	);
	public static ShuffleObject<string> wind = new ShuffleObject<string>(
		"wind",
		"Heavy winds buffet you about.",
		new Dictionary<string, double>(){
			{"start", 8},
		}
	);
}

class ShuffleContainer <T> {
	private readonly StoryState story;
	public Dictionary<T, double> persistantWeights = new Dictionary<T, double>();
	Dictionary<T, Dictionary<string, double>> baseWeights;
	double totalBaseWeight;
	
	public ShuffleContainer(StoryState story, Dictionary<T, Dictionary<string, double>> baseWeights){
		this.story = story;
		this.baseWeights = baseWeights;
		foreach (var pair in baseWeights){
			persistantWeights[pair.Key] = pair.Value["base"];
			totalBaseWeight += pair.Value["base"];
		}
	}
	
	public override string ToString() {
		return GetNext().ToString();
	}
	public T GetNext(){
		var adjustedWeights = new Dictionary<T, double>(persistantWeights);
		foreach (var key in persistantWeights.Keys){
			foreach (var tag in story.tags) {
				if (baseWeights[key].ContainsKey(tag)){
					adjustedWeights[key] *= baseWeights[key][tag];
				}
			}
		}
		var resultKey = GetRandomWeighted<T>(adjustedWeights);
		var resultNewWeight = story.repeatOddspersistantWeights[resultKey];
		var distributeWeight = (1-story.repeatOdds)persistantWeights[resultKey];
		foreach (var pair in adjustedWeights){
			if (EqualityComparer<T>.Default.Equals(pair.Key, resultKey)){
				persistantWeights[pair.Key] = resultNewWeight;
			} else {
				persistantWeights[pair.Key] += distributeWeightbaseWeights[pair.Key]["base"] / (totalBaseWeight - baseWeights[resultKey]["base"]);
			}
		}
		return resultKey;
	}
	
	public static S GetRandomWeighted<S>(Dictionary<S, double> weights) {
		var totalWeights = new Dictionary<S, double>();

		double totalWeight = 0.0;
		foreach (var weight in weights) {
			if (weight.Value <= 0) continue;
			totalWeight += weight.Value;
			totalWeights.Add(weight.Key, totalWeight);
		}

		double randomTotalWeight = globals.random.NextDouble()totalWeight;
		foreach (var weight in totalWeights) {
			if (weight.Value >= randomTotalWeight) {
				return weight.Key;
			}
		}
		return default;
	}
}

class StoryState{
	public double repeatOdds;
	public HashSet<string> tags;
	
	public StoryState(){
		tags = new HashSet<string>();
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
		var weather = new ShuffleContainer<string>(story, baseWeights);
		
		story.tags.Add("start");
		story.repeatOdds = 0;
		Console.WriteLine("The storm approaches.");
		Console.WriteLine(weather);
		story.tags.Add("intense");
		Console.WriteLine("The storm intensifies!");
		Console.WriteLine(weather);
		story.tags.Remove("start");
		Console.WriteLine("The storm arrives.");
		story.repeatOdds = 0.75;
		Console.WriteLine(weather);
		Console.WriteLine(weather);
		Console.WriteLine(weather);
		story.tags.Remove("intense");
		Console.WriteLine($"The storm calms, producing {weather}, {weather}, {weather}, {weather}, and {weather}.");
		Console.WriteLine($"More {weather}, {weather}, {weather}, {weather}, and {weather}.");
		story.tags.Add("intense");
		Console.WriteLine("The storm intensifies!");
		Console.WriteLine(weather);
		Console.WriteLine(weather);
		Console.WriteLine(weather);
		story.repeatOdds = 0;
		Console.WriteLine(weather);
		story.tags.Remove("intense");
		story.tags.Add("end");
		Console.WriteLine("The storm calms.");
		Console.WriteLine(weather);
		Console.WriteLine(weather);
		Console.WriteLine("The storm ends.");

		//Console.WriteLine("The storm calms, producing " + storm +", "+ storm +", "+ storm +", "+ storm +", and "+ storm + ".");
		//Console.WriteLine("More " + storm +", "+ storm +", "+ storm +", "+ storm +", and "+ storm + ".");
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