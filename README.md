# Wake on LAN

Aplicación moderna para Windows 11 que enciende los equipos de tu red con **Wake-on-LAN**.
Inspirada en WakeMeOnLan, pero con interfaz Fluent (Mica, tema oscuro, iconos de Windows 11).

## Funcionalidades

- **Escanear la red**: encuentra equipos automáticamente (ARP + ping + DNS), incluso los que tienen el ping bloqueado.
- **Lista de dispositivos** con estado en vivo (en línea, apagado, encendiendo) y "visto por última vez".
- **Encender** uno, varios (selección múltiple) o todos.
- **Favoritos** (estrella ☆): aparecen primero y están siempre a mano desde la **bandeja del sistema**.
  Al minimizar, la app sigue en la bandeja: un clic en su icono muestra tus favoritos con su estado
  y un botón para encender los que estén apagados. Te avisa cuando un equipo termina de arrancar.
- **Iniciar con Windows** (opcional): arranca minimizada en la bandeja.
- **Añadir / editar / eliminar** dispositivos; obtener la MAC automáticamente a partir de la IP.
- Envío por **todas las interfaces de red** (broadcast general y de subred), o a una dirección fija.
- **Importar / exportar** la lista en JSON. Al importar, las direcciones de fuera de tu red local se descartan por seguridad.
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

### Verificar la descarga

Cada Release incluye `SHA256SUMS.txt` y una *attestation* de procedencia firmada por GitHub,
que demuestra que el ejecutable se compiló en este repositorio a partir del código publicado:

```powershell
Get-FileHash .\WakeOnLan-x.y.z-setup.exe -Algorithm SHA256        # compáralo con SHA256SUMS.txt
gh attestation verify .\WakeOnLan-x.y.z-setup.exe --repo sergwei/WakeOnLan
```

Los ejecutables todavía no están firmados con un certificado de código, así que Windows SmartScreen
puede mostrar "Editor desconocido" la primera vez.

## Requisitos del equipo que quieres encender

1. Wake-on-LAN activado en la **BIOS/UEFI**.
2. En Windows, en las propiedades del adaptador de red:
   - *Administración de energía* → "Permitir que este dispositivo reactive el equipo".
   - *Opciones avanzadas* → "Wake on Magic Packet" = Habilitado.
3. Recomendado: desactivar el **inicio rápido** (Opciones de energía), porque impide WoL desde apagado completo.
4. Conexión por cable: la mayoría de tarjetas Wi-Fi no admiten WoL.

## Seguridad

- Wake-on-LAN **no tiene autenticación**: cualquiera que pueda enviar un paquete a tu red local puede encender tus equipos.
- Para encender equipos **desde fuera de casa usa una VPN** (WireGuard, Tailscale…) y envía el paquete como si estuvieras en la red local.
  **No abras ni redirijas puertos UDP** del router hacia el broadcast de tu red: expone la LAN a cualquiera en Internet.
- La aplicación no abre puertos de entrada; solo envía paquetes UDP, pings y consultas ARP/DNS.
- Para informar de una vulnerabilidad, abre un *security advisory* privado en la pestaña **Security** del repositorio.

## Compilar desde el código

Requiere el [SDK de .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet run --project src\WakeOnLan                       # ejecutar en desarrollo
dotnet test                                             # tests unitarios
powershell -ExecutionPolicy Bypass -File tools\Publish.ps1 -Version 1.0.0   # portable + instalador en .\artifacts
```

El instalador necesita [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`).

### Publicar una versión en GitHub

```powershell
git tag v1.0.0
git push origin v1.0.0
```

El workflow `.github/workflows/release.yml` pasa los tests, compila, firma (si SignPath está configurado) y adjunta a la Release
el instalador, el portable, `SHA256SUMS.txt` y las attestations. Además, `ci.yml` compila y pasa los tests en cada push y pull request.

## Estructura

```
src/WakeOnLan/
  Models/      Device, ScanResult
  Services/    WakeOnLanService (paquete mágico), NetworkScanner, NetworkHelper (ARP/ping/DNS), DeviceStore (JSON), DeviceValidator
  ViewModels/  MainViewModel
  Views/       MainWindow, DeviceDialog, ScanDialog, ConfirmDialog
  Themes/      Theme.xaml (estilos Fluent)
  Helpers/     WindowEffects (Mica), ObservableObject, RelayCommand
tests/WakeOnLan.Tests/   Tests unitarios (xUnit)
installer/               Script de Inno Setup
signpath/                Configuración de artefactos para la firma de código
tools/                   Publish.ps1, New-AppIcon.ps1
```

## Code signing policy

Las versiones publicadas se firmarán con firma de código gratuita de [SignPath.io](https://about.signpath.io/),
con certificado de [SignPath Foundation](https://signpath.org/).

- Autores y revisores: [sergwei](https://github.com/sergwei)
- Aprobadores de firma: [sergwei](https://github.com/sergwei)

Solo se firman binarios compilados por GitHub Actions a partir del código de este repositorio, y cada versión
requiere aprobación manual.

**Privacidad:** este programa no transfiere ninguna información a otros sistemas de red, salvo que lo solicite
expresamente el usuario o la persona que lo instala o lo utiliza. Solo envía los paquetes Wake-on-LAN, los pings
y las consultas ARP/DNS necesarios para su funcionamiento, a las direcciones que configura el propio usuario.
## Licencia

MIT. Consulta [LICENSE](LICENSE).
