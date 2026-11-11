# Serein 0.1

Kompakte Windows-Oberfläche für Equalizer APO. C# / .NET 10, WPF, Windows x64. GPL-2.0-or-later; siehe LICENSE.txt und THIRD-PARTY.md.

## Windows-Installer

Installer und portable ZIP gibt es unter [GitHub Releases](https://github.com/Duffed/apo-serein/releases).

Die Datei **Serein-Setup-Windows-x64.exe** installiert Serein für den aktuellen Benutzer unter %LOCALAPPDATA%\Programs\Serein, legt eine Startmenü-Verknüpfung und einen Windows-Deinstallationseintrag an. Die .NET-Laufzeit ist enthalten. Equalizer APO wird auf dem Zielrechner separat eingerichtet. Profile unter %LOCALAPPDATA%\Serein bleiben bei Installation und Deinstallation erhalten.

Installer bauen: Nach dem Publish ./build-installer.ps1 -Compiler <Pfad-zu-ISCC.exe> -PublishDirectory <portabler-Ordner> ausführen. Benötigt Inno Setup 7; Skript unter installer/Serein.iss. Der Compiler gehört nicht zum Anwendungs-Quelltext.

## Start

Die portable Ausgabe befindet sich im separaten Windows-ZIP (aktuell Ordner `Serein-Profilliste/`). `Serein.exe` starten; eine separate .NET-Installation ist nicht nötig. Equalizer APO muss bereits installiert und für die gewünschten Wiedergabegeräte eingerichtet sein.

Die App startet zunächst als Vorschau. Erst **APO verbinden / verwalten → Sichern und mit Serein verbinden** aktiviert die Anbindung. Peace vorher beenden. Der Windows-Dialog für Administratorrechte gehört zur einmaligen Einrichtung. Die Oberfläche läuft anschließend ohne erhöhte Rechte.

Für neu angeschlossene Ausgabegeräte öffnet **APO verbinden / verwalten → APO-Geräte einrichten …** die Geräteauswahl von Equalizer APO. Wähle dort das Gerät aus und bestätige.

## Funktionen

- Fluent-Oberfläche mit gespeicherter heller und dunkler Darstellung; Auswahl links unten.

- Profile, Favoriten im Systemtray, Gerätezuordnungen und Wiederherstellung beim Wechsel des Windows-Standardausgabegeräts.
- Grafische persönliche Anpassung mit 10/31 Bändern zusätzlich zu unveränderten importierten Korrekturen.
- Parametrische Filter: PK, LS, HS, LSC, HSC, LP, HP, LPQ, HPQ, NO, AP, BP; einzelne Filter deaktivierbar.
- Preamp, geschätzter Headroom und optionale automatische Absenkung mit 0,2 dB Rechenreserve.
- Import von APO-Text und Peace-Dateien; Vorschau vor Speicherung, anschließende Aktivierung per Profilwahl.
- AutoEQ-Suche mit lokal mitgeliefertem Index; Download, Vorschau und Offlinecache.
- Sicherung und bytegenaue Wiederherstellung der bisherigen APO-Hauptkonfiguration.

## Bedienung

1. **Importieren …** öffnet zunächst den vorhandenen APO-Konfigurationsordner. Mehrere `.peace`- oder `.txt`-Dateien können nacheinander geprüft und gespeichert werden.
2. In der Vorschau **In Bibliothek speichern** wählen. Das laufende Profil bleibt ausgewählt.
3. Links ein gespeichertes Profil auswählen, um es auf das oben gewählte Zielgerät anzuwenden. Die Gerätewahl stellt nicht das Windows-Standardgerät um.
4. In **Parametrische Filter** Werte per Doppelklick bearbeiten. Punkt und Komma werden als Dezimaltrennzeichen akzeptiert. `Q nutzen` unterscheidet explizite Q-Werte vom APO-Standard, insbesondere bei Shelves.
5. Der grafische EQ ist eine zusätzliche persönliche Anpassung. Das Wechseln zwischen 10 und 31 Bändern setzt nur diese Anpassung zurück; vorhandene importierte Filter bleiben erhalten.
6. Das Schließen des Fensters minimiert in den Systemtray. Über dessen Menü lässt sich die App vollständig beenden. Der zuletzt geschriebene EQ bleibt in APO wirksam, auch wenn Serein beendet wird.

Änderungen werden automatisch nach kurzer Pause gespeichert und angewendet. **Speichern** übernimmt Eingaben sofort. Bei einer ungültigen Filtereingabe bleibt die letzte gültige Konfiguration erhalten.

## Daten und Einrichtung

Benutzerdaten: `%LOCALAPPDATA%\Serein\profiles.json`, Katalog und heruntergeladene Presets unter `%LOCALAPPDATA%\Serein\autoeq`. `SEREIN_DATA` kann für isolierte Tests einen anderen Datenordner festlegen.

Die Einrichtung unterstützt eine leere Hauptkonfiguration oder eine reine `Include: peace.txt`-Einbindung mit Kommentaren. Weitere APO-Befehle werden nicht ungeprüft entfernt. Serein ersetzt die Einbindung durch `Include: Serein/active.txt`. Die exakten Originalbytes liegen Base64-codiert in `config\Serein\setup.json`. Schreibrechte werden ausschließlich für den angemeldeten Benutzer und diesen Unterordner vergeben.

Externe Änderungen an `config.txt` oder `Serein/active.txt` halten weitere Ausgaben an. Peace nicht parallel zum Steuern von APO verwenden. Bei einem Konflikt zuerst beide Konfigurationen prüfen; Serein überschreibt den Konflikt nicht automatisch. Die normale Wiederherstellung steht im Einrichtungsdialog bereit und verändert eine extern geänderte Hauptdatei nicht.

Bei einem Gerätewechsel wird das zuletzt zugeordnete Profil wiederhergestellt. Für ein erstmals erkanntes Windows-Standardgerät wird ein neutrales Profil angelegt. Bei manueller Auswahl eines noch nicht zugeordneten Zielgeräts wird das aktuelle Profil zugeordnet. Mehrere Kopfhörer am selben X7-Ausgang bleiben manuell auswählbare Profile.

## Grenzen dieser Ausgabe

- Kanalrouting, individuelle Kanal-EQs, Surround, VST, Faltung und Mikrofone sind ausgeschlossen. Nicht unterstützte Importbefehle werden mit einer Fehlermeldung vollständig abgewiesen.
- Peace-Filter mit eigener Steilheit/Ordnung (IDs 8–13) sind noch nicht importierbar; sie werden nicht angenähert. Unterstützte IDs und Verhalten stehen in `docs/FORMAT.md`.
- Kurve und Headroom sind eine Vorschau für **48 kHz**. Grafische EQ-Stufen werden logarithmisch interpoliert; tatsächliche APO-Verarbeitung und andere Abtastraten können abweichen. Automatische Absenkung ist eine Schätzung und kein Echtzeitlimiter.
- Die Ergebnisliste von AutoEQ nennt nicht für jedes Preset eine Zielkurve. Die App kennzeichnet dies ausdrücklich, statt eine Zielkurve zu erfinden. Enthalten sind die empfohlenen AutoEQ-Ergebnisse, nicht sämtliche historischen Messvarianten oder OPRA.
- Geräteeinbau/-ausbau und Änderungen des Standardgeräts werden alle zwei Sekunden geprüft. Eine bereits geschriebene gerätespezifische Konfiguration bleibt bis zum nächsten Wechsel bestehen. Vorherige Geräte erhalten dadurch nicht gleichzeitig weitere aktive Profile.
- Der Status bestätigt das Schreiben der Konfiguration, nicht die hörbare Anwendung durch APO. APO-Installation und Audiofunktion müssen mit Wiedergabe geprüft werden.
- Der Windows-Installer ist unsigniert. Version 0.1 enthält keinen Autoupdater und keinen automatischen Windows-Start.

## Bauen und testen

Voraussetzung: Windows und .NET-10-SDK.

```powershell
./build.ps1
```

Das Skript erstellt die portable Anwendung und führt die Tests aus. `./build.ps1 -OnlineTests` prüft zusätzlich einen echten AutoEQ-Download. Build und Restore laden nur offizielle .NET-Laufzeitpakete; die Anwendung selbst benötigt keine NuGet-Drittbibliotheken.

`Serein.exe --smoke` prüft die Darstellung isoliert, misst Start und Profilwechsel und erzeugt eine PNG-Vorschau. Dieser Modus schreibt keine APO-Konfiguration und keine normalen Benutzerdaten. Für den Test werden gerenderte App-Steuerelemente verwendet, nicht der Audiotreiber.



