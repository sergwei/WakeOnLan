# Wake on LAN

Aplicación para Windows 10 y 11 que **enciende los equipos de tu red local** a distancia, mediante Wake-on-LAN.
Guarda tus equipos una vez y, a partir de ahí, enciéndelos con un clic, sin levantarte de la silla.

## Instalación

Descarga la última versión desde [Releases](../../releases). Hay dos opciones:

- **`WakeOnLan-x.y.z-setup.exe`**: instalador. No pide permisos de administrador.
- **`WakeOnLan-x.y.z-portable.exe`**: un único archivo que se ejecuta sin instalar.

No necesita nada más instalado.

> La primera vez, Windows puede mostrar el aviso *"Windows protegió su PC"*, porque la aplicación es nueva y poco
> conocida. Pulsa **Más información → Ejecutar de todas formas**.

## Cómo se usa

### 1. Añade tus equipos
- **Escanear red**: busca los equipos encendidos de tu red. Marca los que quieras y pulsa **Añadir**.
- **Añadir manualmente** (`Ctrl+N`): escribe un nombre y la dirección MAC. Si el equipo está encendido y conoces
  su IP, **Obtener desde IP** rellena la MAC por ti.

Lo más cómodo es añadirlos mientras están encendidos: así la aplicación detecta sus datos sola.

### 2. Enciéndelos
- Pulsa **Encender** junto a un equipo, o selecciona varios y enciéndelos a la vez.
- El estado se actualiza solo: *en línea*, *apagado* o *encendiendo*.

### 3. Úsala desde la bandeja del sistema
- Marca con la **estrella ☆** los equipos que más uses para hacerlos favoritos.
- Al **minimizar o cerrar** la ventana, la aplicación se queda junto al reloj. Con un clic en su icono ves tus
  favoritos, su estado y un botón para encenderlos. Te avisa cuando un equipo termina de arrancar.
  Para cerrarla del todo, usa **Salir** en ese mismo panel.
- Con el instalador, se inicia con Windows directamente en la bandeja. Ambas cosas se pueden cambiar en
  **Más opciones (⋯)**: *Iniciar con Windows* y *Al cerrar, mantener en la bandeja*.

### Otras opciones
- **Clic derecho** sobre un equipo: editar, eliminar o copiar su MAC.
- **Más opciones (⋯)**: importar o exportar tu lista de equipos.
- Atajos: `Ctrl+N` añadir · `Ctrl+F` buscar · `F5` actualizar · `Supr` eliminar.
- **Línea de comandos**, para accesos directos o tareas programadas:

  ```
  WakeOnLan.exe /wake "PC del salón"
  WakeOnLan.exe /wake AA:BB:CC:DD:EE:FF
  WakeOnLan.exe /wakeall
  ```

## Si un equipo no se enciende

Wake-on-LAN tiene que estar activado **en el equipo que quieres encender**:

1. En la **BIOS/UEFI**, activa *Wake on LAN*. Según el fabricante, puede llamarse *Power On by PCI-E* o similar.
2. En Windows, abre **Administrador de dispositivos** → tu adaptador de red → **Propiedades**:
   - *Administración de energía*: marca "Permitir que este dispositivo reactive el equipo".
   - *Opciones avanzadas*: pon "Wake on Magic Packet" en *Habilitado*.
3. Desactiva el **inicio rápido**, porque impide encender el equipo cuando está apagado del todo.
   Está en Opciones de energía → "Elegir el comportamiento de los botones de inicio/apagado".
4. Conecta el equipo **por cable**: la mayoría de tarjetas Wi-Fi no admiten Wake-on-LAN.

## Seguridad y privacidad

- La aplicación solo se comunica con **tu red local**: no envía datos a Internet ni abre puertos.
- Tu lista de equipos se guarda únicamente en tu PC, en `%AppData%\WakeOnLan`.
- Para encender equipos **desde fuera de casa**, conéctate antes a tu red con una VPN, como WireGuard o Tailscale.
  **No abras puertos en el router para esto**: cualquiera en Internet podría encender tus equipos.

Si encuentras un problema de seguridad, avísame de forma privada desde la pestaña **Security** del repositorio.

## Compilar desde el código

Requiere el [SDK de .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet run --project src\WakeOnLan   # ejecutar
dotnet test                          # tests
```

## Licencia

MIT. Consulta [LICENSE](LICENSE).
