# Wake on LAN

Aplicación moderna para Windows 11 que enciende los equipos de tu red con **Wake-on-LAN**.
Inspirada en WakeMeOnLan, pero con interfaz Fluent (Mica, tema oscuro, iconos de Windows 11).

## Funcionalidades

- **Escanear la red**: encuentra equipos automáticamente (ARP + ping + DNS), incluso los que tienen el ping bloqueado.
- **Lista de dispositivos** con estado en vivo (en línea, apagado, encendiendo) y "visto por última vez".
- **Encender** uno, varios (selección múltiple) o todos.
- **Añadir / editar / eliminar** dispositivos; obtener la MAC automáticamente a partir de la IP.
- Envío por **todas las interfaces de red** (broadcast general y de subred), o a una dirección fija
  (útil para despertar equipos desde fuera con reenvío de puertos).
- **Importar / exportar** la lista en JSON.
- **Línea de comandos** para accesos directos o tareas programadas:

  ```
  WakeOnLan.exe /wake "PC del salón"
  WakeOnLan.exe /wake AA:BB:CC:DD:EE:FF 192.168.1.30
  WakeOnLan.exe /wakeall
  ```

Atajos: `Ctrl+N` añadir · `Ctrl+F` buscar · `F5` actualizar · `Supr` eliminar · doble clic para editar.

Los datos se guardan en `%AppData%\WakeOnLan\devices.json`.

## Instalación

Descarga desde [Releases](../../releases):

- `WakeOnLan-x.y.z-setup.exe`: instalador. No pide permisos de administrador; crea un acceso directo en el menú Inicio y un desinstalador.
- `WakeOnLan-x.y.z-portable.exe`: un único ejecutable que no necesita instalación.

No hace falta tener .NET instalado.

## Requisitos del equipo que quieres encender

1. Wake-on-LAN activado en la **BIOS/UEFI**.
2. En Windows, en las propiedades del adaptador de red:
   - *Administración de energía* → "Permitir que este dispositivo reactive el equipo".
   - *Opciones avanzadas* → "Wake on Magic Packet" = Habilitado.
3. Recomendado: desactivar el **inicio rápido** (Opciones de energía), porque impide WoL desde apagado completo.
4. Conexión por cable: la mayoría de tarjetas Wi-Fi no admiten WoL.

## Compilar desde el código

Requiere el [SDK de .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet run                                              # ejecutar en desarrollo
powershell -ExecutionPolicy Bypass -File tools\Publish.ps1 -Version 1.0.0   # portable + instalador en .\artifacts
```

El instalador necesita [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`).

### Publicar una versión en GitHub

```powershell
git tag v1.0.0
git push origin v1.0.0
```

El workflow `.github/workflows/release.yml` compila y adjunta el instalador y el portable a la Release.

## Estructura

```
Models/      Device, ScanResult
Services/    WakeOnLanService (paquete mágico), NetworkScanner, NetworkHelper (ARP/ping/DNS), DeviceStore (JSON)
ViewModels/  MainViewModel
Views/       MainWindow, DeviceDialog, ScanDialog, ConfirmDialog
Themes/      Theme.xaml (estilos Fluent)
Helpers/     WindowEffects (Mica), ObservableObject, RelayCommand
installer/   Script de Inno Setup
tools/       Publish.ps1, New-AppIcon.ps1
```

## Licencia

MIT. Consulta [LICENSE](LICENSE).
