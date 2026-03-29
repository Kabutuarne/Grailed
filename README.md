# Grailed — README

## English

<details>
<summary><strong>Expand</strong></summary>

# Grailed — Roguelike Survival Game

Grailed is a first‑person roguelike survival game focused on exploration, tactical combat, and high‑risk decision‑making. Each expedition generates a new environment filled with enemies, traps, and valuable resources. If the player dies, all carried items are lost, making every choice meaningful.

---

## Core Features

### Procedural Level Generation

Every run creates a new dungeon layout with different room combinations, enemy placements, and loot distribution.

### Attribute‑Driven Gameplay

Players distribute starting points into four attributes:

- Agility
- Strength
- Intelligence
- Stamina

These attributes influence movement speed, spellcasting time, resource regeneration, and maximum HP/Mana/Energy.

### Inventory System

A slot‑based inventory with:

- 9 backpack slots
- 1 hand slot
- 4 accessory slots

Items include consumables, decorative objects, staffs, spells, and accessories.

### Magic and Combat

Players can cast spells directly or embed them into staffs. Spell types include:

- Projectile
- Area‑of‑Effect
- Simple (instant)
- Channeled

Movement, timing, and resource management are essential for survival.

### Base Building

Decorative items found during expeditions can be placed in the player’s base. The base acts as a hub for preparation and progression.

---

## Technical Overview

- Engine: Unity
- Language: C#
- 3D Modeling: Blender
- Project Structure: Modular, component‑based

The game runs fully offline and stores all data locally.

---

## Save System

Each save file contains:

- Player attributes
- Inventory and equipped items
- Base layout
- Progression state
- Unique world ID and creation date

Saves can be created, loaded, and deleted from the main menu.

---

## Getting Started

1. Clone the repository
2. Open the project in Unity
3. Load the **MainMenu** scene
4. Press Play

</details>

---

## Latviešu

<details>
<summary><strong>Atvērt</strong></summary>

# Grailed — Roguelike izdzīvošanas spēle

Grailed ir pirmās personas roguelike izdzīvošanas spēle, kuras pamatā ir izpēte, taktiska cīņa un augsta riska lēmumi. Katrs reids ģenerē jaunu vidi ar pretiniekiem, lamatu mehānismiem un resursiem. Ja spēlētājs mirst, visi līdzi paņemtie priekšmeti tiek zaudēti.

---

## Galvenās iespējas

### Procedurāli ģenerēti līmeņi

Katrs reids izveido unikālu karti ar atšķirīgu istabu izkārtojumu, pretiniekiem un priekšmetiem.

### Atribūtu sistēma

Spēlētājs sākumā sadala punktus starp:

- Agility
- Strength
- Intelligence
- Stamina

Atribūti ietekmē kustību, burvestību ātrumu, resursu atjaunošanos un maksimālos HP/Mana/Energy.

### Inventāra sistēma

Slotu balstīts inventārs:

- 9 mugursomas sloti
- 1 rokas slots
- 4 aksesuāru sloti

Priekšmetu tipi: patēriņa, dekoratīvie, zižļi, burvestības, aksesuāri.

### Burvestības un cīņa

Burvestības var izmantot tieši vai ievietot zižļos. Pieejami:

- Projectile
- AOE
- Simple
- Channeled

Cīņa balstās uz kustību, laika izvēli un resursu pārvaldību.

### Bāzes veidošana

Bāzi var dekorēt ar priekšmetiem, kas iegūti izpētes laikā. Tā kalpo kā sagatavošanās un progresijas centrs.

---

## Tehniskais pārskats

- Dzinis: Unity
- Valoda: C#
- 3D modelēšana: Blender
- Moduļu arhitektūra

Spēle darbojas pilnībā bez interneta un glabā visus datus lokāli.

---

## Saglabājumu sistēma

Saglabājums satur:

- Spēlētāja atribūtus
- Inventāru un aprīkojumu
- Bāzes izkārtojumu
- Progresu
- Unikālu pasaules ID un izveides datumu

Saglabājumus var izveidot, ielādēt un dzēst no galvenās izvēlnes.

---

## Darba uzsākšana

1. Klonē repozitoriju
2. Atver projektu Unity vidē
3. Ielādē **MainMenu** ainu
4. Spied Play

</details>
