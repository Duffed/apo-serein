# Unterstützte Importformate

APO-Befehle: `Preamp`, `Filter` (ON/OFF), bis zu zwei `GraphicEQ`-Stufen; `Device: all` und `Channel: all` werden als gemeinsamer Kanalumfang verstanden. Enthält eine Datei zusätzliche Geräte-/Kanalbereiche oder andere Befehle, wird die gesamte Datei zurückgewiesen. Includes werden niemals beim Import verfolgt. Bei mehreren Preamp-Zeilen wird der Wert addiert, entsprechend APO.

`GraphicEQ` bleibt eine eigene Stufe mit allen Punkten. Sie wird nicht auf 10/31 Punkte reduziert. Eine zweite vorhandene `GraphicEQ`-Stufe wird als persönliche Anpassung dargestellt. Die Frequenzpunkte müssen streng aufsteigend sein.

Peace: INI-Abschnitte `General`, `Frequencies`, `Gains`, `Qualities`, `Filters`, `Disabled`. Beschriftungen in `Speakers` und `Sliders` verändern den Klang nicht. Zusätzliche Frequenz-/Q-Tabellen für inaktive Kanäle werden ignoriert, sofern keine Kanal-Gains oder anderen Kanalbefehle vorhanden sind. Nicht-null Kanal-Gains werden abgewiesen.

| Peace-ID | APO-Typ | Q |
|---|---|---|
| 0 / fehlt | PK | explizit |
| 1 | LPQ | explizit |
| 2 | HPQ | explizit |
| 3 | BP | explizit |
| 4 | LS | nicht exportiert; APO-Shelf-Standard S=0,9 |
| 5 | HS | nicht exportiert; APO-Shelf-Standard S=0,9 |
| 6 | NO | explizit |
| 7 | AP | explizit |
| 14 | LSC | explizit; Mittenfrequenz |
| 15 | HSC | explizit; Mittenfrequenz |
| 16 | LS | explizit; Eckfrequenz |
| 17 | HS | explizit; Eckfrequenz |

IDs 8–13 (Slope-/Butterworth-/Linkwitz-Riley-Varianten) werden abgewiesen. `GraphicEQ=1` wird nur bei durchgehend aktiven Peak-Bändern übernommen.

Zahlen in APO-/Peace-Dateien verwenden einen Dezimalpunkt; die Benutzeroberfläche akzeptiert Punkt und Komma. Frequenzen: 1–24000 Hz; Gain und Preamp: ±100 dB; Q: 0,01–100. Die Grafik zeigt 20–20000 Hz bei 48 kHz und ist als Vorschau gekennzeichnet.

JSON-Speicherformat Version 1: Profile besitzen IDs, Namen, Favorit, Preamp, Filterliste, unveränderte grafische Korrektur, zusätzliche grafische Anpassung, Quellenangabe und die optionale Headroom-Einstellung. Gerätezuordnungen referenzieren Profil-IDs. Unbekannte Dateiversionen werden nicht überschrieben.

Gerätezuordnungen speichern die vollständige Windows-MMDevice-ID. Beim Export wird daraus ausschließlich die abschließende Geräte-GUID als `Device:`-Muster verwendet. APO vergleicht dieses Muster mit Gerätename, Anschlussname und GUID; der Windows-Präfix `{0.0.0.00000000}.` gehört nicht in den APO-Filter.
