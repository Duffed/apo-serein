# Serein 0.1 · Schnellstart

1. Den gesamten ZIP-Inhalt in einen Ordner entpacken und **Serein.exe** starten. Die mitgelieferten DLLs müssen im Ordner bleiben. Eine zusätzliche .NET-Installation ist nicht erforderlich.
2. Über **Importieren … → Datei importieren …** bestehende Peace-/APO-Profile auswählen oder unter **Kopfhörer-Presets** nach einem Modell suchen.
3. Die Vorschau prüfen und **In Bibliothek speichern** wählen. Anschließend lässt sich das Profil links auswählen und bearbeiten.
4. **Peace beenden.** In Serein **APO verbinden / verwalten → Sichern und mit Serein verbinden** wählen. Der einmalige Windows-Administrator-Dialog richtet die Dateieinbindung und Schreibrechte ein. Equalizer APO muss bereits für das gewünschte Wiedergabegerät installiert sein.
5. Das Zielgerät oben auswählen und bei laufender Wiedergabe einen kleinen Preamp-Unterschied testen. Der Status bestätigt nur das Schreiben, nicht die Audiofunktion.

## Im Alltag

- Das **Disketten-Symbol** speichert offene Änderungen sofort. Ohne Änderungen oder bei ungültigen Eingaben ist es deaktiviert. Automatisches Speichern bleibt aktiv; danach wird das Symbol wieder grau. Der Platz für **Preset wiederherstellen** bleibt immer reserviert, damit der Inhalt nicht springt. Die Versionsnummer steht klein neben dem App-Namen.

- **Importieren …** öffnet die Auswahl **Datei importieren …** (APO-/Peace-Dateien) , **APO-Text einfügen …** (kopierte Konfigurations- und Filterzeilen) und **Peace-Profile übernehmen** (vorhandene lokale Profile). Datei- und Textimport führen zur bestehenden Importprüfung und Preset-Vorschau; die Peace-Übernahme zeigt ihren Bericht.
- Erfolgreiches Anwenden wird als kleines grünes Häkchen neben **Speichern** angezeigt. Hover zeigt **Konfiguration angewendet**. Sonstige Meldungen und Fehler bleiben zusätzlich als Text unten sichtbar.

- Eine gemeinsame Tab-Leiste **Grafischer EQ / Parametrische Filter** über dem Graphen steuert gleichzeitig die Kurvenpunkte und die Regler beziehungsweise Filtertabelle darunter. **Importierter EQ** erscheint zusätzlich bei Profilen mit importierter grafischer Stufe; dort zeigen die Regler diese Originalpunkte. Der Wechsel der Ansicht schaltet keine EQ-Stufe aus. Beim Profilwechsel wird zunächst die vorhandene Korrekturstufe angezeigt.

- **Preset wiederherstellen** neben der Quellenzeile setzt die EQ-Werte auf den ursprünglichen Importstand zurück. Der Button erscheint bei Änderungen. Name, Favorit, Quelle und Gerätezuordnung bleiben erhalten. **Rückgängig / Strg+Z** holt die Anpassungen als einen Schritt zurück. Neue Importe speichern ihre Originalwerte lokal; bei älteren Profilen werden sie aus Originaldatei oder AutoEQ-Cache ergänzt. Fehlt ein AutoEQ-Original im Cache, kann der Klick es nachladen; unbekannte oder nicht mehr verfügbare Originale werden ausdrücklich gemeldet. Bereits gespeicherte Originalwerte funktionieren offline.

- Ein ausgewählter Kurvenpunkt zeigt zusätzlich seine **einzelne Filterkurve gestrichelt, ohne Preamp**. Die durchgezogene Linie bleibt die Gesamtkurve. Q-Änderungen sind so direkt als Breitenänderung sichtbar. Bei grafischen Punkten wird der Beitrag des einzelnen Bands gezeigt. Hover zeigt Frequenz, Gain und bei parametrischen Filtern Q sofort am Punkt. Mit Shift erscheint der Hoch-/Runter-Zeiger. Rückgängig-/Wiederholen-Buttons sind nur sichtbar, wenn die jeweilige Aktion verfügbar ist.

- **Rückgängig / Wiederholen** über die Pfeil-Buttons oben oder **Strg+Z / Strg+Y** (auch Strg+Umschalt+Z). Verlauf für EQ-Werte je Profil während der Sitzung, bis zu 100 Schritte. Eine Ziehbewegung oder zusammenhängende Mausradänderung ist ein Schritt. Auch Tabellen-, Preamp- und Bandänderungen werden erfasst; Profilname, Quellen und Gerätezuordnungen bleiben unabhängig. In Textfeldern behält Strg+Z seine normale Textbearbeitungsfunktion.

- Die gemeinsame Tab-Leiste über dem Graphen legt fest, welche Stufe bearbeitet wird: parametrische Filter, importierter grafischer EQ oder persönliche grafische Anpassung. Punkte liegen auf der gesamten Kurve einschließlich Preamp. Parametrische Punkte horizontal für Frequenz und – bei Filtern mit Gain – vertikal verschieben. Feste grafische Bänder lassen sich vertikal verändern; importierte grafische Punkte bleiben beim Verschieben zwischen ihren Nachbarn. **Escape** setzt die laufende Bewegung zurück. Deaktivierte Filter und umgangener EQ sind im Graphen nicht bearbeitbar. Mit **Shift** beim Ziehen bleibt die aktuelle Frequenz fest. Über einem parametrischen Punkt ändert das **Mausrad** den Q-Wert; nach oben erhöht Q (bei einem Glockenfilter wird der Bereich schmaler). Q wird dabei ausdrücklich aktiviert; HP/LP werden bei Bedarf zu HPQ/LPQ. Automatische Preamp-Absenkung bleibt aktiv und beeinflusst weiterhin die Gesamtkurve.

- Unter **Darstellung** links unten zwischen **Hell** und **Dunkel** wechseln. Die Auswahl wird gespeichert. Beide Ansichten verwenden Fluent-Bedienelemente; die Akzentfarbe kommt aus Windows.

- Nach der Verwendung von Peace: Peace beenden, **APO verbinden / verwalten** öffnen und **Neu verbinden und aktuelles Profil laden** wählen. Dabei wird die bisherige Konfiguration gesichert, die Einbindung erneuert und das ausgewählte Serein-Profil aktiviert. Frühere Sicherungen liegen unter `EqualizerAPO\config\Serein\backups`.
- Beim Überfahren einer Profilzeile erscheint rechts ein Papierkorb. Er entfernt genau dieses Profil nach Bestätigung. Ein anderes aktives Profil bleibt ausgewählt; beim Entfernen des aktiven Profils nennt die Bestätigung das anschließend geladene Profil. **Löschen rückgängig** bleibt verfügbar.

- Profile am gepunkteten Griff links in der Zeile ziehen, um ihre Reihenfolge zu ändern. Eine Einfügelinie zeigt die Zielposition; am Listenrand wird automatisch gescrollt. Alternativ das ausgewählte Profil mit **Alt+↑ / Alt+↓** verschieben. Die Reihenfolge wird gespeichert und gilt auch für die Favoriten im Tray. Das Umsortieren aktiviert kein anderes Profil. Bei aktiver Suche wird relativ zum sichtbaren Zielprofil einsortiert.

- **Kompakte Ansicht** blendet die Quellenzeilen in der Profilbibliothek aus. Die Auswahl wird gespeichert.
- Den rechten Rand der linken Seitenleiste ziehen, um sie breiter oder schmaler zu machen; ein Doppelklick setzt die Breite zurück. Die Breite bleibt nach dem Neustart erhalten.
- **Löschen** entfernt das ausgewählte Profil aus Serein. Der Dialog nennt das Profil, das anschließend geladen wird. **Löschen rückgängig** holt die zuletzt entfernten Profile zurück (bis zu 20). Originaldateien bleiben erhalten; entfernte Peace-Profile werden bei der automatischen Übernahme übersprungen. Ein ausdrücklicher Dateiimport ist weiterhin möglich.
- Beide Listen verwenden schmale Scrollleisten passend zur Darstellung und weiches Scrollen mit dem Mausrad. Die Scrollleisten lassen sich weiterhin direkt ziehen.

- Vorhandene lokale Peace-Profile werden beim ersten Start dieser Fassung einmalig übernommen. Bereits übernommene Dateien werden nicht erneut importiert oder überschrieben. **Importieren … → Peace-Profile übernehmen** sucht später nach neuen Dateien und zeigt einen Bericht über nicht unterstützte Profile.
- Unter jedem Profil steht seine Herkunft. Die Quellenzeile unter dem großen Profilnamen öffnet die Profilinformationen; bei Web-Presets lässt sich die Originalquelle öffnen.
- Im Kopfhörerkatalog zeigt die Infospalte die Messquelle, den Messaufbau und veröffentlichte Originalhinweise. Diese werden lokal zwischengespeichert. Eine Zielkurve wird nur bei ausdrücklicher Angabe in der Quelle genannt.
- In der Vorschau speichert **In Bibliothek speichern** ohne Aktivierung. **Speichern und laden** aktiviert das Preset und springt direkt in sein Profil in der Hauptansicht.

- Die Profilwahl links wendet das Profil sofort auf das gewählte Zielgerät an. Sie stellt das Windows-Ausgabegerät nicht um.
- Grafische Regler ergänzen importierte Korrekturen. **Parametrische Filter** zeigt ihre unveränderten Frequenz-, Gain- und Q-Werte; Werte per Doppelklick bearbeiten. Punkt und Komma werden akzeptiert.
- Änderungen speichern sich automatisch; das **Disketten-Symbol** übernimmt offene Eingaben sofort. Ein Stern nimmt das Profil in die Favoriten des Systemtray-Menüs auf.
- Das Fensterkreuz legt Serein im Systemtray ab. Vollständig beenden: Rechtsklick auf das Tray-Symbol → **Beenden**. APO behält dabei den zuletzt geschriebenen EQ.
- Zurück zu Peace: im Einrichtungsdialog **Gesicherte Originalkonfiguration wiederherstellen**, danach Peace wieder öffnen.

## Daten und Grenzen

Profile liegen unter `%LOCALAPPDATA%\Serein`, heruntergeladene Presets im Unterordner `autoeq`. Die mitgelieferte Suche umfasst 6.033 empfohlene AutoEQ-Modelle zum Abrufdatum; eine Aktualisierung erfolgt über **Katalog aktualisieren**. Der Index nennt keine verlässliche Zielkurvenbezeichnung pro Profil; dies wird in der Liste angezeigt.

Beim Wechsel des Windows-Standardgeräts stellt Serein dessen gespeichertes Profil wieder her. Ein neues Standardgerät erhält zunächst ein neutrales Profil. Die Erkennung erfolgt alle zwei Sekunden. Bei manueller Auswahl eines neuen Zielgeräts wird das aktuelle Profil zugeordnet.

Kurve und Headroom sind eine **48-kHz-Vorschau**; die automatische Absenkung ist kein Echtzeitlimiter. Individuelle Kanalbearbeitung, Routing, Surround, VST, Faltung und Mikrofone sind nicht enthalten. Nicht unterstützte Importbefehle werden vollständig abgewiesen; die [Formatbeschreibung](docs/FORMAT.md) nennt die unterstützten Filter.

Die Einrichtung unterstützt eine leere APO-Hauptkonfiguration oder eine reine Peace-Einbindung. Weitere Befehle werden nicht automatisch entfernt. Originalbytes werden in `EqualizerAPO\config\Serein\setup.json` gesichert. Externe Änderungen an der Haupt- oder Serein-Konfiguration halten die Ausgabe an. Peace und Serein deshalb nicht parallel zum Steuern von APO verwenden.

Portable Ausgabe und zusätzlicher Windows-Installer. Keine Codesignatur und kein Autoupdater. Lizenz: [GPL-2.0-or-later](Serein-LICENSE.txt). Die C#-/WPF-Quellen und das reproduzierbare Buildskript werden separat als **Serein-0.1-Quelltext.zip** geliefert. [Drittanbieterhinweise](Serein-THIRD-PARTY.md).










