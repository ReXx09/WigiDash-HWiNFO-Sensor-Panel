# HWiNFO Sensor Panel für WigiDash

Dieses Projekt ist ein grafisches 5x4-WigiDash-Widget. Die Oberfläche wird vollständig als Bitmap gezeichnet und ist deshalb frei gestaltbar: Anzeigen, Rahmen, Logos, Balken und Texte sind nicht auf die normalen WigiDash-Schaltflächen beschränkt.

## Aktueller Stand

- grafisches CPU-/GPU-Panel im Stil des Referenzbildes
- RAM-, FPS- und Statusbereiche
- animierte Demo-Werte zum Testen des Layouts
- echte WigiDash-Sensorquelle über `GetSensorList()` und `SensorUpdated`
- auswählbare CPU-, GPU- und RAM-Sensoren im Bearbeitungsmenü
- Demo-Fallback außerhalb des WigiDash Managers

## Bauen

Voraussetzungen:

- Visual Studio 2022 mit .NET Framework 4.7.2 Developer Pack
- WigiDash Manager installiert
- HWiNFO für den späteren Shared-Memory-Zugriff

Der Standardpfad für die SDK-DLL ist:

`C:\Program Files (x86)\G.SKILL\WigiDash Manager\WigiDashWidgetFramework.dll`

Falls der Manager an einem anderen Ort installiert ist, kann der Pfad beim Build gesetzt werden:

```powershell
dotnet build .\HwinfoSensorPanel.csproj -p:WigiDashManagerPath="D:\Programme\WigiDash Manager"
```

Bei einem Release-Build wird die DLL automatisch nach

`%APPDATA%\G.SKILL\WigiDashManager\Widgets\B4C9D6B1-3C75-4C27-8E8F-1D2DA1B8A4D3`

kopiert. Der Zielpfad kann überschrieben werden:

```powershell
dotnet build .\HwinfoSensorPanel.csproj -c Release -p:WigiDashUserWidgetsPath="D:\WigiDashWidgets"
```

Ist der WigiDash Manager während des Builds geöffnet, kann die DLL gesperrt sein. Dann den Manager schließen und den Release-Build erneut starten.

## Nächster Schritt

Im WigiDash Manager werden die Sensoren aus der SDK-Sensorliste bezogen. HWiNFO muss dafür laufen und seine Sensorintegration im Manager verfügbar sein. CPU-Last, CPU-Temperatur, GPU-Last, GPU-Temperatur und RAM-Last können im Bearbeitungsmenü ausgewählt werden. FPS bleibt eine separate Anzeige und benötigt später eine eigene Quelle.