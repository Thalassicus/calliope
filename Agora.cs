
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