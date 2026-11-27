# Tosakana – Kinshasa Streets

**Made by Joel Kalala** · Unity 2022.3

*"Tosakana" means "let's play" in Lingala.*

An open-city game set in a small Kinshasa. Walk the streets, take any car, and do delivery jobs across the city
before time runs out. Every job pays money.

![Title screen](Docs/1_title.jpg)

| | |
|---|---|
| ![On foot next to your taxi](Docs/2_on_foot_next_to_taxi.jpg) | ![Driving on Boulevard du 30 Juin](Docs/3_driving_boulevard_du_30_juin.jpg) |
| ![Delivery mission](Docs/5_package_picked_up.jpg) | ![Place du 30 Juin and the DRC flag](Docs/10_place_du_30_juin_flag.jpg) |
| ![Traffic at the Grand Marché](Docs/12_traffic_at_crossroads.jpg) | ![A seller carrying a basin](Docs/13_basin_seller.jpg) |
| ![Police chase with 3 wanted stars](Docs/14_police_chase.jpg) | ![BUSTED](Docs/15_busted.jpg) |
| ![Talking in Lingala in Lemba](Docs/16_talking_in_lingala.jpg) | ![Mont-Ngafula](Docs/18_bumbu_and_mont_ngafula.jpg) |
| ![A roulage officer on Boulevard du 30 Juin](Docs/19_roulage_officer.jpg) | ![They fight back](Docs/21_they_fight_back.jpg) |
| ![N'djili airport terminal](Docs/23_ndjili_terminal.jpg) | ![Flying over Kinshasa](Docs/24_flying_over_kinshasa.jpg) |
| ![The rich sapeur](Docs/25_rich_sapeur.jpg) | ![The pistol](Docs/27_pistol.jpg) |

## Places in the city
- **Place du 30 Juin** – the square with the DRC flag (you start here)
- **Boulevard du 30 Juin** – the big street with palm trees
- **Grand Marché** – the market
- **Gombe** – the tall towers
- **Matonge** – colorful houses
- **Stade des Martyrs** – the stadium
- **Port de Ngobila** – on the Fleuve Congo

## Controls
| Key | What it does |
|---|---|
| W A S D | walk / drive |
| Mouse | look around |
| Shift | run |
| Space | jump / brake |
| E | get in or out of a car |
| H | horn |
| T | talk to someone (in Lingala) |
| Left click / R | punch (or shoot with the pistol) |
| Q | fists / pistol |
| In a plane | W/S speed · A/D turn · ↑ or Space climb · ↓ or C dive |
| Esc | free the mouse (click to take it back) |

## Version 1 has
- Walking and running with a third-person camera
- Cars you can get into and drive (yellow-and-blue taxis too)
- Delivery missions with a timer, money, MISSION PASSED / MISSION FAILED
- A round mini-map that turns with you and shows where to go
- Area names (Gombe, Matonge, ...) at the bottom right
- Sounds: engine, horn, doors, crashes, mission music

## Version 2 adds
- **Traffic:** 18 cars (many yellow-and-blue taxis) drive on the right side of the road, turn at crossroads,
  stop for people and cars in front of them, and honk if you block the road
- **Take any car:** press E next to a traffic car, and the driver gets out and walks away
- **People:** about 45 people walk on the sidewalks. Some wear a pagne and head wrap, and some carry a basin of fruit on their head
- People cross the streets, jump out of the way of fast cars, and if you hit them they fall down and get back up

## Version 3 adds: police and wanted stars ⭐
- **Crimes give you stars (up to 5):** hitting a person with your car, taking a car while the police can see you,
  stealing a police car (+2), or ramming a police car
- **2 police cars patrol the city.** When you are wanted, they turn on red and blue lights and a siren and chase you
  through the streets. More stars bring more police cars.
- **BUSTED:** if the police catch you while you are stopped, you pay a $200 fine, lose your stars, and start again at Place du 30 Juin
- **Escape:** after the police find you, get far away from all of them. The stars blink while they search,
  then you see "YOU LOST THE POLICE"
- Police cars show as red and blue dots on the mini-map

## Version 4 adds: Kinshasa communes and Lingala 🇨🇩
- **A bigger city with 12 communes**, roughly where they really are:
  Gombe, Kintambo and Limete by the river; Lingwala, Ngiri-Ngiri, Bandal, Kalamu, Matonge in the middle;
  Bumbu, Selembao and Lemba further south; Mont-Ngafula with green land and mango trees
- Each commune has its own look (Gombe towers, Bumbu and Selembao small houses with tin roofs, bright Matonge, Limete warehouses)
- Each commune has a sign and a mission place. When you enter one: **"Bokomi malamu na LEMBA"** (Welcome to Lemba)
- **People speak Lingala** in speech bubbles, with the English under each line:
  - Press **T** near someone to talk: *"Mbote!"* → *"Mbote na yo, ndeko!"*
  - People say *"Keba!"* when a car comes, *"Moyibi!"* when you take a car, and *"Ozali liboma?"* if you hit them
  - Drivers shout *"Pesa nzela!"* and the police shout *"Kanga ye!"*
- Missions and big messages have Lingala too (*"Kende kozwa paquet na Gombe"*, *"Matondo!"*, *"Bakangi yo!"*)
- All the Lingala is in one file, `Assets/Scripts/Lingala.cs`, so it is easy to fix or add lines

## Version 5 adds: real voices and roulage 🎙️👮🏾
- **People speak out loud, no text bubbles.** Every Lingala line is spoken with Joel's own recorded voice
  (each person gets a slightly different pitch, so one voice becomes many people)
- **Voice recorder inside Unity:** Tools → Tosakana → Record Voices. 57 lines, with ● Record, ■ Stop and ▶ Listen.
  Lines not recorded yet play a soft murmur that follows the vowels of the words
- **End of a mission:** the customer thanks you and blesses you: *"Matondo mingi! Nzambe apambola yo!"*
- **Roulage officers** stand in the middle of 5 busy crossroads. They whistle, wave, and the traffic stops and goes when they say.
  They shout *"Leka! Leka!"* and *"Telema kuna!"* Hit one and you get 2 wanted stars

## Version 6 adds: fights, a bigger Kinshasa and N'djili airport 🥊
- **Punch** with left click or R. People fall, then run away (*"Bosalisa ngai!"*) or fight back (*"Yaka! Tobunda!"*)
- A **health bar**; if it runs out you are KNOCKED OUT (*"Babeti yo!"*)
- Wanted on foot: **police officers get out of their cars** and run at you (*"Maboko likolo!"*). Three punches knock one down
- The city grows east: **Matete, Masina, N'djili, Kimbanseke, Kingasani** and **Boulevard Lumumba** (17 communes, 845 buildings)
- **Aéroport International de N'djili**: runway, terminal, control tower, planes, and a plane that takes off again and again

## Version 7 adds: fly the planes ✈️
- Press **E** next to a plane at the terminal to get in
- W/S speed, A/D turn, ↑ or Space climb (when fast enough), ↓ or C dive; speed and height on the screen
- Hit a building or the ground too hard: **CRASHED** (*"Avion ekwei!"*)

## Version 8 adds: a rich man with a goal 🕴🏾🎯
- **The goal:** you start with $1,000. Earn **$25,000** to become **"Mokonzi ya Kinshasa"** (King of Kinshasa). Your progress is on the screen
- **A yellow arrow** floats over your head (or your car or plane) and points to your mission
- You are a **Kinshasa sapeur**: bright suit, white trousers, hat, sunglasses, tie, gold chain and gold watch. Your own car is a shiny black luxury car
- **Easier landing:** near the ground the plane levels out by itself, so you can land softly

## Version 9 adds: the pistol 🔫
- **Q** switches between your fists and a pistol; **left click** shoots where the + points
- People you shoot fall and don't get up; gunshots make everyone near run away
- Shooting brings wanted stars (shooting a police officer: +2)

## How to open
Open the folder in Unity Hub (Unity 2022.3). The game is in `Assets/Scenes/Main.unity`. Press ▶ Play.
To rebuild the city: **Tools → Tosakana → Build City**.

## Credits
Game design: Joel Kalala. Built with help from Claude (AI assistant).
