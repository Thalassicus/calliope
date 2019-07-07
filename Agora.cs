
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;

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

public class HUD {
	public Dictionary<string, TextBox> labels = new Dictionary<string, TextBox>();
	public TextBox forest, fields, food, people, gatherers, farmers, farms;
	
	public HUD(){
		Console.CursorVisible = false;
		Console.SetWindowSize(100,30);
		Console.SetBufferSize(100,30);
		Console.ForegroundColor = ConsoleColor.Black;
		Console.BackgroundColor = ConsoleColor.White;
		Console.Clear();
		
		int width = 14;
		int height = 7;
		int x, y;
		
		// Natural resources
		x = 2;
		y = 1;
		forest = new TextBox("Forest", x, y, width, height);
		forest.Draw();
		x += width + 4;
		fields = new TextBox("Fields", x, y, width, height);
		fields.Draw();
		x += width + 4;
		food = new TextBox("Food", x, y, width, height);
		food.Draw();
		x += width + 4;
		
		// People
		x = 2;
		y += height+1;
		
		people = new TextBox("People", x, y, width, height);
		people.Draw();
		x += width + 4;
		
		gatherers = new TextBox("Gatherers", x, y, width, height);
		gatherers.Draw();
		x+= width + 4;
		
		farmers = new TextBox("Farmers", x, y, width, height);
		farmers.Draw();
		
		// Buildings
		x = 2;
		y += height+1;
		farms = new TextBox("Farms", x, y, width, height);
		farms.Draw();
		
	}
}

public class TextBox {
	public int posX, posY, sizeX, sizeY;
    public ConsoleColor borderColor;
	public string label, data;
	
	public TextBox(string label, int posX, int posY, int sizeX=8, int sizeY=7) {
		this.posX = posX;
		this.posY = posY;
		this.sizeX = sizeX;
		this.sizeY = sizeY;
		//this.borderColor = borderColor;
		this.label = label;
	}
	
    public void Draw(bool visible=true) {
		string topLine = "╔";
		string midLine = "║";
		string barLine = "║";
		string botLine = "╚";
		
        for (int i = 0; i < sizeX; i++) {
            topLine += "═";
            midLine += " ";
            barLine += "─";
            botLine += "═";
        }
		topLine += "╗";
		midLine += "║";
		barLine += "║";
		botLine += "╝";
		
        //Console.ForegroundColor = borderColor;
		for (int y=0; y<sizeY; y++) {
			Console.CursorLeft = posX;
			Console.CursorTop = posY + y;
			if (y == 0) {
				Console.Write(topLine);
			} else if (y == sizeY-3) {
				Console.Write(barLine);
			} else if (y < sizeY-1) {
				Console.Write(midLine);
			} else {
				Console.Write(botLine);
			}
		}
		Console.CursorTop = posY + sizeY - 2;
		Console.CursorLeft = posX+1 + (sizeX - label.Length)/2;
		Console.Write(label);
        //Console.ResetColor();
    }
	
	public void SetData(long data) {
		string output = String.Format("{0}", data);
		Console.CursorLeft = posX+1 + (sizeX - output.Length)/2;
		Console.CursorTop = posY+2;
		Console.Write(output);
	}
}

//
// Test Program
//

public class Program {
	public static void Main(string[] args) {
		var log = new Logger(Logger.TRACE);
		var hud = new HUD();
		Console.ReadKey();
		int forest = 20;
		int fields = 50;
		int food = 0;
		int people = 10;
		int gatherers = 0;
		int farmers = 0;
		int farms = 0;
		
		hud.forest.SetData(forest);
		hud.fields.SetData(fields);
		hud.food.SetData(food);
		hud.people.SetData(people);
		hud.gatherers.SetData(gatherers);
		hud.farmers.SetData(farmers);
		hud.farms.SetData(farms);
		
		Console.ReadKey();
		Console.ResetColor();
		Console.Clear();
	}
}