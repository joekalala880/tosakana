using UnityEngine;

// All the Lingala in the game, with the English next to it.
// Joel: if a line is not right, change the Lingala here (the first text in each pair).
// Each line is spoken with YOUR recorded voice: Tools > Tosakana > Record Voices.
// The recording file is named after the Lingala words (for example "mbote_na_yo_ndeko.wav").
public static class Lingala
{
	public struct Line
	{
		public string lingala;
		public string english;
		public Line(string l, string e) { lingala = l; english = e; }

		// The file name for this line's recording: "Mbote na yo, ndeko!" -> "mbote_na_yo_ndeko"
		public string Key
		{
			get
			{
				string t = lingala.ToLowerInvariant().Replace('ï', 'i').Replace('é', 'e').Replace('è', 'e');
				var sb = new System.Text.StringBuilder();
				foreach (char ch in t)
					sb.Append((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') ? ch : '_');
				string k = sb.ToString();
				while (k.Contains("__")) k = k.Replace("__", "_");
				return k.Trim('_');
			}
		}
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

	// Roulage (traffic police) at the crossroads
	public static readonly Line[] Roulage =
	{
		L("Leka! Leka!", "Go! Go!"),
		L("Telema kuna!", "Stop there!"),
		L("Kende malembe!", "Go slowly!"),
		L("Zela!", "Wait!"),
		L("Pesa ngai permis!", "Give me your license!"),
	};

	// The customer when you pick up the package
	public static readonly Line[] Pickup =
	{
		L("Mema yango noki!", "Carry it quickly!"),
		L("Keba na paquet na ngai!", "Be careful with my package!"),
	};

	// The end of a mission: thanks and a blessing
	public static readonly Line[] Blessings =
	{
		L("Matondo mingi! Nzambe apambola yo!", "Thank you very much! God bless you!"),
		L("Nzambe apambola yo, ndeko!", "God bless you, my friend!"),
		L("Matondo! Nzambe azala na yo!", "Thank you! God be with you!"),
	};

	// Welcome to each commune (only plays if you recorded it)
	public static readonly string[] CommuneNames = { "Gombe", "Matonge", "Lemba", "Ngiri-Ngiri", "Bandal", "Bumbu", "Selembao", "Mont-Ngafula", "Kintambo", "Limete", "Kalamu", "Lingwala" };
	public static Line WelcomeTo(string commune) { return L("Bokomi malamu na " + commune + "!", "Welcome to " + commune + "!"); }

	// Big messages
	public static readonly Line Welcome = L("Bokomi malamu na", "Welcome to");
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

	// Every line someone says out loud, in groups (for the voice recorder). Same words = one recording.
	public static System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, Line>> AllSpoken()
	{
		var list = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, Line>>();
		var seen = new System.Collections.Generic.HashSet<string>();
		System.Action<string, Line[]> add = (group, lines) =>
		{
			foreach (Line l in lines)
				if (seen.Add(l.Key))
					list.Add(new System.Collections.Generic.KeyValuePair<string, Line>(group, l));
		};
		add("You (press T)", PlayerSays);
		for (int i = 0; i < Answers.Length; i++)
			add("People answer", Answers[i]);
		add("Sellers", SellerSays);
		add("People say hello", Greetings);
		add("Watch out!", WatchOut);
		add("Hit by a car", Hit);
		add("You took their car", Thief);
		add("Drivers", Honk);
		add("Police", Police);
		add("Roulage", Roulage);
		add("Customer (pick up)", Pickup);
		add("Mission passed (blessing)", Blessings);
		add("Messages", new[] { TooSlow, Busted, Escaped });
		var welcomes = new Line[CommuneNames.Length];
		for (int i = 0; i < CommuneNames.Length; i++)
			welcomes[i] = WelcomeTo(CommuneNames[i]);
		add("Welcome to the communes", welcomes);
		return list;
	}
}
