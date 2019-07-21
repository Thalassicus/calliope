
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Threading;

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

class ShuffleContainer <T> {
	Dictionary<T, double> persistantWeights;
	Dictionary<T, Dictionary<string, double>> baseWeights;
	double totalBaseWeight;
	
	public ShuffleContainer(Dictionary<T, Dictionary<string, double>> baseWeights){
		this.baseWeights = baseWeights;
		foreach (var weight in baseWeights.Values){
			totalBaseWeight += weight["base"];
		}
	}
	
	T GetNext(double continueOdds, params string[] tags){
		var adjustedWeights = persistantWeights;
		foreach (var key in persistantWeights.Keys){
			foreach (var tag in tags) {
				if (baseWeights[key].ContainsKey(tag)){
					adjustedWeights[key] *= baseWeights[key][tag];
				}
			}
		}
		var resultKey = GetRandomWeighted<T>(adjustedWeights);
		var resultNewWeight = continueOdds * persistantWeights[resultKey];
		var distributeWeight = 1 - resultNewWeight;
		foreach (var pair in persistantWeights){
			if (pair.Key == resultKey){
				pair.Value = resultNewWeight;
			} else {
				pair.Value += distributeWeight * baseWeights[pair.Key]["base"] / totalBaseWeight;
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

		double randomTotalWeight = globals.random.NextDouble() * totalWeight;
		foreach (var weight in totalWeights) {
			if (weight.Value >= randomTotalWeight) {
				return weight.Key;
			}
		}
		return default(S);
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
		Console.Title = "Agora";
		Console.CursorVisible = false;
		Console.SetWindowSize(100,30);
		Console.SetBufferSize(100,30);
		Console.ForegroundColor = ConsoleColor.Black;
		Console.BackgroundColor = ConsoleColor.White;
		Console.Clear();
	}
	
	static void Main(string[] args) {
		InitConsole();
		var baseWeights = new Dictionary<string, Dictionary<string, double>>();
		
		baseWeights["drizzle"] = new Dictionary<string, double>(){
			{"base", 2},
		};
		baseWeights["downpour"] = new Dictionary<string, double>(){
			{"base", 2},
			{"highwind", 2},
			{"lightning", 2},
		};
		var options = new ShuffleContainer<string>(baseWeights);
			
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