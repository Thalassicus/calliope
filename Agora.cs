
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Threading;

//
// Toolbox
//
public class Logger {
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

public class UILabel<T> {
	public int posX, posY;
	public string format;
	
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

public class Colony {
	private static int labelWidth = 15;
	
	private UILabel<string> landLabel;	
	private UILabel<string> capitalLabel;
	private UILabel<string> jobsLabel;
	
	private UILabel<int> forestLabel;
	private int forestData;
	public int forest {
		get {return forestData;} 
		set {forestData = value; forestLabel.Update(value);}
	}
	
	private UILabel<int> fieldsLabel;
	private int fieldsData;
	public int fields {
		get {return fieldsData;} 
		set {fieldsData = value; fieldsLabel.Update(value);}
	}
	
	private UILabel<int> peopleLabel;
	private int peopleData;
	public int people {
		get {return peopleData;} 
		set {peopleData = value; peopleLabel.Update(value);}
	}
	
	private UILabel<int> foodLabel;
	private int foodData;
	public int food {
		get {return foodData;} 
		set {foodData = value; foodLabel.Update(value);}
	}
	
	private UILabel<int> farmsLabel;
	private int farmsData;
	public int farms {
		get {return farmsData;} 
		set {farmsData = value; farmsLabel.Update(value);}
	}
	
	private UILabel<int> huntersLabel;
	private int huntersData;
	public int hunters {
		get {return huntersData;} 
		set {huntersData = value; huntersLabel.Update(value);}
	}
	
	private UILabel<int> farmersLabel;
	private int farmersData;
	public int farmers {
		get {return farmersData;} 
		set {farmersData = value; farmersLabel.Update(value);}
	}
	
	public Colony(){
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
	}
}

//
// Test Program
//

public class Program {
	public static void Main(string[] args) {
		var log = new Logger(Logger.TRACE);
		var city = new Colony();
		city.people = 10;
		city.forest = 20;
		city.fields = 50;
		city.food = 0;
		city.hunters = 0;
		city.farmers = 0;
		city.farms = 0;
		
		Console.ReadKey();
		Console.ResetColor();
		Console.Clear();
	}
}