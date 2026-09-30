# CodexBar para Windows

> [!IMPORTANT]
> **Este proyecto es un fork de [steipete/CodexBar](https://github.com/steipete/CodexBar)**, creado por
> [Peter Steinberger](https://github.com/steipete). La idea, el nombre, la arquitectura de proveedores y
> gran parte del código (en especial el motor en Swift que consulta los límites de uso) vienen del
> proyecto original. Este fork agrega un port nativo para **Windows 11** con su propia aplicación de
> bandeja en WPF. Si usás macOS o Linux, o necesitás alguno de los más de 60 proveedores que soporta
> CodexBar, usá el [repositorio original](https://github.com/steipete/CodexBar).

Muestra en el escritorio de Windows los límites de **Codex** y **Claude Code**: cuánto te queda de
cada ventana de uso, cuándo se renueva y qué cuenta tiene cuota disponible. Funciona con varias
cuentas por proveedor.

[![Windows 11](https://img.shields.io/badge/Windows-11%20x64-0a0a0c?style=flat-square)](#instalación)
[![.NET 10](https://img.shields.io/badge/.NET-10%20LTS-512bd4?style=flat-square)](Windows/global.json)
[![Upstream](https://img.shields.io/badge/upstream-steipete%2FCodexBar-d9d900?style=flat-square)](https://github.com/steipete/CodexBar)
[![License: MIT](https://img.shields.io/badge/license-MIT-6e5aff?style=flat-square)](LICENSE)

---

## Qué hace

### Límites de uso
- **Codex** (OpenAI) y **Claude Code** (Anthropic), usando la sesión que ya tienen sus CLI oficiales.
- Ventana de **Sesión** (5 horas) y **Semanal** de cada cuenta, con barra de progreso y porcentaje
  disponible.
- **Cuenta regresiva en vivo** hasta la próxima renovación, más la fecha completa localizada
  (por ejemplo, *jueves 2 de octubre, 3:40 p. m.*).
- Se omiten las cuotas semanales por modelo que repiten la información de la cuota general.

### Varias cuentas
- Detecta automáticamente perfiles aislados `%USERPROFILE%\.codex-*` y `%USERPROFILE%\.claude-*`
  además de las cuentas principales.
- Cada cuenta se consulta en un proceso independiente y de corta duración, así que el fallo de una
  cuenta no afecta a las demás.
- Clic sobre el nombre de una cuenta para renombrarla.
- Si una cuenta perdió la sesión, su tarjeta muestra **Copiar login**, que copia el comando de
  PowerShell para autenticar ese perfil en particular. El comando no contiene credenciales.
- Los tokens vencidos de Claude Code se renuevan automáticamente con el refresh token del perfil.

### Estado de la cuota
- Cuando **Sesión** o **Semanal** llega a 0 %, la tarjeta se marca en rojo y baja debajo de las cuentas
  que todavía tienen cuota.
- La tarjeta agotada muestra **"Disponible en …"** en ámbar, que pasa a verde cuando faltan 30 minutos
  o menos para la renovación.
- Cuando la cuota vuelve, la tarjeta regresa arriba y parpadea suavemente en verde hasta que hacés
  clic en ella.

### Ventana y bandeja
- Ícono en el área de notificación: clic para mostrar u ocultar; menú con *Abrir*, *Actualizar* y *Salir*.
- **Vista de tarjetas** con todo el detalle, o **vista Resumen** horizontal y compacta, que muestra solo
  la sesión y su cuenta regresiva.
- **Siempre visible** (◆), minimizar a la barra de tareas y ocultar a la bandeja sin cerrar la app.
- Recuerda la posición donde arrastraste la ventana.
- **Escala de la interfaz** del 80 % al 160 % (botón **Aa**), útil en monitores 4K.
- Actualiza los datos cada 5 minutos; la cuenta regresiva avanza cada minuto sin volver a consultar
  a los proveedores.

---

## Instalación

### Requisitos
- Windows 11 x64.
- [Codex CLI](https://learn.chatgpt.com/docs/codex) y/o [Claude Code](https://code.claude.com)
  instalados y con sesión iniciada.
- Si Windows avisa que falta el runtime de Visual C++, instalá el
  [Visual C++ Redistributable x64](https://aka.ms/vs/17/release/vc_redist.x64.exe).

El paquete incluye los runtimes de .NET y Swift, así que la PC de destino **no** necesita Swift, .NET
SDK, Visual Studio ni Git.

### Desde el ZIP
1. Extraé `CodexBar-Windows.zip`.
2. En PowerShell, dentro de la carpeta extraída:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass `
     -File .\Install-CodexBar.ps1 -DesktopShortcut -StartWithWindows
   ```

El instalador verifica el hash SHA-256 de cada archivo, instala en
`%LOCALAPPDATA%\Programs\CodexBar`, crea los accesos directos del menú Inicio y abre la app. No pide
permisos de administrador. `-DesktopShortcut` y `-StartWithWindows` son opcionales.

Para usarla sin instalar: `.\Test-CodexBar.ps1 -Launch`.

> El paquete todavía no está firmado, así que SmartScreen puede pedir confirmación.

Hay una guía paso a paso, desde una PC nueva, en
[docs/windows-installation.es.md](docs/windows-installation.es.md).

### Agregar cuentas secundarias

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Add-CodexBarAccounts.ps1 -Login
```

Crea `.codex-2` y `.claude-2` y abre el login de cada uno. Codex usa **código de dispositivo** por
defecto: abrís la URL en el perfil de navegador de la cuenta correcta y pegás el código. Si ese
método no está habilitado, activalo en la configuración de seguridad de ChatGPT. Otras opciones:

- `-ProfileName trabajo`: crea más perfiles con otro nombre.
- `-CodexBrowserLogin`: usa el flujo que abre el navegador.

### Actualizar
Con el repositorio clonado en la PC, doble clic en **`Update-CodexBar.cmd`**. El script:

1. Hace `git pull --ff-only` sobre `main`.
2. Compila el paquete.
3. Deja `CodexBar-Windows.zip` en el escritorio.
4. Reinstala la app.

### Desinstalar
Usá **Uninstall CodexBar** en el menú Inicio. Se conservan las preferencias y nunca se tocan las
credenciales de Codex ni de Claude. Para borrar también las preferencias:

```powershell
& "$env:LOCALAPPDATA\Programs\CodexBar\Uninstall-CodexBar.ps1" -RemoveSettings
```

---

## Seguridad y privacidad

- **CodexBar no guarda credenciales.** Lee los archivos que ya administran los CLI
  (`.codex\auth.json`, `.claude\.credentials.json`) y no copia tokens a su propia configuración.
- `%LOCALAPPDATA%\CodexBar\settings.json` solo contiene preferencias de presentación: nombres de las
  cuentas, vista, escala, posición y siempre visible.
- La app de bandeja **no lee archivos de credenciales**. Ejecuta el motor como proceso hijo, con lista
  de argumentos (sin shell), un timeout de 45 s y un límite de respuesta. Solo acepta JSON con versión
  de esquema conocida.
- Nunca muestra stderr ni salidas malformadas del motor, para no exponer datos sensibles en la interfaz.

---

## Arquitectura

```text
┌─────────────────────────────┐   proceso hijo por cuenta   ┌──────────────────────────────┐
│ CodexBar.Windows.Tray (WPF) │ ──────────────────────────▶ │ CodexBarWindowsEngine (Swift)│
│ bandeja · ventana · ajustes │ ◀────── JSON versionado ─── │ OAuth Codex / Claude Code    │
└─────────────────────────────┘                             └──────────────────────────────┘
            │ CodexBar.EngineClient: ejecución, validación de esquema, perfiles
```

| Componente | Ruta | Rol |
| --- | --- | --- |
| Motor portable | `Sources/CodexBarPortableCore`, `Sources/CodexBarWindowsEngine` | Swift con Foundation únicamente; consulta los límites y emite el snapshot JSON |
| Cliente del motor | `Windows/CodexBar.EngineClient` | Lanza el motor, valida el esquema y descubre perfiles |
| App de bandeja | `Windows/CodexBar.Windows.Tray` | WPF sobre .NET 10, sin dependencias externas |
| Scripts | `Windows/*.ps1` | Publicar, instalar, actualizar, desinstalar y agregar cuentas |

La app de macOS y `CodexBarCore` del proyecto original siguen en el repositorio sin cambios.

## Compilar desde el código

Requiere el [toolchain de Swift para Windows](https://www.swift.org/install/windows/) y el .NET 10 SDK.

```powershell
# Motor
swift build -c release --product CodexBarWindowsEngine
swift test --filter Portable

# App de bandeja y tests del cliente
cd Windows
dotnet build .\CodexBar.Windows.Tray\CodexBar.Windows.Tray.csproj -c Release
dotnet run --project .\CodexBar.EngineClient.Tests\CodexBar.EngineClient.Tests.csproj -c Release
```

En desarrollo, poné `CodexBarWindowsEngine.exe` junto a `CodexBar.Windows.Tray.exe` o definí
`CODEXBAR_ENGINE_PATH`.

Para generar el paquete distribuible:

```powershell
.\Windows\Publish-Windows.ps1 -SkipTests -ArchiveOnly `
  -ArchivePath "$env:USERPROFILE\Desktop\CodexBar-Windows.zip"
```

Los detalles técnicos (contrato del snapshot, límites del proceso, empaquetado) están en
[docs/windows.md](docs/windows.md).

## Pendiente

- Validación más amplia en distintos escritorios Windows.
- Soporte de Windows Credential Manager antes de que CodexBar administre secretos propios.
- Firma de binarios y paquete (y opcionalmente MSIX/MSI).
- Tests de paridad con los mapeos maduros de Codex y Claude de macOS/Linux.
- Rediseño visual con temas y colores personalizables.

Fuera de alcance por ahora: cookies de navegador, WebView2, ConPTY y proveedores distintos de Codex y
Claude Code.

---

## Créditos y licencia

- Proyecto original: **[CodexBar](https://github.com/steipete/CodexBar)** de
  [Peter Steinberger](https://github.com/steipete) ([codexbar.app](https://codexbar.app)).
- Port de Windows: [reiterstahl/CodexBar-win](https://github.com/reiterstahl/CodexBar-win).

Distribuido bajo la [licencia MIT](LICENSE), que conserva el copyright del autor original. Los avisos de
terceros del paquete de Windows están en [Windows/THIRD-PARTY-NOTICES.md](Windows/THIRD-PARTY-NOTICES.md).
