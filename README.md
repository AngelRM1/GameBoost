# GameBoost

Optimizador de red para juegos online en Windows. Mide tu ping en vivo, prueba servidores, activa el Modo Juego, optimiza TCP/IP y DNS, analiza tu PC y la optimiza (limpieza, inicio, servicios y efectos). Todo reversible y sin cuentas.

> **Aviso honesto:** GameBoost **no aumenta la velocidad de tu internet** ni acerca los servidores. Reduce la latencia y el jitter en tu propio PC: fondo, prioridad, TCP/DNS y elección de servidor. Si un programa te promete más velocidad contratada, desconfía.

Creado por [NousLogic](https://nouslogic.xyz) — Angel Rodríguez Martínez.

## Descargar (Windows 10 / 11, 64 bits)

Ve a la sección **[Releases](https://github.com/AngelRM1/GameBoost/releases)** y descarga la versión más reciente:

| Archivo | Para quién |
| --- | --- |
| `GameBoost-v1.4.0.zip` (155 MB) | **Recomendado.** Un solo `GameBoost.exe`, no necesita instalar .NET. Descomprime y ejecuta. |
| `GameBoost-v1.4.0-ligero.zip` (808 KB) | Si ya tienes instalado el [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0). |

Pasos:

1. Descomprime el zip donde quieras.
2. Ejecuta `GameBoost.exe` **como administrador** (el programa lo pide solo al abrirse).
3. Si Windows SmartScreen avisa que es un programa sin firmar, pulsa "Más información" → "Ejecutar de todas formas".

Guía completa de uso en [`docs/GUIA.md`](docs/GUIA.md).

## Qué hace

- **📶 Medidor en vivo:** ping actual, promedio, mínimo, máximo, jitter y pérdida, con gráfica de 60 segundos y comparación ANTES / DESPUÉS.
- **🌎 Servidores:** prueba 4 pings a Cloudflare, Google, OpenDNS, Quad9, Steam, Epic, Riot, Minecraft y servidores personalizados, y ordena por el mejor promedio.
- **🎮 Modo Juego:** cierra las apps que roban red, pone Alto rendimiento, pausa Windows Update y da prioridad Alta a tu juego. Al desactivarlo, todo vuelve.
- **⚙️ Optimización TCP/IP y DNS:** ajustes reversibles con respaldo en `%AppData%\GameBoost\backup.json`; DNS Cloudflare, Google o automático.
- **🔒 Teclas:** bloquea la tecla Windows y sus combinaciones (Win+D, Win+E, Win+R, Win+Tab, Alt+Tab...) mientras juegas. Win+L y Ctrl+Alt+Supr no se pueden bloquear: Windows los reserva por seguridad. Incluye una combinación de emergencia configurable (por defecto Ctrl+Alt+F12) para cerrar el juego si se cuelga.
- **💻 Mi PC:** lee tu procesador, RAM, gráfica (VRAM real, sin el límite de 4 GB de WMI), disco y pantalla; puntuación de 0 a 100, veredicto para juegos modernos y cuellos de botella con qué mejorar primero.
- **🧹 Optimizar PC:** limpieza de disco (temporales, caché de shaders, Papelera, Windows Update), programas de inicio, servicios en segundo plano y efectos visuales. Todo reversible, con respaldo.

## Lo que NO hace

- No sube tus datos a ningún servidor: todo se mide y se guarda solo en tu PC.
- No fuerza el anti-cheat de ningún juego. Si un juego como Battlefield 6 (EA Javelin) bloquea el cambio de prioridad, GameBoost te lo explica y no lo fuerza: forzarlo puede provocar un baneo.
- No es un "limpiador de registro" ni promete más FPS por arte de magia.

## Compilar desde el código fuente

Necesitas el [**.NET 8 SDK**](https://dotnet.microsoft.com/download/dotnet/8.0) (gratis, de Microsoft), en Windows o en Linux.

```bash
git clone https://github.com/AngelRM1/GameBoost.git
cd GameBoost/src
dotnet publish -r win-x64 -c Release -o ../publish
```

Eso genera `publish/GameBoost.exe` (un solo archivo, autocontenido, no necesita .NET instalado para correr). También sirve el script `src/build.sh`.

Para la versión ligera (necesita .NET 8 Desktop Runtime en el equipo donde corra):

```bash
dotnet publish -r win-x64 -c Release --self-contained false -p:PublishSingleFile=false -o ../publish-ligero
```

## Estructura

```text
src/
  Program.cs            Punto de entrada
  MainForm.cs           Ventana principal, pestañas y registro
  MainForm2.cs          Medidor, servidores, Modo Juego
  MainForm3.cs          Donaciones
  MainForm4.cs          Bloqueo de teclas y cierre de emergencia
  MainForm5.cs          Pestaña Mi PC
  MainForm6.cs          Pestaña Optimizar PC
  Services.cs           Ping, Modo Juego, TCP/IP, DNS
  PcSpecs.cs            Lectura de hardware (WMI) y puntuación
  PcOptimize.cs         Limpieza, inicio, servicios y efectos
  KeyboardBlocker.cs    Hook de teclado (WH_KEYBOARD_LL)
  ProcessPicker.cs      Selector de proceso del juego
  Styles.cs             Tema oscuro y controles personalizados
  app.manifest          requireAdministrator
  logo.png / logo.ico   Logo del programa
docs/
  GUIA.md               Guía de uso completa
```

## Donaciones

Si te sirve, puedes apoyar el desarrollo (voluntario, todo sigue gratis):

- PayPal: <https://www.paypal.com/paypalme/engelarm>
- Buy Me a Coffee: <https://buymeacoffee.com/engelarm>

## Licencia

© 2026 NousLogic — Angel Rodríguez Martínez. Todos los derechos reservados. El código se publica para su revisión y compilación personal; no está permitido redistribuirlo, revenderlo ni presentarlo como propio.
