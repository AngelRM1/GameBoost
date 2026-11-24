# GameBoost v1.1 — Optimización de red para juegos online
Creado por **Nous Logic** — nouslogic.xyz

## Novedades v1.1 (2026-11-11)
- **Logo propio:** rayo verde sobre fondo oscuro, en el header, en el icono
  del .exe y en la barra de tareas. Archivos en `assets/logo.png`,
  `assets/logo.ico` y `assets/logo-full.png`.
- **Elegir el juego sin escribirlo:** en Modo Juego, botón
  **🔍 Elegir proceso...** — abre la lista de procesos en ejecución
  (con buscador y filtro "Solo con ventana"), doble clic lo elige.
  También **📁 Examinar .exe...** para buscar el archivo del juego.
  Ya no hay que escribir el nombre a mano. Si el juego no aparece en la
  lista, desmarca "Solo con ventana" y pulsa Actualizar.
- **Sección Donaciones:** nueva pestaña 💛 Donaciones con dos opciones:
  PayPal — https://www.paypal.com/paypalme/engelarm
  Buy Me a Coffee — https://buymeacoffee.com/engelarm
  Cada una con botón para abrir en el navegador y botón para copiar el
  enlace. Donar es voluntario, todo sigue gratis, sin funciones bloqueadas.

## Qué es (honesto)
GameBoost mide tu ping real, prueba servidores, cierra apps que roban red,
prioriza tu juego y aplica ajustes TCP/IP y DNS **reversibles**.

**Lo que NO hace:** no aumenta la velocidad contratada con tu ISP,
no baja el ping por arte de magia, no acerca servidores lejanos.
Si tu ping ya es bueno por cable y al servidor más cercano, la mejora será
de 0 a 5 ms. Si juegas por Wi-Fi con descargas de fondo, la mejora puede ser
de 20 a 40 ms. El programa te lo muestra con números, antes y después,
para que lo compruebes tú mismo.

## Archivos entregados
- `publish/GameBoost.exe` — **155 MB, un solo archivo, autocontenido.**
  No necesita instalar .NET. Es el recomendado. Doble clic y listo.
- `publish-ligero/GameBoost.exe` + `GameBoost.dll` — **149 KB + 61 KB.**
  Solo funciona si tu PC ya tiene instalado el
  **.NET 8 Desktop Runtime (x64)**. Descárgalo de Microsoft si te da error
  de que falta el runtime. Úsalo solo si no quieres bajar los 155 MB.
- `src/` — código fuente completo en C# (.NET 8, WinForms).

## Cómo probarlo en tu PC (5 minutos)
1. Copia `GameBoost.exe` a tu PC con Windows 10/11 (64 bits).
2. Clic derecho → **Ejecutar como administrador**.
   Acepta el aviso de Windows (UAC). Sin administrador, el programa avisa
   y no puede aplicar TCP/DNS ni el Modo Juego.
   > Windows SmartScreen puede avisar porque el .exe no tiene firma de
   > pago. Es normal en un programa nuevo sin certificado. Pulsa
   > "Más información" → "Ejecutar de todos modos" si confías en el origen.
3. Pestaña **Medidor en vivo**: elige 1.1.1.1, pulsa ▶ Iniciar medición,
   deja correr 30 segundos y pulsa **Marcar ANTES**.
4. Pestaña **Test de servidores**: pulsa **Probar todos**. Anota el mejor.
5. Pestaña **Modo Juego**: abre tu juego primero, pulsa
   **🔍 Elegir proceso...**, elige el juego de la lista (o
   **📁 Examinar .exe...**), marca Chrome/Edge/OneDrive/Spotify si los
   tienes abiertos, y pulsa **ACTIVAR Modo Juego**.
6. Pestaña **Optimización y DNS**: pulsa **Aplicar optimización TCP/IP**
   y **Cambiar DNS** (Cloudflare 1.1.1.1).
7. Vuelve al **Medidor**, mide otros 30 segundos y pulsa **Marcar DESPUÉS**.
   Ahí ves si mejoró, empeoró o no cambió nada.
8. Al terminar de jugar: **DESACTIVAR Modo Juego** y, si quieres,
   **Restaurar valores de Windows** en Optimización.
9. Si te sirve, pestaña **Donaciones**: PayPal o Buy Me a Coffee.

## Qué hace cada botón (detalle técnico)
- **Medidor:** ping ICMP cada 1 s con `System.Net.NetworkInformation.Ping`.
  Muestra actual, promedio, mín/máx, jitter (variación media entre pings
  seguidos) y pérdida %. Guarda los últimos 60 valores en la gráfica.
- **Test de servidores:** 4 pings a 1.1.1.1, 8.8.8.8, 208.67.222.222,
  9.9.9.9, Steam, Epic, Riot y Minecraft, más uno personalizado.
  Algunos servidores de juego bloquean ICMP: marcarán 100% pérdida aunque
  el juego funcione. Eso no es un error del programa.
- **Modo Juego:** cierra los procesos marcados, `powercfg /s` a Alto
  rendimiento, `sc stop wuauserv` (pausa Windows Update) y pone prioridad
  Alta al .exe del juego si está abierto. Al desactivar: vuelve a
  Equilibrado y `sc start wuauserv`.
- **Optimización TCP/IP:** `netsh int tcp set global` (autotuning=normal,
  rss=enabled, ecn=disabled, timestamps=disabled, initialrto=2000) y
  registro `TcpNoDelay=1`, `TcpAckFrequency=1`, `TcpDelAckTicks=0` por
  interfaz, más `NetworkThrottlingIndex=0xffffffff` y
  `SystemResponsiveness=0`. Guarda respaldo en
  `%AppData%\GameBoost\backup.json`. **Restaurar** borra esos valores y
  devuelve los de Windows (10 y 20).
- **DNS:** `netsh interface ipv4 set/add dnsservers` en el adaptador activo
  (Ethernet o Wi-Fi con puerta de enlace). Cloudflare 1.1.1.1/1.0.0.1 o
  Google 8.8.8.8/8.8.4.4, o volver a DHCP. Luego `ipconfig /flushdns`.

## Verificación hecha en esta entrega (2026-11-10 / v1.1 2026-11-11)
- Compilado con .NET SDK 8.0.428 en Linux para `win-x64`: **0 errores,
  0 advertencias**. El .exe es PE32+ GUI x86-64 para Windows.
- Manifiesto `requireAdministrator` incrustado (verificado en el binario).
- v1.1: recursos `GameBoost.logo.png` y `GameBoost.logo.ico` incrustados
  en el .dll (verificado), enlaces de PayPal y Buy Me a Coffee presentes
  en el binario (UTF-16, verificado), y diálogo `ProcessPickerForm`
  presente. `PrioritizeGame` ahora acepta ruta completa, nombre con
  .exe o sin él.
- Lógica de ping validada con datos simulados: 28, 32, 30, 45 ms →
  promedio 33.8 ms, jitter 7.0 ms, veredicto "Bueno". Caso vacío →
  100% pérdida, correcto.
- **No se pudo probar en Windows real en este entorno:** no hay PC con
  Windows ni Wine aquí. El ping ICMP real tampoco se pudo probar en el
  servidor Linux (los sockets raw están bloqueados: `ping: socket:
  Operation not permitted`). Por eso la prueba real en tu PC es el paso
  que falta: sigue la guía de 5 minutos de arriba y dime qué números
  ves en ANTES / DESPUÉS, y si el juego aparece en Elegir proceso.

## Recompilar
En Windows con .NET 8 SDK instalado, o en Linux con el SDK:
```
cd src
dotnet publish -r win-x64 -c Release -o ../publish
```
El proyecto es `src/GameBoost.csproj` (`net8.0-windows`, WinForms,
`EnableWindowsTargeting=true` para compilar desde Linux).

## Ideas para v1.1 (si quieres seguir)
- Medir ping al servidor real del juego que elijas (IP personalizada
  guardada por juego: Valorant, Fortnite, Warzone, LoL, etc.).
- Auto-detectar el juego abierto y activar el Modo Juego solo.
- Historial de sesiones ANTES/DESPUÉS guardado en CSV.
- Icono propio y firma de código (certificado) para quitar el aviso
  de SmartScreen.

## Novedades v1.2 (2026-11-11)
- **Mensaje descriptivo al priorizar:** ya no sale solo "Acceso denegado".
  GameBoost intenta Alta, si no lo permite intenta "Por encima de lo normal",
  y si tampoco, explica en la pantalla: en Battlefield 6 es EA Javelin,
  en Valorant es Vanguard, en Fortnite es Easy Anti-Cheat. No es un error
  tuyo ni de GameBoost, y no se debe forzar (riesgo de baneo). El resto del
  Modo Juego sigue funcionando. Para juegos sin anti-cheat, el mensaje dice
  que compruebes que GameBoost corre como administrador.
- **Diseno profesional:** pestañas dibujadas a medida (la activa en verde,
  las demas en gris oscuro), tarjetas con esquinas redondeadas y borde sutil,
  botones con hover, header con linea verde y tipografia mas limpia.
- **Descripciones amigables:** pasa el mouse por encima de cada boton o campo
  y veras que hace, en palabras simples. Cada seccion tiene una frase arriba
  explicando para que sirve.
- **Nueva seccion 🔒 Teclas (bloqueo de teclas de Windows):** bloquea la tecla
  Windows sola, Win+D, Win+E, Win+R, Win+Tab, Win+S, Win+I, Win+X, Alt+Tab
  y Ctrl+Esc, para que nada te saque del juego por accidente. Incluye boton
  "Probar bloqueo 10 s" y opcion en Modo Juego para activarlo automaticamente.
  Solo funciona mientras GameBoost este abierto; al cerrarlo, todo vuelve.
  Win+L y Ctrl+Alt+Supr NO se pueden bloquear: Windows los reserva por
  seguridad, no es un fallo de GameBoost.

## Verificacion v1.2
- Compilado 0 errores / 0 advertencias, PE32+ GUI x64, requireAdministrator.
- Logica de prioridad validada en Linux reutilizando Services.cs:
  bf6 y Valorant se detectan como protegidos, notepad no; un proceso
  inexistente da un mensaje descriptivo sin romper; un proceso normal sin
  permiso da el mensaje generico de administrador.
- El hook de teclado (WH_KEYBOARD_LL, user32.dll) compila para win-x64 y
  sus textos estan verificados en el .exe, pero NO se pudo probar en Windows
  real en este entorno (no hay PC con Windows). Pruebalo en tu PC con el
  boton "Probar bloqueo 10 s" y dime si la tecla Windows deja de sacarte.

## Novedades v1.3 (2026-11-17)
- **Nueva pestaña 💻 Mi PC:** pulsa "Analizar mi PC" y lee tu procesador,
  RAM, gráfica, disco, sistema, placa y pantalla (por WMI). Te da una
  puntuación general de 0 a 100 y por componente (gráfica 40%, procesador
  30%, RAM 20%, disco 10%), y un veredicto en palabras simples: Potente,
  Bueno, Medio, Justo o Básico para juegos modernos y realistas.
  También lista los puntos críticos / cuellos de botella (poca RAM, poca
  VRAM, HDD en vez de SSD, pocos núcleos...) con por qué frena y qué mejorar
  primero. Puedes copiar el informe o guardarlo en el Escritorio como
  GameBoost-Mi-PC.txt. Es una estimación, no un benchmark.
- **Cierre de emergencia en 🔒 Teclas:** nueva sección para crear tu propia
  combinación (Ctrl/Alt/Shift/Win + F1-F12, Esc, Supr, Fin, Q, X, C o K;
  por defecto Ctrl + Alt + F12). Si el juego se crashea y no responde,
  pulsas esa combinación y GameBoost cierra solo el juego elegido en Modo
  Juego, sin reiniciar la PC. Se guarda en %AppData%\GameBoost\settings.json
  y se recuerda al abrir el programa. También hay botón "Cerrar juego ahora".
  Funciona cuando el bloqueo está ACTIVADO (el hook solo escucha mientras
  GameBoost está abierto). No cierra Windows ni otros programas.
- **Alto y scrolls arreglados:** la ventana ahora es más alta (860 px en vez
  de 780) y todas las pestañas tienen scroll vertical y horizontal: si el
  contenido no cabe (pantalla chica o escala de Windows al 125/150%),
  aparecen las barras y puedes bajar hasta el último botón.

## Verificación v1.3
- Compilado 0 errores / 0 advertencias, PE32+ GUI x64, requireAdministrator.
- Lógica de puntuación validada con dos PCs de prueba: uno potente
  (i7 8 núcleos, 32 GB DDR5, RTX 4070 12 GB, NVMe) da 92/100 "Potente";
  uno justo (i3 4 núcleos, 8 GB, GTX 1650 4 GB, HDD) da 46/100 "Justo".
  WMI sin datos (Linux) devuelve "No detectado" sin romper.
- Cerrar juego sin juego elegido o con juego inexistente devuelve 0 y un
  mensaje amigable, sin excepción. La combinación se guarda y se muestra
  como "Ctrl + Shift + F9" en la prueba.
- El hook de teclado y la lectura WMI real no se pudieron probar en Windows
  en este entorno (no hay PC con Windows). Pruébalo en tu PC: Analizar mi PC
  y la combinación de emergencia con un juego abierto.

## Corrección v1.3.1 (2026-11-17) — VRAM mal leída
- **El bug:** en una RTX 5080 de 16 GB, Mi PC decía "4 GB de VRAM" y
  marcaba "Poca VRAM" como crítico. Causa: Win32_VideoController.AdapterRAM
  es un número de 32 bits y se capa en 4.294.967.295 bytes = 4,0 GB. Los
  drivers devuelven ese tope para cualquier gráfica con más de 4 GB, y el
  programa lo tomaba como si fuera el valor real.
- **La corrección:** la VRAM ahora se lee en este orden: WMI (solo si no
  está en el tope de 4 GB), `nvidia-smi` (el driver NVIDIA da la VRAM
  exacta, 16384 MiB en una RTX 5080) y el registro
  `HardwareInformation.qwMemorySize` (64 bits, sin límite de 4 GB).
  Si ninguna fuente es fiable, el programa dice "más de 4 GB (no se pudo
  leer el valor exacto)" en vez de inventar un 4 GB, y no marca "Poca VRAM".
- **Tabla de GPUs actualizada a noviembre de 2026:** serie RTX 50
  (5090/5080/5070 Ti/5070/5060 Ti/5060/5050), RX 9070 XT / 9070 / 9060 XT /
  9060 (RDNA 4) y Arc B580/B570 (Battlemage). Una GPU desconocida sin VRAM
  fiable ya no se castiga con 25/100, queda en 50 neutro.

## Verificación v1.3.1
- Compilado 0 errores / 0 advertencias, PE32+ GUI x64, requireAdministrator.
- Con una PC sintética RTX 5080 16 GB + i7 + 32 GB + NVMe: GPU 94/100,
  general 95/100 "Potente para juegos modernos", sin aviso de "Poca VRAM".
- ResolveVram con el tope de WMI (4,0 GB) devuelve "no fiable" en vez de
  4 GB; con 8 GB reales de WMI devuelve 8 GB fiable. RX 9070 XT = 89,
  Arc B580 = 73, RTX 5070 Ti = 90. (Durante la prueba se encontró y corrigió
  un error: el primer chequeo de "tope" rechazaba también 8 GB reales.)
- La lectura real con nvidia-smi / registro en Windows sigue pendiente de
  probar en tu PC: en la tuya debe salir 16 GB.

## Corrección v1.3.2 (2026-11-20) — barras de Mi PC
- **El problema:** las barras de puntuación usaban el control ProgressBar
  de WinForms, que en Windows 10/11 tiene un brillo animado aunque el
  valor ya esté fijo, y parecía que nunca terminaba de cargar.
- **La corrección:** ahora son barras estáticas: un panel oscuro de fondo
  y un panel de color encima con el ancho según la puntuación. No se mueven
  nunca. Color por puntuación: verde 70+, ámbar 55-69, rojo menos de 55.
  El número (ej. 94/100) sigue al lado en texto.

## Verificación v1.3.2
- Compilado 0 errores / 0 advertencias, PE32+ GUI x64, requireAdministrator.
- No queda ningún `new ProgressBar` en el código de Mi PC (solo queda la
  palabra en un comentario que explica el cambio).

## Novedades v1.4 (2026-11-20) — pestaña 🧹 Optimizar PC
Nueva pestaña con las 4 optimizaciones seguras, todas reversibles y con
respaldo antes de aplicar. Nada borra documentos, fotos ni juegos.
- **Limpieza de disco:** temporales de Windows y del usuario, caché
  DirectX/shaders (D3DCache, NVIDIA, AMD), vaciar Papelera y limpieza de
  Windows Update (DISM, tarda minutos en segundo plano). Botón Calcular
  espacio antes de borrar, confirmación antes de limpiar, y no borra
  archivos en uso ni modificados hace menos de 1 hora.
- **Programas de inicio:** lista lo que se abre al encender (registro +
  carpeta Inicio). Desmarcar uno solo evita que se abra solo; el programa
  sigue instalado. Usa el byte de StartupApproved como el Administrador
  de tareas, no borra los valores del registro.
- **Servicios en segundo plano:** SysMain, Búsqueda, Telemetría, Fax,
  Mapas y Registro remoto (marcados por defecto); Xbox y Cola de impresión
  desmarcados por defecto, con aviso si usas Game Pass o impresora. Se
  ponen en Manual y se detienen (no deshabilitados del todo); el respaldo
  guarda el inicio original de cada uno.
- **Efectos visuales:** quitar animaciones, quitar transparencia y ajuste
  para mejor rendimiento, cada uno por separado, con restaurar.
- Botón **Restaurar todo lo de esta pestaña**: devuelve inicio, servicios
  y efectos a como estaban.
- Las pestañas ahora son 8, por eso su ancho bajó de 150 a 128 px para
  que quepan todas en la ventana.

## Verificación v1.4
- Compilado 0 errores / 0 advertencias, PE32+ GUI x64, requireAdministrator.
- Lógica de limpieza validada en prueba: carpeta con 4 archivos (7.500
  bytes) — medir contó 4/7.500, limpiar borró 3 viejos (3.500 bytes),
  saltó 1 reciente (<1 h), borró la subcarpeta vacía; segunda limpieza 0.
  StartupService sin registro devuelve lista vacía sin romper; 11 servicios
  con sus valores por defecto correctos (Xbox e impresora desmarcados).
- La aplicación real en Windows (registro, `sc`, DISM, efectos) sigue
  pendiente de probar en tu PC.

## Novedades v1.5 (2026-11-23) — menú vertical, Temperaturas y diseño minimalista
- **Menú vertical a la izquierda:** las 9 secciones ahora se eligen desde
  una barra lateral (Medidor, Servidores, Modo Juego, Optimización, Teclas,
  Mi PC, Optimizar, Temperaturas y Donaciones). La ventana es más ancha
  (1200 px) para que el contenido no pierda espacio.
- **Temperaturas (nueva pestaña):** temperaturas, voltajes, potencia,
  ventiladores y carga de CPU, GPU, placa, RAM y discos, en vivo cada 2 s,
  leídos con LibreHardwareMonitor. Si tu placa no expone un sensor, no
  aparece: no es un fallo del programa.
- **Alerta del cable de alimentación de la GPU:** vigila la potencia de la
  GPU contra su TDP, el punto caliente y el carril de 12 V. Aviso si la GPU
  consume más del 90% de su TDP, el punto caliente pasa de 100 °C o el 12 V
  sale de 11,4–12,6 V; crítico si pasa del 110% del TDP, 108 °C de punto
  caliente o el 12 V sale de 11,0–13,0 V. También avisa si la CPU pasa de
  90 °C o el VRM de 85 °C. Las alertas solo se registran cuando cambian.
  Honesto: el software NO puede medir la corriente por cada pin del
  conector 12VHPWR; la alerta usa potencia, temperatura y voltaje, que son
  los indicadores que sí se pueden leer. Revisa el conector bien asentado,
  sin adaptadores en cadena y sin el cable doblado junto al conector.
- **Diseño minimalista:** fondo casi negro, tarjetas planas con borde
  sutil, un solo acento verde, menú sin iconos y cabecera más limpia.
- System.Management subió de 8.0.0 a 9.0.0 (lo exige
  LibreHardwareMonitorLib 0.9.4).

## Verificación v1.5
- Compilado 0 errores / 0 advertencias, PE32+ GUI x64, requireAdministrator.
- Lógica de alertas validada en prueba con sensores simulados: RTX 5080
  normal (300 W, 82 °C hotspot, 12,05 V) = sin alertas; pesada (380 W,
  105% del TDP, hotspot 102 °C) = aviso; crítica (420 W, 116%, hotspot
  110 °C, 12 V en 11,2 V) = crítico; 12 V en 12,6 V = dentro de norma;
  11,2 V = aviso; 10,9 V = crítico; TDP por modelo (RTX 5070 Ti = 300 W,
  no 250 W) y GPU desconocida con umbrales absolutos (500 W aviso,
  600 W crítico). Durante la prueba se encontraron y corrigieron dos
  fallos: el punto caliente contaba también como temperatura del núcleo
  (falso crítico) y el límite de 12,6 V se marcaba como aviso en vez de
  dentro de norma.
- La lectura real de sensores en Windows sigue pendiente de probar en tu
  PC (depende de la placa y la gráfica).

## Corrección v1.5.1 (2026-11-23) — menú lateral y falsa alerta de 255 °C
- **Menú lateral corregido:** en v1.5 las pestañas salían en varias
  columnas estrechas y el texto se cortaba ("D...", "M...") con fondo
  blanco. Causa: con las pestañas a la izquierda, WinForms transpone el
  tamaño (el ancho en pantalla es ItemSize.Height y el alto es
  ItemSize.Width), así que (190, 46) dibujaba pestañas de 46 px de ancho
  por 190 de alto. Ahora es (46, 190): una sola columna de 190 x 46 con
  el texto completo, y las pestañas no seleccionadas se pintan con el
  fondo oscuro en vez del blanco nativo.
- **Falsa alerta de GPU a 255 °C:** algunos sensores ausentes reportan
  255 °C como valor centinela y GameBoost lo marcaba como alerta crítica.
  Ahora se ignoran las temperaturas imposibles (más de 125 °C o menos de
  -40 °C): una GPU real a 102 °C sigue dando alerta crítica, pero un
  255 °C fantasma ya no aparece.

## Corrección v1.5.2 (2026-11-23) — sin fondos blancos, nombres claros, textos amigables
- **Sin fondos blancos:** el TabControl nativo dejaba blanca la tira
  vacia bajo el menu y un filo a la derecha. El menu ahora es un Panel
  propio con 9 botones oscuros (opcion activa con fondo claro y barra
  verde a la izquierda): todo el programa usa sus colores, sin blancos
  del control de Windows. La barra de titulo azul es de Windows, no de
  GameBoost.
- **Nombres que ya no se confunden:** "Optimización" (TCP/IP y DNS) ahora
  se llama **Red**, y "Optimizar" (limpieza, arranque, servicios y
  efectos) ahora se llama **Optimizar PC**.
- **Textos mas descriptivos y amigables:** titulos que explican que hace
  cada seccion ("Red: ajustes de TCP/IP y DNS para que tus paquetes
  lleguen antes", "Optimizar PC: limpia, arranca mas rapido y quita lo
  que estorba. Todo se puede deshacer"), botones que dicen lo que hacen
  ("Elegir mi juego abierto", "Buscar el .exe del juego", "Ver cuanto se
  puede liberar", "Guardar cambios de inicio", "Leer sensores ahora",
  "Deshacer todo lo de esta seccion") y las descripciones largas ya no
  se cortan: los textos que ocupan dos lineas tienen su alto reservado.

## Diagnóstico v1.5.3 (2026-11-24) — el exe no arrancaba en la PC del usuario
- El exe publicado estaba íntegro (PE válido, todos los ensamblados
  dentro del bundle, 164.927.768 bytes) y el código de arranque ya
  estaba protegido, así que la causa estaba fuera del programa
  (SmartScreen/Defender, descarga incompleta o UAC).
- Esta versión añade un registro de arranque: al abrir GameBoost se
  escribe GameBoost-inicio.txt en %TEMP% (y junto al exe), con cada
  paso del arranque. Si algo falla, sale una ventana con el error y la
  ruta del registro, en vez de cerrarse en silencio.
- Si el exe no abre: 1) clic derecho en el exe → Propiedades → si
  aparece "Desbloquear", márcalo y Aceptar; 2) clic derecho →
  "Ejecutar como administrador"; 3) revisa Seguridad de Windows →
  Historial de protección, por si Defender lo puso en cuarentena;
  4) comprueba que el archivo pese 158 MB (164.927.768 bytes): si pesa
  menos, la descarga quedó incompleta y hay que bajarlo de nuevo.

## Corrección v1.5.4 (2026-11-24) — el exe no arrancaba (error TabPage)
- Causa real, gracias al diagnóstico de la v1.5.3: al quitar el
  TabControl en la v1.5.2, las páginas siguieron siendo TabPage, y
  WinForms no permite meter un TabPage dentro de un Panel normal
  ("Sólo se pueden agregar TabPages a TabControls"). Por eso el exe
  se cerraba al arrancar con ArgumentException.
- Arreglo: las 9 páginas ahora son Paneles normales. El menú lateral,
  los nombres (Red / Optimizar PC) y los textos de la v1.5.2 quedan
  igual, y el registro de arranque de la v1.5.3 sigue activo.

## v1.5.5 (2026-11-24) — barras de desplazamiento oscuras
- Las barras de WinForms las dibuja Windows con el tema claro y no hay
  una propiedad para cambiarles el color. Se usan dos interruptores del
  propio Windows: DWMWA_USE_IMMERSIVE_DARK_MODE en la ventana principal
  (tambien pone oscura la barra de titulo) y SetWindowTheme
  "DarkMode_Explorer" en cada control con scroll (paginas, listas de
  inicio/servicios, rejilla de sensores y cuadro de registro). Si el
  Windows no lo soporta, todo sigue igual que antes. El resultado solo
  se puede comprobar en un Windows real.

## v1.5.6 (2026-11-24) — barras oscuras en todas partes
- La v1.5.5 solo cubria las paginas, el registro, las listas y la
  rejilla. Ahora el tema oscuro se aplica recorriendo TODOS los
  controles de la ventana: tambien los desplegables de Medidor y
  Teclas, el cuadro de cuellos de botella de Mi PC y la lista del
  dialogo "Elegir proceso" (que ademas lleva su barra de titulo
  oscura). Cualquier scroll que se añada luego queda cubierto.

## v1.6.0 (2026-11-24) — paquete recomendado: Rendimiento de Windows
Nueva sección al final de Optimizar PC (baja con el scroll):
- **Modo Juego de Windows + HAGS:** Modo Juego por registro
  (AutoGameModeEnabled) y HAGS por registro (HwSchMode 2). HAGS se
  aplica al reiniciar y solo funciona si la gráfica y su driver lo
  soportan; si no, Windows lo ignora.
- **Energía y grabación:** plan Máximo rendimiento (se crea con
  powercfg si no existe) y vuelta a Equilibrado; Game DVR desactivado
  (AppCaptureEnabled + GameDVR_Enabled).
- **Liberar espacio:** apagar/encender hibernación (powercfg,
  apaga también el inicio rápido) con el peso de hiberfil.sys, y
  limpieza de la caché de Windows Update (para el servicio, borra
  las descargas viejas, lo vuelve a arrancar).
- **Salud del disco:** lista de discos con tipo (SSD/HDD), tamaño y
  estado SMART, Analizar (fragmentación con defrag /A) y Optimizar
  (TRIM en SSD, desfragmentar en HDD, en segundo plano).
- Todo lo reversible se guarda en %AppData%\GameBoost\perf-backup.json
  al primer cambio y "Deshacer todo lo de esta sección" lo devuelve.
  La caché de Update y el TRIM/defrag no se pueden deshacer, pero no
  se pierde nada (Windows vuelve a descargar; el disco solo se ordena).

## Corrección v1.6.1 (2026-11-24) — las barras oscuras no se veían
- Causa real: los controles de las secciones ocultas aún no tienen su
  ventana de Windows creada cuando arranca el programa, y sin esa
  ventana el tema oscuro no se les puede aplicar. Por eso las barras
  seguían claras al abrir una sección por primera vez.
- Arreglo: el tema oscuro ahora se aplica dos veces: al arrancar se
  fuerza la creación de las ventanas de todos los controles, y cada
  vez que cambias de sección se vuelve a aplicar. Además el registro
  escribe "Tema oscuro aplicado a las barras de desplazamiento." al
  arrancar, para comprobar que el código corrió.
- Si al abrir esta versión la barra de título sigue azul y el registro
  no dice esa línea, es que se está ejecutando un exe viejo: la barra
  de título debe decir v1.6.1 y el archivo debe pesar 158 MB.

## Novedades v1.7.0 (2026-11-24) — Perfiles por juego + modo automatico
- Nueva pestana **Perfiles** (menu izquierdo, despues de Modo Juego):
  - Crea un perfil por juego con los ajustes que tienes en Modo Juego
    (que apps cerrar, alto rendimiento, pausa de Update, bloqueo de
    teclas). Se guarda en %AppData%\GameBoost\profiles.json.
  - Botones: Nuevo perfil (te pide el juego y un nombre), Aplicar y
    activar, Cargar (pone el perfil en Modo Juego para editarlo),
    Guardar (actualiza el perfil con los ajustes de Modo Juego),
    Renombrar y Eliminar.
- **Modo automatico**: una casilla en Perfiles. Si la marcas, GameBoost
  vigila cada 5 segundos y activa el Modo Juego solo cuando se abre un
  juego que tiene perfil; al cerrarse el juego, lo desactiva y deja todo
  como estaba. La casilla se recuerda entre sesiones.
- Si lo apagas a mano mientras juegas, no se vuelve a activar solo hasta
  que cierres y reabras el juego (para que no te pelee el control).
- Para que el modo automatico funcione, GameBoost tiene que estar abierto
  (puede estar minimizado). Solo vigila juegos con perfil.

## Correccion + novedades v1.8.0 (2026-11-24)
- Corregido el contenedor de Perfiles: la ultima fila de botones
  (Cargar / Guardar / Renombrar) se salia 2 px por debajo del borde
  del contenedor. La tarjeta ahora tiene 12 px de margen inferior.
- **Comparar DNS** (pestana Red): nueva tarjeta que pregunta a cada
  DNS cuanto tarda en responder (consulta UDP directa al puerto 53,
  3 intentos por servidor) y los ordena del mas rapido al mas lento:
  Cloudflare, Google, Quad9, OpenDNS y el DNS actual de tu ISP.
  El boton "Aplicar el mas rapido" cambia el DNS de tu adaptador al
  ganador. No cambia nada hasta que lo pulses.
- **Aviso de nueva version**: al arrancar, GameBoost revisa en GitHub
  si hay una version mas nueva (en segundo plano, sin molestar; si no
  hay internet, no pasa nada). Si la hay, el boton "Buscar
  actualizaciones" del encabezado cambia a "¡Nueva version: vX.Y.Z!"
  en verde y al pulsarlo pregunta si quieres abrir la descarga.
  El boton tambien sirve para revisar a mano en cualquier momento.
