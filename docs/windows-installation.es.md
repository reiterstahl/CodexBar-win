# Instalación de CodexBar en una PC nueva

Esta guía instala CodexBar en Windows 11 x64 a partir del archivo
`CodexBar-Windows.zip`. La PC de destino no necesita Swift, .NET SDK,
Visual Studio, Git ni el repositorio del proyecto.

CodexBar no incluye ni transfiere credenciales. Codex CLI y Claude Code
administran sus propias sesiones, por lo que cada cuenta debe autenticarse
nuevamente en la PC nueva.

## 1. Requisitos

- Windows 11 de 64 bits.
- Una conexión a Internet para instalar los CLI y autenticar las cuentas.
- `CodexBar-Windows.zip`.
- Opcionalmente, `CodexBar-Windows.zip.sha256` para verificar la transferencia.

CodexBar incluye los runtimes de .NET y Swift que necesita. Si Windows informa
que falta un runtime de Microsoft Visual C++, instala el
[Microsoft Visual C++ Redistributable x64](https://aka.ms/vs/17/release/vc_redist.x64.exe)
y vuelve a intentarlo.

## 2. Instalar Codex CLI

Abre PowerShell y ejecuta el instalador oficial para Windows:

```powershell
powershell.exe -ExecutionPolicy Bypass -Command `
  "irm https://chatgpt.com/codex/install.ps1 | iex"
```

Cierra y abre PowerShell nuevamente y comprueba la instalación:

```powershell
codex --version
```

Autentica la cuenta principal mediante código de dispositivo:

```powershell
codex login --device-auth
```

Codex mostrará una URL y un código. Abre la URL en el perfil de navegador de
la cuenta correcta, introduce el código y regresa a PowerShell. Si el método
no está habilitado, activa **Device code login** en la configuración de
seguridad de ChatGPT.

Comprueba el estado:

```powershell
codex login status
```

## 3. Instalar Claude Code

En PowerShell, ejecuta el instalador oficial recomendado:

```powershell
irm https://claude.ai/install.ps1 | iex
```

También se puede instalar mediante WinGet:

```powershell
winget install Anthropic.ClaudeCode
```

Cierra y abre PowerShell nuevamente y comprueba la instalación:

```powershell
claude --version
```

Inicia Claude Code:

```powershell
claude
```

Completa el acceso en el navegador. Si no aparece automáticamente, escribe
`/login`. Cuando termine la autenticación, escribe `/exit`.

## 4. Extraer e instalar CodexBar

Copia `CodexBar-Windows.zip` a la PC nueva. Si también recibiste el archivo
`.sha256`, puedes mostrar el hash descargado con:

```powershell
(Get-FileHash .\CodexBar-Windows.zip -Algorithm SHA256).Hash.ToLowerInvariant()
Get-Content .\CodexBar-Windows.zip.sha256
```

Los dos valores deben coincidir. Después, desbloquea y extrae el ZIP:

```powershell
Unblock-File .\CodexBar-Windows.zip

$setupDirectory = Join-Path $env:TEMP `
  ("CodexBar-Setup-" + [guid]::NewGuid().ToString("N"))

Expand-Archive `
  -Path .\CodexBar-Windows.zip `
  -DestinationPath $setupDirectory
```

Instala CodexBar con acceso directo de Escritorio e inicio automático:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File "$setupDirectory\Install-CodexBar.ps1" `
  -DesktopShortcut `
  -StartWithWindows
```

El instalador:

- verifica todos los hashes internos del paquete;
- prueba el motor sin consultar las cuentas;
- instala en `%LOCALAPPDATA%\Programs\CodexBar`;
- crea accesos directos en Inicio y Escritorio;
- registra el inicio automático solo para el usuario actual;
- inicia CodexBar en el área de notificación.

No necesita permisos de administrador. Los parámetros `-DesktopShortcut` y
`-StartWithWindows` son opcionales.

## 5. Añadir las cuentas secundarias

El asistente incluido crea perfiles independientes para la segunda cuenta de
Codex y la segunda cuenta de Claude:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File "$env:LOCALAPPDATA\Programs\CodexBar\Add-CodexBarAccounts.ps1" `
  -Login
```

El perfil predeterminado es `2`:

```text
%USERPROFILE%\.codex-2
%USERPROFILE%\.claude-2
```

En el paso de Codex, la terminal mostrará una URL y un código sin abrir el
navegador predeterminado. Completa el acceso con la segunda cuenta. Después
se abrirá Claude Code para autenticar la segunda cuenta; usa `/login` si es
necesario y `/exit` cuando termine.

Para crear perfiles adicionales, proporciona un nombre corto:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File "$env:LOCALAPPDATA\Programs\CodexBar\Add-CodexBarAccounts.ps1" `
  -ProfileName trabajo `
  -Login
```

## 6. Comprobar la instalación

Abre **CodexBar** desde el menú Inicio o desde su icono en la bandeja y
presiona **Refresh**. Con dos cuentas por proveedor deben aparecer:

1. Codex principal.
2. Codex secundario.
3. Claude principal.
4. Claude secundario.

Puedes hacer clic en el nombre de cada cuenta para cambiarlo. Los botones del
encabezado cambian entre las vistas **Tarjetas**, **Resumen** y **Mini**,
actualizan los datos, activan **Siempre visible** (chincheta), abren
**Personalizar**, minimizan o esconden la ventana en el área de notificación.

**Personalizar** permite elegir tema (Grafito, Medianoche, Claro, Papel o Alto
contraste), color de acento (seis predefinidos, cualquier color o un código
`#RRGGBB`), tipo de gráfico (barras, anillos, medidor, bloques o números), cómo
se colorean los gráficos (acento, por nivel de cuota o un color por proveedor),
si se muestra el porcentaje disponible o usado, densidad y tamaño de la
interfaz (80% a 160%). Todo se aplica al instante y se guarda solo.

Cuando una cuenta no tiene una sesión válida, su tarjeta muestra **Sin sesión**
y el botón **Copiar login**. El botón copia al portapapeles el bloque de
PowerShell correcto para esa cuenta concreta, incluida su carpeta aislada. No
incluye contraseñas, tokens ni otros secretos.

Cuando Claude muestre `Paste code here`, pega el código con clic derecho o
`Shift+Insert`. Claude puede ocultar los caracteres pegados, por lo que el
campo puede parecer vacío. Usa solamente el código de la autorización que
acabas de iniciar; un código anterior, incompleto o acompañado por la URL puede
producir HTTP 400.

Debajo del nombre se ve el proveedor, el plan y la parte del correo anterior a
`@`. Para Claude y Codex, las vistas muestran únicamente **Sesión** y
**Semanal**; se omiten cuotas de modelo que duplican la información semanal.
Cada cuota muestra la cuenta regresiva y la fecha de renovación (la fecha
completa aparece al pasar el mouse). Con el **marcador de ritmo** activo, una
línea vertical sobre la barra indica dónde deberías estar si consumieras parejo
hasta la renovación, y un texto avisa si a este ritmo la cuota se agota antes.

Si **Sesión** o **Semanal** llega a 0% disponible, la tarjeta usa un fondo rojo
tenue y se mueve debajo de las cuentas que todavía tienen cuota (se puede
desactivar en **Personalizar**). Mientras está agotada, la tarjeta muestra
**Vuelve en …** en ámbar, que cambia a verde cuando quedan 30 minutos o menos.
Cuando las cuotas vuelven, la tarjeta regresa arriba, muestra **Disponible otra
vez** y parpadea suavemente en verde; haz clic sobre ella para descartar la
alerta. Si está activado, Windows muestra además una notificación.

El ícono del área de notificación muestra la sesión (barra gruesa) y la semana
(barra fina) de la cuenta más limitada, con un punto rojo si hay una cuenta
agotada o verde si una acaba de recuperarse. Se puede volver al ícono fijo
desde **Personalizar**.

Cuando el token de acceso de Claude Code vence, CodexBar renueva la sesión de
forma automática con el refresh token ya guardado por Claude Code. No abre el
navegador ni requiere volver a iniciar sesión. Solo necesitarás usar **Copiar
login** si Claude revoca la sesión o rechaza el refresh token.

Para una referencia de los comandos de inicio/cierre de sesión por perfil,
consulta [Comandos de cuentas de CodexBar para Windows](windows-account-commands.es.md).

Para comprobar el motor directamente:

```powershell
& "$env:LOCALAPPDATA\Programs\CodexBar\CodexBarWindowsEngine.exe" --pretty
```

El resultado debe incluir los proveedores `codex` y `claude`. El motor no
imprime tokens ni contraseñas.

## 7. Actualizar CodexBar

### Actualización completa con una sola orden

En la PC de compilación, el script `Update-CodexBar.ps1` actualiza `main`,
genera el ZIP, lo extrae temporalmente, reinstala CodexBar con acceso directo
de Escritorio e inicio automático, vuelve a abrir la aplicación y elimina la
extracción temporal:

Después de descargar el actualizador por primera vez, basta con hacer doble
clic en **`Update-CodexBar.cmd`**, ubicado en la raíz del repositorio. La
ventana permanece abierta al terminar para mostrar el resultado.

También se puede iniciar desde PowerShell con una sola orden:

```powershell
cd $env:USERPROFILE\source\CodexBar-win

powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\Windows\Update-CodexBar.ps1
```

El ZIP actualizado permanece en el Escritorio como
`CodexBar-Windows.zip`. El script se detiene antes de descargar cambios si el
repositorio no está en la rama `main` o contiene modificaciones locales sin
guardar.

Opcionalmente, ejecuta también todas las pruebas durante la actualización:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\Windows\Update-CodexBar.ps1 `
  -RunTests
```

### Actualización manual desde un ZIP

Extrae un ZIP nuevo en otra carpeta y ejecuta nuevamente su instalador:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\Install-CodexBar.ps1 `
  -DesktopShortcut `
  -StartWithWindows
```

El instalador cierra la versión anterior y la reemplaza. Conserva las cuentas,
los nombres personalizados, la escala, la posición y las demás preferencias.
No es necesario volver a autenticar.

## 8. Desinstalar

Usa **Uninstall CodexBar** en el menú Inicio. Esto elimina la aplicación y los
accesos directos, pero conserva preferencias y credenciales.

Para eliminar también las preferencias visuales de CodexBar:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File "$env:LOCALAPPDATA\Programs\CodexBar\Uninstall-CodexBar.ps1" `
  -RemoveSettings
```

La desinstalación nunca elimina `%USERPROFILE%\.codex*` ni
`%USERPROFILE%\.claude*`.

## Solución de problemas

### `swift.exe` no se reconoce al crear el ZIP

`Publish-Windows.ps1` busca automáticamente Swift en el `PATH`, en
`%LOCALAPPDATA%\Programs\Swift` y en `%ProgramFiles%\Swift`. También añade al
proceso tanto el toolchain como sus DLL de runtime; Swift puede cerrarse sin
mostrar versión si solo se agrega la carpeta `Toolchains` al `PATH`.

Si Swift realmente no está instalado, usa el paquete oficial y abre una nueva
ventana de PowerShell:

```powershell
winget install --id Swift.Toolchain -e --source winget
swift --version
```

### `codex` o `claude` no se reconoce

Cierra todas las ventanas de PowerShell y abre una nueva. Después ejecuta:

```powershell
Get-Command codex
Get-Command claude
```

Si alguno sigue sin aparecer, reinstala ese CLI con el comando de esta guía.

### SmartScreen muestra una advertencia

El paquete todavía no está firmado. Verifica el archivo `.sha256`, selecciona
**Más información** y ejecuta la aplicación solo si el ZIP proviene de una
fuente de confianza.

### Las cuentas nuevas no aparecen

Presiona **Refresh**. Si no aparecen, cierra CodexBar mediante **Exit** y
ábrelo desde Inicio. Comprueba también el motor con `--pretty`.

### La aplicación instalada no inicia

Cierra cualquier proceso anterior:

```powershell
Get-Process CodexBar.Windows.Tray -ErrorAction SilentlyContinue |
  Stop-Process -Force
```

Extrae nuevamente el ZIP en una carpeta vacía y vuelve a ejecutar
`Install-CodexBar.ps1`.

## Crear el ZIP transferible

Este paso solo se realiza en la PC de compilación, donde ya están instalados
Swift y .NET SDK:

```powershell
cd $env:USERPROFILE\source\CodexBar-win
git pull origin main

powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\Windows\Publish-Windows.ps1 `
  -SkipTests `
  -ArchiveOnly `
  -ArchivePath "$env:USERPROFILE\Desktop\CodexBar-Windows.zip"
```

Para la PC nueva solo se transfieren `CodexBar-Windows.zip` y, opcionalmente,
`CodexBar-Windows.zip.sha256`.

## Referencias oficiales

- [Instalación y repositorio de Codex CLI](https://github.com/openai/codex)
- [Autenticación de Codex en dispositivos sin navegador](https://learn.chatgpt.com/docs/auth#login-on-headless-devices)
- [Instalación y repositorio de Claude Code](https://github.com/anthropics/claude-code)
