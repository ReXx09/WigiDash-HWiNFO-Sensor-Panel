# HWiNFO Sensor Panel für WigiDash

Grafisches WigiDash-Widget für CPU-, GPU-, RAM-, VRAM-, Lüfter- und Netzwerkdaten. Das Widget unterstützt alle Rastergrößen von 1x1 bis 5x4. Außerhalb des WigiDash Managers wird eine animierte Demoquelle verwendet.

## Funktionen

- CPU- und GPU-Anzeige mit Last- und Temperatur-Gauges
- RAM-, VRAM-, Lüfter- und Netzwerk-Karten
- HWiNFO-Sensoren im Einstellungsmenü frei zuweisbar
- konfigurierbare Gauge-Farben und Warnschwellen
- Uhrzeit, Zeitzone, Uhrfarbe und Uhrgröße
- Touchaktionen für Header und externe WigiDash-Aktionen
- Logo und Autorenhinweis `by ReXx09`

## Voraussetzungen

- Windows mit installiertem WigiDash Manager
- .NET Framework 4.7.2 Developer Pack oder Visual Studio 2022 mit entsprechender Zielplattform
- HWiNFO, wenn echte HWiNFO-Messwerte verwendet werden sollen
- Zugriff auf die WigiDash-SDK-Datei `WigiDashWidgetFramework.dll`

Standardpfad der SDK-Datei:

`C:\Program Files (x86)\G.SKILL\WigiDash Manager\WigiDashWidgetFramework.dll`

## Bauen und installieren

PowerShell im Projektordner öffnen und zuerst den Debug- oder Release-Build ausführen:

```powershell
dotnet build .\HwinfoSensorPanel.csproj -c Release
```

Der Release-Build kopiert automatisch diese Dateien in den WigiDash-Widgetordner:

- die Widget-DLL `B4C9D6B1-3C75-4C27-8E8F-1D2DA1B8A4D3.dll`
- das Logo `IMG_0382.ico`

Standardziel:

`%APPDATA%\G.SKILL\WigiDashManager\Widgets\B4C9D6B1-3C75-4C27-8E8F-1D2DA1B8A4D3`

Falls der WigiDash Manager an einem anderen Ort installiert ist:

```powershell
dotnet build .\HwinfoSensorPanel.csproj -c Release `
	-p:WigiDashManagerPath="D:\Programme\WigiDash Manager"
```

Falls ein anderer Widget-Zielordner verwendet werden soll:

```powershell
dotnet build .\HwinfoSensorPanel.csproj -c Release `
	-p:WigiDashUserWidgetsPath="D:\WigiDashWidgets"
```

Nach dem Build den WigiDash Manager neu starten oder seine Widget-Liste aktualisieren. Ist der Manager während des Builds geöffnet und sperrt die DLL, ihn schließen und den Release-Build erneut starten.

## Installation prüfen

Nach einem erfolgreichen Release-Build müssen beide Dateien vorhanden sein:

```powershell
$target = "$env:APPDATA\G.SKILL\WigiDashManager\Widgets\B4C9D6B1-3C75-4C27-8E8F-1D2DA1B8A4D3"
Test-Path "$target\B4C9D6B1-3C75-4C27-8E8F-1D2DA1B8A4D3.dll"
Test-Path "$target\IMG_0382.ico"
```

Beide Befehle müssen `True` ausgeben. Anschließend das Widget im Manager hinzufügen oder eine vorhandene Instanz neu laden.

## Sensoren verwenden

Im Einstellungsmenü des Widgets lassen sich die passenden CPU-, GPU-, RAM-, Lüfter- und Netzwerksensoren auswählen. Für echte Werte müssen HWiNFO und die Sensorintegration des WigiDash Managers aktiv sein. Ohne Manager beziehungsweise ohne verfügbare Sensorquelle zeigt das Widget Demo-Werte.

## Demo-Animation

![Animierte Vorschau des 5x1-Widgets](docs/demo-animation.gif)

Die Demoquelle [`DemoSensorSource`](DemoSensorSource.cs) erzeugt kontinuierlich wechselnde Last-, Temperatur-, Takt-, Leistungs- und Lüfterwerte. Sie wird für Vorschauen und außerhalb des laufenden WigiDash Managers verwendet.

## Unterstützen

Die Weiterentwicklung kann über [GitHub Sponsors](https://github.com/sponsors/ReXx09) unterstützt werden.