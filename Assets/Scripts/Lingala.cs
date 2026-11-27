using UnityEngine;

// All the Lingala in the game, with the English next to it.
// Joel: if a line is not right, change the Lingala here (the first text in each pair). Nothing else needs to change.
public static class Lingala
{
	public struct Line
	{
		public string lingala;
		public string english;
		public Line(string l, string e) { lingala = l; english = e; }
	}

	static Line L(string lingala, string english) { return new Line(lingala, english); }

	// What you say when you press T near someone (one after another)
	public static readonly Line[] PlayerSays =
	{
		L("Mbote!", "Hello!"),
		L("Sango nini?", "What's new?"),
		L("Okei wapi?", "Where are you going?"),
		L("Matondo, kende malamu!", "Thank you, go well!"),
	};

	// How people answer each of your lines (same order as PlayerSays)
	public static readonly Line[][] Answers =
	{
		new[] { L("Mbote na yo, ndeko!", "Hello to you, my friend!"), L("Mbote! Ozali malamu?", "Hello! Are you well?"), L("Mbote mingi!", "A big hello!") },
		new[] { L("Sango te, nazali malamu.", "No news, I'm fine."), L("Makambo ezali malamu!", "Things are good!"), L("Mokolo ya lelo ezali molunge!", "Today is hot!") },
		new[] { L("Nakei na zando.", "I'm going to the market."), L("Nakei na ndako.", "I'm going home."), L("Nakei na mosala.", "I'm going to work."), L("Nakei kotala baninga.", "I'm going to see friends.") },
		new[] { L("Tikala malamu!", "Stay well!"), L("Kende malamu, ndeko!", "Go well, my friend!"), L("Nzambe apambola yo!", "God bless you!") },
	};

	// A seller carrying a basin always answers like a seller
	public static readonly Line[] SellerSays =
	{
		L("Somba mbuma na ngai!", "Buy my fruit!"),
		L("Mbuma ya sika, somba!", "Fresh fruit, buy!"),
	};

	// People say hello when you walk past
	public static readonly Line[] Greetings =
	{
		L("Mbote!", "Hello!"),
		L("Sango nini, ndeko?", "What's new, friend?"),
		L("Boni?", "How are you?"),
	};

	// A fast car comes at them
	public static readonly Line[] WatchOut =
	{
		L("Keba!", "Watch out!"),
		L("Keba na motuka!", "Watch out for the car!"),
		L("Tala!", "Look!"),
	};

	// A car hits them
	public static readonly Line[] Hit =
	{
		L("Mama na ngai!", "Oh my mother!"),
		L("Ozali liboma?", "Are you crazy?"),
		L("Aïe! Keba!", "Ouch! Be careful!"),
	};

	// You took their car
	public static readonly Line[] Thief =
	{
		L("Moyibi! Moyibi!", "Thief! Thief!"),
		L("Motuka na ngai!", "My car!"),
	};

	// A car driver waiting behind you
	public static readonly Line[] Honk =
	{
		L("Longwa na nzela!", "Get out of the road!"),
		L("Pesa nzela!", "Make way!"),
	};

	// The police chasing you
	public static readonly Line[] Police =
	{
		L("Telema!", "Stop!"),
		L("Kanga ye!", "Catch him!"),
		L("Polisi! Telema!", "Police! Stop!"),
	};

	// Big messages
	public static readonly Line Welcome = L("Boyei malamu na", "Welcome to");
	public static readonly Line Passed = L("Matondo!", "Thank you!");
	public static readonly Line TooSlow = L("Malembe mingi!", "Too slow!");
	public static readonly Line Busted = L("Bakangi yo!", "They caught you!");
	public static readonly Line Escaped = L("Okimi bapolisi!", "You escaped the police!");
	public static readonly Line GetInCar = L("Kota na motuka", "Get in the car");
	public static readonly Line Talk = L("Loba", "Talk");

	// Mission instructions
	public static string GoGet(string place) { return "Kende kozwa paquet na " + place + "."; }   // Go get a package at ...
	public static string Carry(string place) { return "Mema yango na " + place + " noki!"; }        // Carry it to ... quickly!

	public static Line Pick(Line[] lines) { return lines[Random.Range(0, lines.Length)]; }
}
