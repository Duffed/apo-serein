# Herkunft und Lizenzen

Serein wird unter GPL-2.0-or-later bereitgestellt. Der vollständige Quelltext gehört zur Lieferung.

- Equalizer APO: Jonas Thedering, GPL-2.0-or-later. Die mathematischen Biquad-Koeffizienten und Shelf-Frequenzkonventionen wurden mit `filters/BiQuad.cpp`, `BiQuadFilter.cpp` und `BiQuadFilterFactory.cpp` abgeglichen. https://sourceforge.net/projects/equalizerapo/ und Referenzspiegel https://github.com/PureLin/EqualizerAPO
- AutoEQ: Jaakko Pasanen und Mitwirkende, MIT. Der mitgelieferte empfohlene Ergebnisindex stammt von https://github.com/jaakkopasanen/AutoEq/blob/master/results/README.md (Abruf 01.11.2026). Lizenztext: `data/AutoEq-LICENSE.txt`. Messquellen werden pro Ergebnis angezeigt. Die Quellenangabe eines heruntergeladenen Profils bleibt gespeichert.
- Peace: Peter Verbeek, GPLv2. Peace wird weder mitgeliefert noch ausgeführt. Das INI-Dateiformat wird unabhängig gelesen. Filter-IDs wurden mit lokalen Peace-Profilen, der aktiven APO-Ausgabe und der dokumentierten Formatzuordnung des Konverters https://github.com/m1lhaus/convert-peace-to-easyeffects abgeglichen. Der offizielle Peace-Quelltext ließ sich beim Build nicht als prüfsummenidentische Datei abrufen; nicht sicher unterstützte Varianten werden abgewiesen.
- .NET / WPF / Windows Forms: Microsoft und Mitwirkende. Die portable Ausgabe enthält die offiziellen Laufzeitdateien einschließlich deren `LICENSE.txt` und `ThirdPartyNotices.txt`.

Es wurden keine Peace-Grafiken, Peace-Binärdateien oder persönlichen Profile in die portable Ausgabe übernommen. Das Serein-Symbol wurde aus einfachen geometrischen Formen erstellt.
