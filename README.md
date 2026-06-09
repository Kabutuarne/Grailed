
# Grailed — Roguelike izdzīvošanas spēle

Grailed ir pirmās personas roguelike izdzīvošanas spēle, kuras pamatā ir izpēte, taktiska cīņa un riska lēmumi. Katrs reids ģenerē jaunu vidi ar pretiniekiem un resursiem. Ja spēlētājs mirst, visi līdzi paņemtie priekšmeti tiek nomesti, un līmeņa laiks.

---

## Projekta uzstādīšana

- Lai uzsāktu projektu, lejupielādē jaunāko "Release" versiju.
- Atarhīvē failu
- Atver folderi un klikšķini uz "Grailed.exe"
- Izbaudi spēli
<img width="787" height="252" alt="image" src="https://github.com/user-attachments/assets/2efa0179-35a6-4a29-806e-7252890c9c06" />
<img width="679" height="383" alt="image" src="https://github.com/user-attachments/assets/3c083b66-aa22-4c5e-906f-0ce4aa8ad83c" />

## Galvenās iespējas

### Procedurāli ģenerēti līmeņi

Katrs reids izveido unikālu karti ar atšķirīgu istabu izkārtojumu, pretiniekiem un priekšmetiem.

### Atribūtu sistēma

Spēlētājs sākumā sadala 25 brivos punktus starp:

- Agility
- Strength
- Intelligence
- Stamina
- (Turot kursoru virs viena no virsrakstiem, var iepazīties ar šī atribūta ietekmi uz spēli)
<img width="683" height="383" alt="image" src="https://github.com/user-attachments/assets/07f44e7a-7204-48da-9a83-95cb62346674" />

Atribūti ietekmē kustību, burvestību ātrumu, resursu atjaunošanos un maksimālos HP/Mana/Energy.

### Inventāra sistēma

Slotu balstīts inventārs:

- 8 mugursomas sloti
- 1 rokas slots
- 3 aksesuāru sloti

Priekšmetu tipi: patēriņa, dekoratīvie, zižļi, burvestības, aksesuāri.

### Burvestības un cīņa

Burvestības var izmantot tieši vai ievietot zižļos. Burvestību veidi:

- Projectile -- ietekmēs tikai entītijas, kurām trāpīs ar šāviņu
- AOE -- ietekmēs visas entītijas noteiktajā rādiusā
------------------------------------
- Simple -- tiks izsaukta tikai vienreiz, pēc izsaukšanas pabeigšanas
- Channeled -- tiks izsaukta katru kadru, turot, pēc izsaukšanas pabeigšanas
------------------------------------
Līmeņa interjera eksplorācijas piemērs:
<img width="298" height="168" alt="download (1)" src="https://github.com/user-attachments/assets/cefedb22-bc99-4946-98ee-5226d284c125" />




